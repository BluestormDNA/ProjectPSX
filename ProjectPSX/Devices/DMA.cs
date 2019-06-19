using System;
using static ProjectPSX.Width;

namespace ProjectPSX.Devices {
    //TODO
    //This class was one of the first to be write and needs to be rewrited to not use "Device" and to address all those load and writes
    //and use variables for the channels maybe something like the timers a Channel inner class...
    //Also I think CD reads are overkill word per word so DMA chunks should be handled as a whole but this causes issues on the cd itseld
    //as it can read random values can be handled with a little pointer variable like the GPU transfers (doable but low priority)
    public class DMA : Device {

        private DMA_Transfer dma_transfer;

        bool edgeInterruptTrigger;

        //private uint CONTROL { get { return load(WORD, 0x1F8010F0); } set { write(WORD, 0x1F8010F0, value); } }
        //private uint INTERRUPT { get { return load(WORD, 0x1F8010F4); } set { write(WORD, 0x1F8010F4, value); } }

        public DMA() {
            mem = new byte[0x80];
            memOffset = 0x1F801080;

            //CONTROL = 0x07654321;
            write(0x1F8010F0, 0x07654321);
        }


        public void write(uint addr, uint value) {
            if (addr == 0x1F8010F4) {
                //Console.WriteLine("Write to interrupt handler " + addr.ToString("x8") + " " + value.ToString("x8"));
                value &= 0x7FFF_FFFF;
                uint irqFlag = (value >> 24) & 0x7F;
                value = (uint)(value & ~0x7F008000); //disable force irq investigate this

                //Console.WriteLine(irqFlag);
                irqFlag ^= irqFlag;
                //Console.WriteLine(irqFlag);
                value |= irqFlag << 24;
                //Console.WriteLine("Write to interrupt handler 2" + addr.ToString("x8") + " " + value.ToString("x8"));
            }
            base.write32(addr, value);


            //Console.WriteLine("[DMA] Write: {0}  Value: {1}", addr.ToString("x8"), value.ToString("x8"));

            uint channel = (addr & 0x70) >> 4;
            uint register = addr & 0xF;

            switch (channel) {
                case uint channels when channel <= 6:
                    if (register == 8 && isActive(value)) {
                        //Console.WriteLine("[DMA] [CHANNEL] " + channel + " " + addr.ToString("x8"));
                        handleDMA(addr, value);
                        disable(addr, value);
                        handleInterrupt((int)channel);
                    }
                    break;

                case 7:
                    switch (register) {
                        case 0:
                            //Console.WriteLine("[DMA] [DPCR - Control Register] " + channel + " " + addr.ToString("x8") + " " + value.ToString("x8"));
                            //TODO
                            break;
                        case 4:
                            //Console.WriteLine("[DMA] [DICR - Interrupt Register] " + channel + " " + addr.ToString("x8") + " " + value.ToString("x8"));
                            //TODO
                            break;
                    }

                    break;

                default:
                    //Console.WriteLine("[DMA] [CHANNEL] WARNING! UNAVAILABLE CHANNEL" + channel);
                    break;
            }
        }

        public bool tick() {
            if (edgeInterruptTrigger) {
                edgeInterruptTrigger = false;
                //Console.WriteLine("[IRQ] Triggering DMA");
                return true;
            }
            return false;
        }

        private void handleInterrupt(int channel) {
            //IRQ flags in Bit(24 + n) are set upon DMAn completion - but caution - they are set ONLY if enabled in Bit(16 + n).
            //Bit31 is a simple readonly flag that follows the following rules:
            //IF b15 = 1 OR(b23 = 1 AND(b16 - 22 AND b24 - 30) > 0) THEN b31 = 1 ELSE b31 = 0

            uint interruptRegister = load(WORD, 0x1F8010F4);

            bool forceIRQ = (interruptRegister >> 15) != 0;
            uint irqEnable = (interruptRegister >> 16) & 0x7F;
            bool masterEnable = (interruptRegister >> 23) != 0;
            uint irqFlag = (interruptRegister >> 24) & 0x7F;
            bool masterFlag = (interruptRegister >> 31) != 0;

            if ((irqEnable & (1 << channel)) != 0) {
                irqFlag |= (uint)(1 << channel);
            } else {
                irqFlag &= (uint)~(1 << channel);
            }

            masterFlag = /*forceIRQ ||*/ (masterEnable && ((irqEnable & irqFlag) > 0));
            edgeInterruptTrigger = masterFlag;
            //Console.WriteLine("MasterFlag" + masterFlag + " irqEnable16" + irqEnable.ToString("x8") + " irqFlag24" + irqFlag.ToString("x8") + masterEnable + ((irqEnable & irqFlag) > 0));

            interruptRegister = 0;
            interruptRegister |= (forceIRQ ? 1u : 0) << 15;
            interruptRegister |= irqEnable << 16;
            interruptRegister |= (masterEnable ? 1u : 0) << 23;
            interruptRegister |= irqFlag << 24;
            interruptRegister |= (masterFlag ? 1u : 0) << 31;

            base.write(WORD, 0x1F8010F4, interruptRegister);
        }

        private void disable(uint addr, uint value) {
            uint disabled = (uint)(value & ~0x11000000);
            write32(addr, disabled);
        }

        private void handleDMA(uint addr, uint control) {
            uint syncMode = (control >> 9) & 3;
            //Console.WriteLine("[DMA] SyncMode: " + syncMode);
            switch (syncMode) {
                case 2:
                    linkedList(addr);
                    break;
                default:
                    blockCopy(syncMode, addr, control);
                    break;
            }
        }

