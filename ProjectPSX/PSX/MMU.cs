using System;
using System.IO;

namespace ProjectPSX {
    internal class MMU {

        private byte[] RAM = new byte[2048 * 1024];
        private byte[] EX1 = new byte[8192 * 1024];
        private byte[] SCRATHPAD = new byte[1024];
        private byte[] REGISTERS = new byte[8 * 1024];
        private byte[] BIOS = new byte[512 * 1024];
        private byte[] IO = new byte[512];

        internal uint read32(uint addr) {
            if (addr % 4 != 0) {
                Console.WriteLine("UNALIGNED READ");
            }
            //Console.WriteLine("READ ADDR: " + addr.ToString("x4"));
            switch (addr) {
                case uint KUSEG when addr >= 0x0000_0000 && addr < 0x1F00_0000:
                case uint KSEG0 when addr >= 0x8000_0000 && addr < 0x9F00_0000:
                case uint KSEG1 when addr >= 0xA000_0000 && addr < 0xBF00_0000:
                    addr &= 0x1F_FFFF;
                    return (uint)((RAM[addr + 3] << 24) | (RAM[addr + 2] << 16) | (RAM[addr + 1] << 8) | RAM[addr]);
                case uint KUSEG when addr >= 0x1F00_0000 && addr < 0x1F80_0000:
                case uint KSEG0 when addr >= 0x9F00_0000 && addr < 0x9F80_0000:
                case uint KSEG1 when addr >= 0xBF00_0000 && addr < 0xBF80_0000:
                    if (addr == 0x1f000084) return 0xFFFF_FFFF; //todo look if this is needed EX1 IO port
                    addr &= 0x7F_FFFF;
                    return (uint)((EX1[addr + 3] << 24) | (EX1[addr + 2] << 16) | (EX1[addr + 1] << 8) | EX1[addr]);
                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400: //Warning: _1000
                case uint KSEG0 when addr >= 0x9F80_0000 && addr < 0x9F80_0400: //Warning: _1000
                case uint KSEG1 when addr >= 0xBF80_0000 && addr < 0xBF80_0400: //Warning: _1000
                    addr &= 0xFFF;
                    return (uint)((SCRATHPAD[addr + 3] << 24) | (SCRATHPAD[addr + 2] << 16) | (SCRATHPAD[addr + 1] << 8) | SCRATHPAD[addr]);
                case uint KUSEG when addr >= 0x1F80_1000 && addr < 0x1F80_3000:
                case uint KSEG0 when addr >= 0x9F80_1000 && addr < 0x9F80_3000:
                case uint KSEG1 when addr >= 0xBF80_1000 && addr < 0xBF80_3000:
                    addr &= 0x1FFF;
                    return (uint)((REGISTERS[addr + 3] << 24) | (REGISTERS[addr + 2] << 16) | (REGISTERS[addr + 1] << 8) | REGISTERS[addr]);
                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                case uint KSEG0 when addr >= 0x9FC0_0000 && addr < 0x9FC8_0000:
                case uint KSEG1 when addr >= 0xBFC0_0000 && addr < 0xBFC8_0000: //BIOS mem map
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
            if (addr % 4 != 0) {
                Console.WriteLine("UNALIGNED WRITE");
            }

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
                case uint KUSEG when addr >= 0x1F00_0000 && addr < 0x1F80_0000:
                case uint KSEG0 when addr >= 0x9F00_0000 && addr < 0x9F80_0000:
                case uint KSEG1 when addr >= 0xBF00_0000 && addr < 0xBF80_0000:
                    addr &= 0x7F_FFFF;
                    EX1[addr] = (byte)(value);
                    EX1[addr + 1] = (byte)(value >> 8);
                    EX1[addr + 2] = (byte)(value >> 16);
                    EX1[addr + 3] = (byte)(value >> 24);
                    break;
                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400: //Warning: _1000
                case uint KSEG0 when addr >= 0x9F80_0000 && addr < 0x9F80_0400: //Warning: _1000
                case uint KSEG1 when addr >= 0xBF80_0000 && addr < 0xBF80_0400: //Warning: _1000
                    addr &= 0xFFF;
                    SCRATHPAD[addr] = (byte)(value);
                    SCRATHPAD[addr + 1] = (byte)(value >> 8);
                    SCRATHPAD[addr + 2] = (byte)(value >> 16);
                    SCRATHPAD[addr + 3] = (byte)(value >> 24);
                    break;
                case uint KUSEG when addr >= 0x1F80_1000 && addr < 0x1F80_3000:
                case uint KSEG0 when addr >= 0x9F80_1000 && addr < 0x9F80_3000:
                case uint KSEG1 when addr >= 0xBF80_1000 && addr < 0xBF80_3000:
                    addr &= 0x1FFF;
                    REGISTERS[addr] = (byte)(value);
                    REGISTERS[addr + 1] = (byte)(value >> 8);
                    REGISTERS[addr + 2] = (byte)(value >> 16);
                    REGISTERS[addr + 3] = (byte)(value >> 24);
                    break;
                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                case uint KSEG0 when addr >= 0x9FC0_0000 && addr < 0x9FC8_0000:
                case uint KSEG1 when addr >= 0xBFC0_0000 && addr < 0xBFC8_0000: //BIOS mem map
                    addr &= 0x7_FFFF;
                    BIOS[addr] = (byte)(value);
                    BIOS[addr + 1] = (byte)(value >> 8);
                    BIOS[addr + 2] = (byte)(value >> 16);
                    BIOS[addr + 3] = (byte)(value >> 24);
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
            /*
            switch (addr) {
                case uint r when addr == 0x1f80_1000: //BIOS mem map
                    if (value != 0x1f00_0000) Console.WriteLine("WARNING NON VALID MEM MAP");
                    // array = tal
                    break;
                case uint r when addr == 0x1f80_1004: //BIOS mem map
                    if (value != 0x1f80_2000) Console.WriteLine("WARNING NON VALID MEM MAP");
                    // array = tal
                    break;
                default:
                    Console.WriteLine("Unsupported WRITE AREA: " + addr.ToString("x4") + ": " + value.ToString("x4"));
                    break;
            }
            */
        }

        internal void write8(uint addr, byte value) {
            if (addr % 1 != 0) {
                Console.WriteLine("UNALIGNED WRITE");
            }
            //Console.WriteLine("Write Addres Byte: " + addr.ToString("x4"));

            switch (addr) {
                case uint KUSEG when addr >= 0x0000_0000 && addr < 0x1F00_0000:
                case uint KSEG0 when addr >= 0x8000_0000 && addr < 0x9F00_0000:
                case uint KSEG1 when addr >= 0xA000_0000 && addr < 0xBF00_0000:
                    addr &= 0x1F_FFFF;
                    RAM[addr] = (byte)(value);
                    break;
                case uint KUSEG when addr >= 0x1F00_0000 && addr < 0x1F80_0000:
                case uint KSEG0 when addr >= 0x9F00_0000 && addr < 0x9F80_0000:
                case uint KSEG1 when addr >= 0xBF00_0000 && addr < 0xBF80_0000:
                    addr &= 0x7F_FFFF;
                    EX1[addr] = (byte)(value);
                    break;
                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400: //Warning: _1000
                case uint KSEG0 when addr >= 0x9F80_0000 && addr < 0x9F80_0400: //Warning: _1000
                case uint KSEG1 when addr >= 0xBF80_0000 && addr < 0xBF80_0400: //Warning: _1000
                    addr &= 0xFFF;
                    SCRATHPAD[addr] = (byte)(value);
                    break;
                case uint KUSEG when addr >= 0x1F80_1000 && addr < 0x1F80_3000:
                case uint KSEG0 when addr >= 0x9F80_1000 && addr < 0x9F80_3000:
                case uint KSEG1 when addr >= 0xBF80_1000 && addr < 0xBF80_3000:
                    addr &= 0x1FFF;
                    REGISTERS[addr] = (byte)(value);
                    break;
                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                case uint KSEG0 when addr >= 0x9FC0_0000 && addr < 0x9FC8_0000:
                case uint KSEG1 when addr >= 0xBFC0_0000 && addr < 0xBFC8_0000: //BIOS mem map
                    addr &= 0x7_FFFF;
                    BIOS[addr] = (byte)(value);
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
            if (addr % 2 != 0) {
                Console.WriteLine("UNALIGNED WRITE");
            }

            switch (addr) {
                case uint KUSEG when addr >= 0x0000_0000 && addr < 0x1F00_0000:
                case uint KSEG0 when addr >= 0x8000_0000 && addr < 0x9F00_0000:
                case uint KSEG1 when addr >= 0xA000_0000 && addr < 0xBF00_0000:
                    addr &= 0x1F_FFFF;
                    RAM[addr] = (byte)(value);
                    RAM[addr + 1] = (byte)(value >> 8);
                    break;
                case uint KUSEG when addr >= 0x1F00_0000 && addr < 0x1F80_0000:
                case uint KSEG0 when addr >= 0x9F00_0000 && addr < 0x9F80_0000:
                case uint KSEG1 when addr >= 0xBF00_0000 && addr < 0xBF80_0000:
                    addr &= 0x7F_FFFF;
                    EX1[addr] = (byte)(value);
                    EX1[addr + 1] = (byte)(value >> 8);
                    break;
                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400: //Warning: _1000
                case uint KSEG0 when addr >= 0x9F80_0000 && addr < 0x9F80_0400: //Warning: _1000
                case uint KSEG1 when addr >= 0xBF80_0000 && addr < 0xBF80_0400: //Warning: _1000
                    addr &= 0xFFF;
                    SCRATHPAD[addr] = (byte)(value);
                    SCRATHPAD[addr + 1] = (byte)(value >> 8);
                    break;
                case uint KUSEG when addr >= 0x1F80_1000 && addr < 0x1F80_3000:
                case uint KSEG0 when addr >= 0x9F80_1000 && addr < 0x9F80_3000:
                case uint KSEG1 when addr >= 0xBF80_1000 && addr < 0xBF80_3000:
                    addr &= 0x1FFF;
                    REGISTERS[addr] = (byte)(value);
                    REGISTERS[addr + 1] = (byte)(value >> 8);
                    break;
                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                case uint KSEG0 when addr >= 0x9FC0_0000 && addr < 0x9FC8_0000:
                case uint KSEG1 when addr >= 0xBFC0_0000 && addr < 0xBFC8_0000: //BIOS mem map
                    addr &= 0x7_FFFF;
                    BIOS[addr] = (byte)(value);
                    BIOS[addr + 1] = (byte)(value >> 8);
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