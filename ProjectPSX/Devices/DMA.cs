using System;
using static ProjectPSX.Width;

namespace ProjectPSX.Devices {
    public class DMA : Device {

        private DMA_Transfer dma_transfer;

        //private uint CONTROL { get { return load(WORD, 0x1F8010F0); } set { write(WORD, 0x1F8010F0, value); } }
        //private uint INTERRUPT { get { return load(WORD, 0x1F8010F4); } set { write(WORD, 0x1F8010F4, value); } }

        public DMA() {
            mem = new byte[0x80];
            memOffset = 0x1F801080;

            //CONTROL = 0x07654321;
            write(WORD, 0x1F8010F0, 0x07654321);
        }


        public new void write(Width w, uint addr, uint value) {
            base.write(w, addr, value);
            //Console.WriteLine("[DMA] Write: {0}  Value: {1}", addr.ToString("x8"), value.ToString("x8"));

            uint channel = (addr & 0x70) >> 4;
            uint register = addr & 0xF;

            switch (channel) {
                case uint CHANNELS when channel >= 0 && channel <= 6:
                    if (register == 8 && isActive(value)) {
                        Console.WriteLine("[DMA] [CHANNEL] " + channel + " " + addr.ToString("x8"));
                        handleDMA(addr, value);
                        disableChannel(addr, value);
                    }
                    break;

                case 7:
                    Console.WriteLine("[DMA] [CHANNEL] " + channel + " " + addr.ToString("x8"));
                    break;

                default:
                    Console.WriteLine("[DMA] [CHANNEL] WARNING! UNAVAILABLE CHANNEL" + channel);
                    break;
            }
        }

        private void disableChannel(uint addr, uint value) {
            uint disabled = (uint)(value & ~0x11000000);
            write(WORD, addr, disabled);
        }

        private void handleDMA(uint addr, uint control) {
            uint syncMode = (control >> 9) & 3;
            Console.WriteLine("[DMA] SyncMode: " + syncMode);
            switch (syncMode) {
                case 2:
                    linkedList(addr, control);
                    break;
                default:
                    blockCopy(syncMode, addr, control);
                    break;
            }

        }

        private void linkedList(uint addr, uint control) {
            uint header = 0;
            uint dmaAddress = getStartAdress(addr);

            while ((header & 0x800000) == 0) {
                header = dma_transfer.fromRAM(WORD, dmaAddress);
                //Console.WriteLine("HEADER addr " + dmaAddress.ToString("x8") + " value: " + header.ToString("x8"));
                uint size = header >> 24;

                while (size > 0) {
                    dmaAddress = (dmaAddress + 4) & 0x1ffffc;
                    uint load = dma_transfer.fromRAM(WORD, dmaAddress);
                    //Console.WriteLine("GPU SEND addr " + dmaAddress.ToString("x8") + " value: " + load.ToString("x8"));
                    dma_transfer.toGPU(load);
                    size--;
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

                        switch (channel) {

                            case 6: //OTC
                                if(size == 1) {
                                    data = 0xFF_FFFF;
                                } else {
                                    data = (dmaAddress - 4) & 0xFF_FFFF;
                                }
                                Console.WriteLine("[DMA] [C6 OTC] Address: {0} Data: {1}", (dmaAddress & 0x1F_FFFC).ToString("x8"), data.ToString("x8"));
                                break;
                            default:
                                data = 0;
                                Console.WriteLine("[DMA] [BLOCK COPY] Unsupported Channel (to Ram)" + channel);
                                break;
                        }
                        dma_transfer.toRAM(WORD, dmaAddress & 0x1F_FFFC, data);

                        break;
                    case 1: //From Ram
                        uint load = dma_transfer.fromRAM(WORD, dmaAddress);

                        switch (channel) {
                            case 2: //GPU
                                dma_transfer.toGPU(load);
                                break;
                            default: //MDECin and SPU
                                Console.WriteLine("[DMA] [BLOCK COPY] Unsupported Channel (from Ram)" + channel);
                                break;
                        }
                        break;
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

        public new uint load(Width w, uint addr) {
            Console.WriteLine("[DMA] Load: {0}  Value: {1}", addr.ToString("x8"), base.load(w, addr).ToString("x8"));
            return base.load(w, addr);
        }

        public void setDMA_Transfer(DMA_Transfer dma_transfer) {
            this.dma_transfer = dma_transfer;
        }

    }
}