        private void linkedList(uint addr) {
            uint header = 0;
            uint dmaAddress = getStartAdress(addr);

            while ((header & 0x800000) == 0) {
                header = dma_transfer.fromRAM(dmaAddress);
                //Console.WriteLine("HEADER addr " + dmaAddress.ToString("x8") + " value: " + header.ToString("x8"));
                uint size = header >> 24;

                if (size > 0) {
                    dmaAddress = (dmaAddress + 4) & 0x1ffffc;
                    //uint load = dma_transfer.fromRAM(dmaAddress);
                    // Console.WriteLine("GPU SEND addr " + dmaAddress.ToString("x8") + " value: " + load.ToString("x8"));
                    //dma_transfer.toGPU(load);
                    uint[] bufferTest = dma_transfer.fromRAM(dmaAddress, size);
                    dma_transfer.toGPU(bufferTest);
                }
                dmaAddress = header & 0x1ffffc;
            }

        }

        private void blockCopy(uint syncMode, uint addr, uint control) {
            uint dmaAddress = getStartAdress(addr);
            uint size = getSize(syncMode, addr);

            uint direction = control & 1;
            int step = ((control >> 1) & 1) == 0 ? 4 : -4;
            uint channel = (addr & 0x70) >> 4;

            while (size > 0) {
                switch (direction) {
                    case 0: //To Ram
                        uint data = 0;
                        //byte[] cdTest = null;

                        switch (channel) {

                            case 2: //GPU
                                data = dma_transfer.fromGPU();
                                //Console.WriteLine("[DMA] [C2 GPU] Address: {0} Data: {1} Size {2}", (dmaAddress & 0x1F_FFFC).ToString("x8"), data.ToString("x8"), size);
                                break;
                            case 3: //CD
                                data = dma_transfer.fromCD();
                                //if(step == -4) {
                                //    Console.WriteLine("WARNING !!! UNHANDLED REVERSE ON BUFFER CD TRANSFER");
                                //    Console.ReadLine();
                                //}
                                //cdTest = dma_transfer.fromCD(size);
                                //for (int i = 0; i < cdTest.Length; i++) {
                                //    Console.WriteLine(cdTest[i].ToString("x2"));
                                //}
                                //dma_transfer.toRAM(dmaAddress & 0x1F_FFFC, cdTest, size);
                                //return;
                                 //Console.WriteLine("[DMA] [C3 CD] TORAM Address: {0} Data: {1} Size {2}", (dmaAddress & 0x1F_FFFC).ToString("x8"), data.ToString("x8"), size);
                                break;
                            case 6: //OTC
                                if (size == 1) {
                                    data = 0xFF_FFFF;
                                } else {
                                    data = (dmaAddress - 4) & 0xFF_FFFF;
                                }
                                //Console.WriteLine("[DMA] [C6 OTC] Address: {0} Data: {1}", (dmaAddress & 0x1F_FFFC).ToString("x8"), data.ToString("x8"));
                                break;
                            default:
                                data = 0;
                                //Console.WriteLine("[DMA] [BLOCK COPY] Unsupported Channel (to Ram) " + channel);
                                break;
                        }
                        dma_transfer.toRAM(dmaAddress & 0x1F_FFFC, data);

                        break;
                    case 1: //From Ram
                        //Console.WriteLine("Size " + size);
                        uint[] load = dma_transfer.fromRAM(dmaAddress & 0x1F_FFFC, size);

                        switch (channel) {
                            case 2: //GPU
                                dma_transfer.toGPU(load);
                                return;
                            default: //MDECin and SPU
                                Console.WriteLine("[DMA] [BLOCK COPY] Unsupported Channel (from Ram) " + channel);
                                return;
                        }
                }

                dmaAddress += (uint)step;
                size--;
            }


        }

        private uint getSize(uint syncMode, uint addr) {
            uint channelBlockControlAddr = (uint)((addr & ~0xF) | 0x4);
            uint channelBlockControlRegister = base.load(WORD, channelBlockControlAddr);
            uint blockSize = (ushort)channelBlockControlRegister;
            uint blockCount = (ushort)(channelBlockControlRegister >> 16);

            switch (syncMode) {
                case 0:
                    return blockSize;
                case 1:
                    return blockSize * blockCount;
                default:
                    Console.WriteLine("[DMA] [BLOCK COPY] Unsupported Sync Mode " + syncMode);
                    return 0xFFFF_FFFF;
            }
        }

        private uint getStartAdress(uint addr) {
            uint channelBaseAddr = (uint)(addr & ~0xF);
            uint channelBaseRegister = base.load(WORD, channelBaseAddr);
            uint startAdress = channelBaseRegister & 0xFFFFFF;

            return startAdress;
        }

        private bool isActive(uint control) {
            bool enable = ((control >> 24) & 1) != 0;
            bool trigger = ((control >> 28) & 1) != 0;
            uint syncMode = (control >> 9) & 3;

            if (syncMode == 0) { //0  Start immediately and transfer all at once (used for CDROM, OTC) needs TRIGGER
                return enable && trigger;
            } else {
                return enable;
            }
        }

        public uint load(uint addr) {
            //Console.WriteLine("[DMA] Load: {0}  Value: {1}", addr.ToString("x8"), base.load(w, addr).ToString("x8"));
            return base.load(Width.WORD, addr);
        }

        public void setDMA_Transfer(DMA_Transfer dma_transfer) {
            this.dma_transfer = dma_transfer;
        }

    }
}
