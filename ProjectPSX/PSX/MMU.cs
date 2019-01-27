using System;
using System.IO;

namespace ProjectPSX {
    internal class MMU {

        private byte[] RAM = new byte[2048 * 1024];
        private byte[] EX1 = new byte[512 * 1024];
        private byte[] SCRATHPAD = new byte[1024];
        private byte[] REGISTERS = new byte[4 * 1024];
        private byte[] BIOS = new byte[512 * 1024];
        private byte[] IO = new byte[512];

        private const uint GPUSTAT = 0x1f801814;
        private const uint GP0 = 0x1f801810;

        internal uint read32(uint addr) {
            //Console.WriteLine("READ ADDR: " + addr.ToString("x4"));
            switch (addr) {
                case uint KUSEG when addr >= 0x0000_0000 && addr < 0x1F00_0000:
                case uint KSEG0 when addr >= 0x8000_0000 && addr < 0x9F00_0000:
                case uint KSEG1 when addr >= 0xA000_0000 && addr < 0xBF00_0000:
                    addr &= 0x1F_FFFF;
                    return (uint)((RAM[addr + 3] << 24) | (RAM[addr + 2] << 16) | (RAM[addr + 1] << 8) | RAM[addr]);

                case uint KUSEG when addr >= 0x1F00_0000 && addr < 0x1F08_0000:
                case uint KSEG0 when addr >= 0x9F00_0000 && addr < 0x9F08_0000:
                case uint KSEG1 when addr >= 0xBF00_0000 && addr < 0xBF08_0000:
                    if (addr == 0x1f00_0084) { Console.WriteLine("EX1 IO PORT READ RETURNING FF"); return 0xFFFF_FFFF; } //todo look if this is needed EX1 IO port
                    addr &= 0x7_FFFF;
                    return (uint)((EX1[addr + 3] << 24) | (EX1[addr + 2] << 16) | (EX1[addr + 1] << 8) | EX1[addr]);

                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                case uint KSEG0 when addr >= 0x9F80_0000 && addr < 0x9F80_0400:
                case uint KSEG1 when addr >= 0xBF80_0000 && addr < 0xBF80_0400:
                    addr &= 0xFFF;
                    return (uint)((SCRATHPAD[addr + 3] << 24) | (SCRATHPAD[addr + 2] << 16) | (SCRATHPAD[addr + 1] << 8) | SCRATHPAD[addr]);

                case uint KUSEG when addr >= 0x1F80_1000 && addr < 0x1F80_2000:
                case uint KSEG0 when addr >= 0x9F80_1000 && addr < 0x9F80_2000:
                case uint KSEG1 when addr >= 0xBF80_1000 && addr < 0xBF80_2000:
                    if (addr >= 0x1f801080 && addr <= 0x1f8010FF) {
                        Console.WriteLine("DMA ACCESS");
                        return 0;
                    } else if (addr == GPUSTAT) { Console.WriteLine("GPUSTAT ACCESS"); return 0x1000_0000; }
                    else if (addr == GP0) { Console.WriteLine("GP0 ACCESS"); return 0; } 
                    else if (addr == 0x1f801104) { Console.WriteLine("Timer 1104 ACCESS"); return 0; } 
                    else if (addr == 0x1f801114) { Console.WriteLine("Timer 1114 ACCESS"); return 0; }
                    else if (addr == 0x1f801118) { Console.WriteLine("Timer 1118 ACCESS"); return 0; } 
                    else if (addr == 0x1f801070) { Console.WriteLine("Interrupt Mask ACCESS"); return 0; } 
                    else if (addr == 0x1f801074) { Console.WriteLine("Interrupt Status ACCESS"); return 0; }
                    else if (addr == 0x1f801dae) { Console.WriteLine("SPU ACCESS"); return 0; }

                    addr &= 0xFFF;
                    return (uint)((REGISTERS[addr + 3] << 24) | (REGISTERS[addr + 2] << 16) | (REGISTERS[addr + 1] << 8) | REGISTERS[addr]);

                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                case uint KSEG0 when addr >= 0x9FC0_0000 && addr < 0x9FC8_0000:
                case uint KSEG1 when addr >= 0xBFC0_0000 && addr < 0xBFC8_0000: //BIOS mem map
                    if (0xbfc04190 == addr) {
                        Console.WriteLine("DMA LOOP");
                        return 0;
                    }

                    addr &= 0x7_FFFF;
                    return (uint)((BIOS[addr + 3] << 24) | (BIOS[addr + 2] << 16) | (BIOS[addr + 1] << 8) | BIOS[addr]);

                case uint KSEG2 when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    addr &= 0x1FF;
                    return (uint)((IO[addr + 3] << 24) | (IO[addr + 2] << 16) | (IO[addr + 1] << 8) | IO[addr]);

                default:
                    Console.WriteLine("Unsupported READ AREA: " + addr.ToString("x4"));
                    return 0xFFFF_FFFF;
            }
        }

