using ProjectPSX.Devices;
using System;
using System.IO;

namespace ProjectPSX {
    public class BUS : DMA_Transfer {

        //Memory
        private byte[] RAM = new byte[2048 * 1024];
        private byte[] EX1 = new byte[512 * 1024];
        private byte[] SCRATHPAD = new byte[1024];
        private byte[] REGISTERS = new byte[4 * 1024];
        private byte[] BIOS = new byte[512 * 1024];
        private byte[] IO = new byte[512];

        //Other Subsystems
        public InterruptController interruptController;
        private DMA dma;
        private GPUTest gpu;
        private CDROM cdrom;
        private TIMERS timers;
        private JOYPAD joypad;

        public BUS() {
            interruptController = new InterruptController(); //refactor this to interface and callbacks
            dma = new DMA();
            gpu = new GPUTest();
            cdrom = new CDROM();
            timers = new TIMERS();
            joypad = new JOYPAD();

            dma.setDMA_Transfer(this);
        }

        internal void setWindow(Window window) {
            gpu.setWindow(window);
            joypad.setWindow(window);
        }

        internal uint load(Width w, uint addr) {
            addr &= RegionMask[addr >> 29];
            switch (addr) {
                case uint KUSEG when addr < 0x1F00_0000:
                    return load(w, addr & 0x1F_FFFF, RAM);

                case uint KUSEG when addr >= 0x1F00_0000 && addr < 0x1F08_0000:
                    if (addr == 0x1F02_0018) {
                        Console.WriteLine("Load ON SWITCH EX");
                    }
                    return load(w, addr & 0x7_FFFF, EX1);

                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                    return load(w, addr & 0xFFF, SCRATHPAD);

                case uint KUSEG when addr >= 0x1F80_1000 && addr < 0x1F80_2000:
                    switch (addr) {
                        case 0x1F801070:
                            return interruptController.loadISTAT();
                        case 0x1F801074:
                            return interruptController.loadIMASK();
                        case uint JOYPAD when addr >= 0x1F80_1040 && addr <= 0x1F80_104F:
                            return joypad.load(w, addr);
                        case uint DMA when addr >= 0x1F80_1080 && addr <= 0x1F80_10FF:
                            return dma.load(w, addr);
                        case uint TIMERS when addr >= 0x1F80_1100 && addr <= 0x1F80_112B:
                            //Console.WriteLine("[TIMERS] Load " + addr.ToString("x8"));
                            return timers.load(w, addr);
                        case uint CDROM when addr >= 0x1F80_1800 && addr <= 0x1F80_1803:
                            return cdrom.load(w, addr);
                        case 0x1F801810:
                            return gpu.loadGPUREAD();
                        case 0x1F801814:
                            return gpu.loadGPUSTAT();
                        default:
                            return load(w, addr & 0xFFF, REGISTERS);
                    }

                case uint KUSEG when addr >= 0x1F80_2000 && addr < 0x1F80_2100:
                    return 0; //nocash bios tests

                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                    return load(w, addr & 0x7_FFFF, BIOS);

                case uint KSEG2 when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    return load(w, addr & 0x1FF, IO);

                default:
                    Console.WriteLine("[BUS] Load Unsupported: " + addr.ToString("x4"));
                    return 0xFFFF_FFFF;
            }
        }

        private uint maskAddr(uint addr) {
            uint i = addr >> 29;
            return addr & RegionMask[i];
        }

        internal void write(Width w, uint addr, uint value) {
            addr &= RegionMask[addr >> 29];
            switch (addr) {
                case uint KUSEG when addr < 0x1F00_0000:
                    write(w, addr & 0x1F_FFFF, value, RAM);
                    break;

                case uint KUSEG when addr >= 0x1F00_0000 && addr < 0x1F08_0000:
                    if (addr == 0x1F02_0018) {
                        Console.WriteLine("Write ON SWITCH EX");
                    }
                    //Console.WriteLine("addr" + addr.ToString("x8")); //CAETLA DEBUG
                    write(w, addr & 0x7_FFFF, value, EX1);
                    break;

                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                    write(w, addr & 0xFFF, value, SCRATHPAD);
                    break;

                case uint KUSEG when addr >= 0x1F80_1000 && addr < 0x1F80_2000:
                    switch (addr) {
                        case 0x1F801070:
                            interruptController.writeISTAT(value);
                            break;
                        case 0x1F801074:
                            interruptController.writeIMASK(value);
                            break;
                        case uint JOYPAD when addr >= 0x1F80_1040 && addr <= 0x1F80_104F:
                            joypad.write(w, addr, value);
                            break;
                        case uint DMA when addr >= 0x1F80_1080 && addr <= 0x1F80_10FF:
                            dma.write(w, addr, value);
                            break;
                        case uint TIMERS when addr >= 0x1F80_1100 && addr <= 0x1F80_112B:
                            //Console.WriteLine("[TIMERS] Write " +addr.ToString("x8") + value.ToString("x8"));
                            timers.write(w, addr, value);
                            break;
                        case uint CDROM when addr >= 0x1F80_1800 && addr <= 0x1F80_1803:
                            cdrom.write(w, addr, value);
                            break;
                        case 0x1F801810:
                            gpu.writeGP0(value);
                            break;
                        case 0x1F801814:
                            gpu.writeGP1(value);
                            break;

                        default:
                            addr &= 0xFFF;
                            write(w, addr, value, REGISTERS);
                            break;
                    }
                    break;

                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                    Console.WriteLine("[BUS] [WARNING] Write on BIOS range" + addr.ToString("x8"));
                    break;

                case uint KSEG2 when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    write(w, addr & 0x1FF, value, IO);
                    break;

                default:
                    Console.WriteLine("[BUS] Write Unsupported: " + addr.ToString("x4") + ": " + value.ToString("x4"));
                    break;
            }
        }

