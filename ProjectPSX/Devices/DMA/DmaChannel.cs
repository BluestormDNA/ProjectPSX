using System;

namespace ProjectPSX.Devices;
public sealed class DmaChannel : Channel {

    private uint baseAddress;
    private uint blockSize;
    private uint blockCount;

    private uint transferDirection;
    private uint memoryStep;
    private uint choppingEnable;
    private uint syncMode;
    private uint choppingDMAWindowSize;
    private uint choppingCPUWindowSize;
    private bool enable;
    private bool trigger;

    private uint unknownBit29;
    private uint unknownBit30;

    private BUS bus;
    private InterruptChannel interrupt;
    private int channelNumber;

    private uint pendingBlocks;

    public DmaChannel(int channelNumber, InterruptChannel interrupt, BUS bus) {
        this.channelNumber = channelNumber;
        this.interrupt = interrupt;
        this.bus = bus;
    }

    public override uint load(uint register) {
        switch (register) {
            case 0: return baseAddress;
            case 4: return blockCount << 16 | blockSize;
            case 8: return loadChannelControl();
            default: return 0;
        }
    }

    private uint loadChannelControl() {
        uint channelControl = 0;

        channelControl |= transferDirection;
        channelControl |= (memoryStep == 4 ? 0 : 1u) << 1;
        channelControl |= choppingEnable << 8;
        channelControl |= syncMode << 9;
        channelControl |= choppingDMAWindowSize << 16;
        channelControl |= choppingCPUWindowSize << 20;
        channelControl |= (enable ? 1u : 0) << 24;
        channelControl |= (trigger ? 1u : 0) << 28;
        channelControl |= unknownBit29 << 29;
        channelControl |= unknownBit30 << 30;

        if (channelNumber == 6) {
            return channelControl & 0x5000_0002 | 0x2;
        }

        return channelControl;
    }

    public override void write(uint register, uint value) {
        switch (register) {
            case 0: baseAddress = value & 0xFFFFFF; break;
            case 4: blockCount = value >> 16; blockSize = value & 0xFFFF; break;
            case 8: writeChannelControl(value); break;
            default: Console.WriteLine($"Unhandled Write on DMA Channel: {channelNumber} register: {register} value: {value}"); break;
        }
    }

    private void writeChannelControl(uint value) {
        transferDirection = value & 0x1;
        memoryStep = (uint)((value >> 1 & 0x1) == 0 ? 4 : -4);
        choppingEnable = value >> 8 & 0x1;
        syncMode = value >> 9 & 0x3;
        choppingDMAWindowSize = value >> 16 & 0x7;
        choppingCPUWindowSize = value >> 20 & 0x7;
        enable = (value >> 24 & 0x1) != 0;
        trigger = (value >> 28 & 0x1) != 0;
        unknownBit29 = value >> 29 & 0x1;
        unknownBit30 = value >> 30 & 0x1;

        if (!enable) pendingBlocks = 0;

        handleDMA();
    }

    private void handleDMA() {
        if (!isActive() || !interrupt.isDMAControlMasterEnabled(channelNumber)) return;
        if (syncMode == 0) {
            //if (choppingEnable == 1) {
            //    Console.WriteLine($"[DMA] Chopping Syncmode 0 not supported. DmaWindow: {choppingDMAWindowSize} CpuWindow: {choppingCPUWindowSize}");
            //}

            blockCopy(blockSize == 0 ? 0x10_000 : blockSize);
            finishDMA();

        } else if (syncMode == 1) {
            // HACK:
            // GPUIn: Bypass blocks to elude mdec/gpu desync as MDEC is actually too fast decoding blocks
            // MdecIn: GranTurismo produces some artifacts that still needs to be checked otherwise it's ok on other games i've checked
            if (channelNumber == 2 && transferDirection == 1 || channelNumber == 0) {
                blockCopy(blockSize * blockCount);
                finishDMA();
                return;
            }

            trigger = false;
            pendingBlocks = blockCount;
            transferBlockIfPending();
        } else if (syncMode == 2) {
            linkedList();
            finishDMA();
        }
    }

    private void finishDMA() {
        enable = false;
        trigger = false;

        interrupt.handleInterrupt(channelNumber);
    }

    private void blockCopy(uint size) {
        if (transferDirection == 0) { //To Ram

            switch (channelNumber) {
                case 1: bus.DmaFromMdecOut(baseAddress, (int)size); break;
                case 2: bus.DmaFromGpu(baseAddress, (int)size); break;
                case 3: bus.DmaFromCD(baseAddress, (int)size); break;
                case 4: bus.DmaFromSpu(baseAddress, (int)size); break;
                case 6: bus.DmaOTC(baseAddress, (int)size); break;
                default: Console.WriteLine($"[DMA] [BLOCK COPY] Unsupported Channel (to Ram) {channelNumber}"); break;
            }

            baseAddress += memoryStep * size;

        } else { //From Ram

            var dma = bus.DmaFromRam(baseAddress & 0x1F_FFFC, size);

            switch (channelNumber) {
                case 0: bus.DmaToMdecIn(dma); break;
                case 2: bus.DmaToGpu(dma); break;
                case 4: bus.DmaToSpu(dma); break;
                default: Console.WriteLine($"[DMA] [BLOCK COPY] Unsupported Channel (from Ram) {channelNumber}"); break;
            }

            baseAddress += memoryStep * size;
        }

    }

    private void linkedList() {
        uint header = 0;
        uint linkedListHardStop = 0xFFFF; //an arbitrary value to avoid infinity linked lists as we don't run the cpu in between blocks

        while ((header & 0x800000) == 0 && linkedListHardStop-- != 0) {
            header = bus.LoadFromRam(baseAddress);
            var size = header >> 24;
            //Console.WriteLine($"[DMA] [LinkedList] Header: {baseAddress:x8} size: {size}");

            if (size > 0) {
                baseAddress = baseAddress + 4 & 0x1ffffc;
                var load = bus.DmaFromRam(baseAddress, size);
                //Console.WriteLine($"[DMA] [LinkedList] DMAtoGPU size: {load.Length}");
                bus.DmaToGpu(load);
            }

            if (baseAddress == (header & 0x1ffffc)) break; //Tekken2 hangs here if not handling this posible forever loop
            baseAddress = header & 0x1ffffc;
        }

    }

    //0  Start immediately and transfer all at once (used for CDROM, OTC) needs TRIGGER
    private bool isActive() => syncMode == 0 ? enable && trigger : enable;

    public void transferBlockIfPending() {
        //TODO: check if device can actually transfer. Here we assume devices are always
        // capable of processing the dmas and never busy.
        if (pendingBlocks > 0) {
            pendingBlocks--;
            blockCopy(blockSize);

            if (pendingBlocks == 0) {
                finishDMA();
            }
        }
    }
}