        internal void write32(uint addr, uint value) {
            switch (addr) {
                case uint KUSEG when addr >= 0x0000_0000 && addr < 0x1F00_0000:
                case uint KSEG0 when addr >= 0x8000_0000 && addr < 0x9F00_0000:
                case uint KSEG1 when addr >= 0xA000_0000 && addr < 0xBF00_0000:
                    addr &= 0x1F_FFFF;
                    RAM[addr] = (byte)(value);
                    RAM[addr + 1] = (byte)(value >> 8);
                    RAM[addr + 2] = (byte)(value >> 16);
                    RAM[addr + 3] = (byte)(value >> 24);
                    break;
                case uint KUSEG when addr >= 0x1F00_0000 && addr < 0x1F08_0000:
                case uint KSEG0 when addr >= 0x9F00_0000 && addr < 0x9F08_0000:
                case uint KSEG1 when addr >= 0xBF00_0000 && addr < 0xBF08_0000:
                    addr &= 0x7_FFFF;
                    EX1[addr] = (byte)(value);
                    EX1[addr + 1] = (byte)(value >> 8);
                    EX1[addr + 2] = (byte)(value >> 16);
                    EX1[addr + 3] = (byte)(value >> 24);
                    break;
                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                case uint KSEG0 when addr >= 0x9F80_0000 && addr < 0x9F80_0400:
                case uint KSEG1 when addr >= 0xBF80_0000 && addr < 0xBF80_0400:
                    addr &= 0xFFF;
                    SCRATHPAD[addr] = (byte)(value);
                    SCRATHPAD[addr + 1] = (byte)(value >> 8);
                    SCRATHPAD[addr + 2] = (byte)(value >> 16);
                    SCRATHPAD[addr + 3] = (byte)(value >> 24);
                    break;
                case uint KUSEG when addr >= 0x1F80_1000 && addr < 0x1F80_2000:
                case uint KSEG0 when addr >= 0x9F80_1000 && addr < 0x9F80_2000:
                case uint KSEG1 when addr >= 0xBF80_1000 && addr < 0xBF80_2000:
                    addr &= 0xFFF;
                    REGISTERS[addr] = (byte)(value);
                    REGISTERS[addr + 1] = (byte)(value >> 8);
                    REGISTERS[addr + 2] = (byte)(value >> 16);
                    REGISTERS[addr + 3] = (byte)(value >> 24);
                    break;
                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                case uint KSEG0 when addr >= 0x9FC0_0000 && addr < 0x9FC8_0000:
                case uint KSEG1 when addr >= 0xBFC0_0000 && addr < 0xBFC8_0000: //BIOS mem map
                    Console.WriteLine("WARNING WRITE 32 on BIOS RANGE" + addr.ToString("x8"));
                    Console.ReadLine();
                    break;
                case uint KSEG2 when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    addr &= 0x1FF;
                    IO[addr] = (byte)(value);
                    IO[addr + 1] = (byte)(value >> 8);
                    IO[addr + 2] = (byte)(value >> 16);
                    IO[addr + 3] = (byte)(value >> 24);
                    break;
                default:
                    Console.WriteLine("Unsupported WRITE AREA: " + addr.ToString("x4") + ": " + value.ToString("x4"));
                    break;
            }
        }