        internal uint load(Width w, uint addr, byte[] memory) {
            switch (w) {
                case Width.WORD: return (uint)(memory[addr + 3] << 24 | memory[addr + 2] << 16 | memory[addr + 1] << 8 | memory[addr]);
                case Width.BYTE: return memory[addr];
                case Width.HALF: return (uint)(memory[addr + 1] << 8 | memory[addr]);
                default: return 0xFFFF_FFFF;
            }
        }

        internal void write(Width w, uint addr, uint value, byte[] memory) {
            switch (w) {
                case Width.WORD:
                    memory[addr] = (byte)value; memory[addr + 1] = (byte)(value >> 8);
                    memory[addr + 2] = (byte)(value >> 16); memory[addr + 3] = (byte)(value >> 24); break;
                case Width.BYTE: memory[addr] = (byte)value; break;
                case Width.HALF: memory[addr] = (byte)value; memory[addr + 1] = (byte)(value >> 8); break;
            }
        }

        string psx = "./SCPH1001.BIN";
        string no = "./nocashBios.ROM";
        internal void loadBios() {
            byte[] rom = File.ReadAllBytes(psx);
            Array.Copy(rom, 0, BIOS, 0, rom.Length);
        }

        //PSX executables are having an 800h-byte header, followed by the code/data.
        //
        // 000h-007h ASCII ID "PS-x EXE"
        // 008h-00Fh Zerofilled
        // 010h Initial PC(usually 80010000h, or higher)
        // 014h Initial GP/R28(usually 0)
        // 018h Destination Address in RAM(usually 80010000h, or higher)
        // 01Ch Filesize(must be N*800h)    (excluding 800h-byte header)
        // 020h Unknown/Unused(usually 0)
        // 024h Unknown/Unused(usually 0)
        // 028h Memfill Start Address(usually 0) (when below Size = None)
        // 02Ch Memfill Size in bytes(usually 0) (0=None)
        // 030h Initial SP/R29 & FP/R30 Base(usually 801FFFF0h) (or 0=None)
        // 034h Initial SP/R29 & FP/R30 Offs(usually 0, added to above Base)
        // 038h-04Bh Reserved for A(43h) Function(should be zerofilled in exefile)
        // 04Ch-xxxh ASCII marker
        //            "Sony Computer Entertainment Inc. for Japan area"
        //            "Sony Computer Entertainment Inc. for Europe area"
        //            "Sony Computer Entertainment Inc. for North America area"
        //            (or often zerofilled in some homebrew files)
        //            (the BIOS doesn't verify this string, and boots fine without it)
        // xxxh-7FFh Zerofilled
        // 800h...   Code/Data(loaded to entry[018h] and up)

        internal (uint, uint, uint, uint) loadEXE(String test) {
            byte[] exe = File.ReadAllBytes(test);
            uint PC = load(Width.WORD, 0x10, exe);
            uint R28 = load(Width.WORD, 0x14, exe);
            uint R29 = load(Width.WORD, 0x30, exe);
            uint R30 = R29; //base
            R30 += load(Width.WORD, 0x34, exe); //offset

            uint DestAdress = load(Width.WORD, 0x18, exe);
            Array.Copy(exe, 0x800, RAM, DestAdress & 0xFFFFFFF, exe.Length - 0x800);

            return (PC, R28, R29, R30);
        }

        string caetla = "./caetlaEXP.BIN";
        internal void loadEXP() {
            byte[] exe = File.ReadAllBytes(caetla);
            Array.Copy(exe, 0, EX1, 0, exe.Length);
        }

        public void tick(int cycles) {
            if (gpu.tick(cycles)) interruptController.set(Interrupt.VBLANK);
            if (cdrom.tick(cycles/3)) interruptController.set(Interrupt.CDROM);
            if (dma.tick()) interruptController.set(Interrupt.DMA);
            if (timers.tick(0, cycles/3)) interruptController.set(Interrupt.TIMER0);
            if (timers.tick(1, cycles/3)) interruptController.set(Interrupt.TIMER1);
            if (timers.tick(2, cycles/3)) interruptController.set(Interrupt.TIMER2);
            if (joypad.tick(cycles)) interruptController.set(Interrupt.CONTR);
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

        uint DMA_Transfer.fromGPU() {
            return gpu.loadGPUREAD();
        }

        uint DMA_Transfer.fromCD() {
            return cdrom.getData();
        }

        private readonly uint[] RegionMask = {
        0xFFFF_FFFF, 0xFFFF_FFFF, 0xFFFF_FFFF, 0xFFFF_FFFF, // KUSEG: 2048MB
        0x7FFF_FFFF,                                        // KSEG0:  512MB
        0x1FFF_FFFF,                                        // KSEG1:  512MB
        0xFFFF_FFFF, 0xFFFF_FFFF,                           // KSEG2: 1024MB
        };
    }
}