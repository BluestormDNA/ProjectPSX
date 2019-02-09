using ProjectPSX.Devices;
using System;
using System.IO;

namespace ProjectPSX {
    public class BUS : DMA_Transfer {

        //Memory
        private byte[] RAM = new byte[2048 * 1024];
        //private byte[] EX1 = new byte[512 * 1024];
        private byte[] SCRATHPAD = new byte[1024];
        //private byte[] REGISTERS = new byte[4 * 1024];
        private byte[] BIOS = new byte[512 * 1024];
        private byte[] IO = new byte[512];

        //Other Subsystems
        private DMA dma;
        private GPU gpu;

        public BUS() {
            dma = new DMA();
            gpu = new GPU();

            dma.setDMA_Transfer(this);
        }

        internal uint load(Width w, uint addr) {
            switch (addr) {
                case uint KUSEG when addr >= 0x0000_0000 && addr < 0x1F00_0000:
                case uint KSEG0 when addr >= 0x8000_0000 && addr < 0x9F00_0000:
                case uint KSEG1 when addr >= 0xA000_0000 && addr < 0xBF00_0000:
                    addr &= 0x1F_FFFF;
                    return load(w, addr, RAM);

                case uint KUSEG when addr >= 0x1F00_0000 && addr < 0x1F08_0000:
                case uint KSEG0 when addr >= 0x9F00_0000 && addr < 0x9F08_0000:
                case uint KSEG1 when addr >= 0xBF00_0000 && addr < 0xBF08_0000:
                    return 0xFFFF_FFFF;

                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                case uint KSEG0 when addr >= 0x9F80_0000 && addr < 0x9F80_0400:
                case uint KSEG1 when addr >= 0xBF80_0000 && addr < 0xBF80_0400:
                    addr &= 0xFFF;
                    return load(w, addr, SCRATHPAD);

                case uint KUSEG when addr >= 0x1F80_1000 && addr < 0x1F80_2000:
                case uint KSEG0 when addr >= 0x9F80_1000 && addr < 0x9F80_2000:
                case uint KSEG1 when addr >= 0xBF80_1000 && addr < 0xBF80_2000:

                    switch (addr) {
                        case uint DMA when addr >= 0x1F80_1080 && addr <= 0x1F80_10FF:
                            return dma.load(w, addr);
                        case 0x1F801810:
                            return gpu.loadGPUREAD();
                        case 0x1F801814:
                            return gpu.loadGPUSTAT();

                        default:
                            addr &= 0xFFF;
                            //return load(w, addr, REGISTERS);
                            return 0;
                    }

                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                case uint KSEG0 when addr >= 0x9FC0_0000 && addr < 0x9FC8_0000:
                case uint KSEG1 when addr >= 0xBFC0_0000 && addr < 0xBFC8_0000:
                    addr &= 0x7_FFFF;
                    return load(w, addr, BIOS);

                case uint KSEG2 when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    addr &= 0x1FF;
                    return load(w, addr, IO);

                default:
                    Console.WriteLine("[BUS] Load Unsupported: " + addr.ToString("x4"));
                    return 0xFFFF_FFFF;
            }
        }

        internal void write(Width w, uint addr, uint value) {
            switch (addr) {
                case uint KUSEG when addr >= 0x0000_0000 && addr < 0x1F00_0000:
                case uint KSEG0 when addr >= 0x8000_0000 && addr < 0x9F00_0000:
                case uint KSEG1 when addr >= 0xA000_0000 && addr < 0xBF00_0000:
                    addr &= 0x1F_FFFF;
                    write(w, addr, value, RAM);
                    break;

                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                case uint KSEG0 when addr >= 0x9F80_0000 && addr < 0x9F80_0400:
                case uint KSEG1 when addr >= 0xBF80_0000 && addr < 0xBF80_0400:
                    addr &= 0xFFF;
                    write(w, addr, value, SCRATHPAD);
                    break;

                case uint KUSEG when addr >= 0x1F80_1000 && addr < 0x1F80_2000:
                case uint KSEG0 when addr >= 0x9F80_1000 && addr < 0x9F80_2000:
                case uint KSEG1 when addr >= 0xBF80_1000 && addr < 0xBF80_2000:

                    switch (addr) {
                        case uint DMA when addr >= 0x1F80_1080 && addr <= 0x1F80_10FF:
                            dma.write(w, addr, value);
                            break;
                        case 0x1F801810:
                            gpu.writeGP0(value);
                            break;
                        case 0x1F801814:
                            gpu.writeGP1(value);
                            break;

                        default:
                            addr &= 0xFFF;
                            //write(w, addr, value, REGISTERS);
                            break;
                    }
                    break;

                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                case uint KSEG0 when addr >= 0x9FC0_0000 && addr < 0x9FC8_0000:
                case uint KSEG1 when addr >= 0xBFC0_0000 && addr < 0xBFC8_0000: //BIOS mem map
                    Console.WriteLine("[BUS] [WARNING] Write on BIOS range" + addr.ToString("x8"));
                    Console.ReadLine();
                    break;

                case uint KSEG2 when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    addr &= 0x1FF;
                    write(w, addr, value, IO);
                    break;

                default:
                    Console.WriteLine("[BUS] Write Unsupported: " + addr.ToString("x4") + ": " + value.ToString("x4"));
                    break;
            }
        }

        internal uint load(Width w, uint addr, byte[] memory) {
            uint load = 0;
            for (int i = 0; i < (byte)w; i++) {
                load |= (uint)(memory[addr + i] << (8 * i));
            }
            return load;
        }

        internal void write(Width w, uint addr, uint value, byte[] memory) {
            for (int i = 0; i < (byte)w; i++) {
                memory[addr + i] = (byte)(value >> (8 * i));
            }
        }

        internal void loadBios() {
            byte[] rom = File.ReadAllBytes("./SCPH1001.BIN");
            Array.Copy(rom, 0, BIOS, 0, rom.Length);
        }

        void DMA_Transfer.toGPU(uint value) {
            gpu.writeGP0(value);
        }

        uint DMA_Transfer.fromRAM(Width w, uint addr) {
            return load(w, addr, RAM);
        }

        void DMA_Transfer.toRAM(Width w, uint addr, uint value) {
            write(w, addr, value, RAM);
        }
    }
}