        internal void write8(uint addr, byte value) {
            switch (addr) {
                case uint KUSEG when addr >= 0x0000_0000 && addr < 0x1F00_0000:
                case uint KSEG0 when addr >= 0x8000_0000 && addr < 0x9F00_0000:
                case uint KSEG1 when addr >= 0xA000_0000 && addr < 0xBF00_0000:
                    addr &= 0x1F_FFFF;
                    RAM[addr] = (byte)(value);
                    break;
                case uint KUSEG when addr >= 0x1F00_0000 && addr < 0x1F08_0000:
                case uint KSEG0 when addr >= 0x9F00_0000 && addr < 0x9F08_0000:
                case uint KSEG1 when addr >= 0xBF00_0000 && addr < 0xBF08_0000:
                    addr &= 0x7_FFFF;
                    EX1[addr] = (byte)(value);
                    break;
                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                case uint KSEG0 when addr >= 0x9F80_0000 && addr < 0x9F80_0400:
                case uint KSEG1 when addr >= 0xBF80_0000 && addr < 0xBF80_0400:
                    addr &= 0xFFF;
                    SCRATHPAD[addr] = (byte)(value);
                    break;
                case uint KUSEG when addr >= 0x1F80_1000 && addr < 0x1F80_2000:
                case uint KSEG0 when addr >= 0x9F80_1000 && addr < 0x9F80_2000:
                case uint KSEG1 when addr >= 0xBF80_1000 && addr < 0xBF80_2000:
                    addr &= 0xFFF;
                    REGISTERS[addr] = (byte)(value);
                    break;
                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                case uint KSEG0 when addr >= 0x9FC0_0000 && addr < 0x9FC8_0000:
                case uint KSEG1 when addr >= 0xBFC0_0000 && addr < 0xBFC8_0000: //BIOS mem map
                    Console.WriteLine("WARNING WRITE 32 on BIOS RANGE" + addr.ToString("x8"));
                    Console.ReadLine();
                    break;
                case uint KSEG2 when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    addr &= 0x1FF;
                    IO[addr] = (byte)(value);
                    break;
                default:
                    Console.WriteLine("Unsupported WRITE AREA: " + addr.ToString("x4") + ": " + value.ToString("x4"));
                    break;
            }
        }

        internal void write16(uint addr, ushort value) {
            switch (addr) {
                case uint KUSEG when addr >= 0x0000_0000 && addr < 0x1F00_0000:
                case uint KSEG0 when addr >= 0x8000_0000 && addr < 0x9F00_0000:
                case uint KSEG1 when addr >= 0xA000_0000 && addr < 0xBF00_0000:
                    addr &= 0x1F_FFFF;
                    RAM[addr] = (byte)(value);
                    RAM[addr + 1] = (byte)(value >> 8);
                    break;
                case uint KUSEG when addr >= 0x1F00_0000 && addr < 0x1F08_0000:
                case uint KSEG0 when addr >= 0x9F00_0000 && addr < 0x9F08_0000:
                case uint KSEG1 when addr >= 0xBF00_0000 && addr < 0xBF08_0000:
                    addr &= 0x7_FFFF;
                    EX1[addr] = (byte)(value);
                    EX1[addr + 1] = (byte)(value >> 8);
                    break;
                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                case uint KSEG0 when addr >= 0x9F80_0000 && addr < 0x9F80_0400:
                case uint KSEG1 when addr >= 0xBF80_0000 && addr < 0xBF80_0400:
                    addr &= 0xFFF;
                    SCRATHPAD[addr] = (byte)(value);
                    SCRATHPAD[addr + 1] = (byte)(value >> 8);
                    break;
                case uint KUSEG when addr >= 0x1F80_1000 && addr < 0x1F80_2000:
                case uint KSEG0 when addr >= 0x9F80_1000 && addr < 0x9F80_2000:
                case uint KSEG1 when addr >= 0xBF80_1000 && addr < 0xBF80_2000:
                    addr &= 0xFFF;
                    REGISTERS[addr] = (byte)(value);
                    REGISTERS[addr + 1] = (byte)(value >> 8);
                    break;
                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                case uint KSEG0 when addr >= 0x9FC0_0000 && addr < 0x9FC8_0000:
                case uint KSEG1 when addr >= 0xBFC0_0000 && addr < 0xBFC8_0000:
                    Console.WriteLine("WARNING WRITE 32 on BIOS RANGE" + addr.ToString("x8"));
                    Console.ReadLine();
                    break;
                case uint KSEG2 when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    addr &= 0x1FF;
                    IO[addr] = (byte)(value);
                    IO[addr + 1] = (byte)(value >> 8);
                    break;
                default:
                    Console.WriteLine("Unsupported WRITE AREA: " + addr.ToString("x4") + ": " + value.ToString("x4"));
                    break;
            }
        }

        internal void loadBios() {
            byte[] rom = File.ReadAllBytes("./SCPH1001.BIN");
            Array.Copy(rom, 0, BIOS, 0, rom.Length);
        }
    }
}