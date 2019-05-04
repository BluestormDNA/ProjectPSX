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
        private DMA dma;
        private GPU gpu;
        private CDROM cdrom;
        private InterruptController interruptController;
        private TIMERS timers;
        private JOYPAD joypad;

        public BUS() {
            interruptController = new InterruptController(); //refactor this to interface and callbacks
            dma = new DMA();
            gpu = new GPU();
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
            switch (addr) {
                case uint KUSEG when addr >= 0x0000_0000 && addr < 0x1F00_0000:
                case uint KSEG0 when addr >= 0x8000_0000 && addr < 0x9F00_0000:
                case uint KSEG1 when addr >= 0xA000_0000 && addr < 0xBF00_0000:
                    addr &= 0x1F_FFFF;
                    return load(w, addr, RAM);

                case uint KUSEG when addr >= 0x1F00_0000 && addr < 0x1F08_0000:
                case uint KSEG0 when addr >= 0x9F00_0000 && addr < 0x9F08_0000:
                case uint KSEG1 when addr >= 0xBF00_0000 && addr < 0xBF08_0000:
                    if (addr == 0x1F02_0018) {
                        Console.WriteLine("Load ON SWITCH EX");
                    }
                    addr &= 0x7_FFFF;
                    return load(w, addr, EX1);

                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                case uint KSEG0 when addr >= 0x9F80_0000 && addr < 0x9F80_0400:
                case uint KSEG1 when addr >= 0xBF80_0000 && addr < 0xBF80_0400:
                    addr &= 0xFFF;
                    return load(w, addr, SCRATHPAD);

                case uint KUSEG when addr >= 0x1F80_1000 && addr < 0x1F80_2000:
                case uint KSEG0 when addr >= 0x9F80_1000 && addr < 0x9F80_2000:
                case uint KSEG1 when addr >= 0xBF80_1000 && addr < 0xBF80_2000:

                    switch (addr) {
                        case 0x1F801070:
                            return interruptController.loadISTAT();
                        case 0x1F801074:
                            return interruptController.loadIMASK();
                        case uint JOYPAD when addr >= 0x1F80_1040 && addr <= 0x1F80_104F:
                            return joypad.load(w, addr);
                        case uint DMA when addr >= 0x1F80_1080 && addr <= 0x1F80_10FF:
                            return dma.load(w, addr);
                        //case uint TIMERS when addr >= 0x1F80_1100 && addr <= 0x1F80_112B:
                            //return timers.load(w, addr);
                        case uint CDROM when addr >= 0x1F80_1800 && addr <= 0x1F80_1803:
                            return cdrom.load(w, addr);
                        case 0x1F801810:
                            return gpu.loadGPUREAD();
                        case 0x1F801814:
                            return gpu.loadGPUSTAT();
                        default:
                            addr &= 0xFFF;
                            return load(w, addr, REGISTERS);
                            //return 0;
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
                    Console.ReadLine();
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

                case uint KUSEG when addr >= 0x1F00_0000 && addr < 0x1F08_0000:
                case uint KSEG0 when addr >= 0x9F00_0000 && addr < 0x9F08_0000:
                case uint KSEG1 when addr >= 0xBF00_0000 && addr < 0xBF08_0000:
                    if(addr == 0x1F02_0018) {
                        Console.WriteLine("Write ON SWITCH EX");
                    }
                    addr &= 0x7_FFFF;
                    Console.WriteLine("addr" + addr.ToString("x8"));
                    write(w, addr, value, EX1);
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
                        //case uint TIMERS when addr >= 0x1F80_1100 && addr <= 0x1F80_112B:
                            //timers.write(w, addr, value);
                            //break;
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

        string psx = "./SCPH1001.BIN";
        string noc = "./PSX-XBOO.ROM";
        internal void loadBios() {
            byte[] rom = File.ReadAllBytes(psx);
            Array.Copy(rom, 0, BIOS, 0, rom.Length);
        }

        //PSX executables are having an 800h-byte header, followed by the code/data.
        //
        // 000h-007h ASCII ID "PS-X EXE"
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

        string ex = "./EXPROM.BIN";
        string nochas = "./PSX-EXP.ROM";
        internal void loadEXP() {
            byte[] exe = File.ReadAllBytes(ex);
            Array.Copy(exe, 0, EX1, 0, exe.Length);
        }

        public void tick(uint cycles) {
            if (gpu.tick(cycles)) interruptController.set(Interrupt.VBLANK);
            if (cdrom.tick(cycles)) interruptController.set(Interrupt.CDROM);
            //if (timers.tick(0, cycles)) interruptController.set(Interrupt.TIMER0);
            //if (timers.tick(1, cycles)) interruptController.set(Interrupt.TIMER1);
            //if (timers.tick(2, cycles)) interruptController.set(Interrupt.TIMER2);
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
            return gpu.loadGPUSTAT();
        }

        uint DMA_Transfer.fromCD() {
            return cdrom.getData();
        }
    }
}