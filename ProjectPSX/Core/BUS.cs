using ProjectPSX.Devices;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProjectPSX {
    //TODO:
    //Do loadX and WriteX simple functions that return a value based on a generic load and write giant switch pointer return
    // WIP: Already got rid of the multiple loadX writeX variants need to adress the giant switches but then how to handle the individual
    // components? they expect allways an uint and they transform to variables. (uint)(object) is out probably because it kills perf and
    // Unsafe.As is still noticeable so... some rework needed on the interaction between components and bus.
    public class BUS {

        //Memory
        IntPtr RAM = Marshal.AllocHGlobal(2048 * 1024);
        IntPtr EX1 = Marshal.AllocHGlobal(512 * 1024);
        IntPtr SCRATHPAD = Marshal.AllocHGlobal(1024);
        IntPtr REGISTERS = Marshal.AllocHGlobal(4 * 1024);
        IntPtr BIOS = Marshal.AllocHGlobal(512 * 1024);
        IntPtr IO = Marshal.AllocHGlobal(512);

        private unsafe byte* ramPtr;
        private unsafe byte* ex1Ptr;
        private unsafe byte* scrathpadPtr;
        private unsafe byte* registersPtr;
        private unsafe byte* biosPtr;
        private unsafe byte* ioPtr;

        //Other Subsystems
        public InterruptController interruptController;
        private DMA dma;
        public GPU gpu;
        private CDROM cdrom;
        private TIMERS timers;
        private JOYPAD joypad;
        private MDEC mdec;

        //temporary hardcoded bios/ex1
        private static string bios = "./SCPH1001.BIN";
        private static string nocashBios = "./nocashBios.ROM";
        private static string ex1 = "./caetlaEXP.BIN";

        public BUS() {
            interruptController = new InterruptController(); //refactor this to interface and callbacks
            dma = new DMA(this);
            gpu = new GPU();
            cdrom = new CDROM();
            timers = new TIMERS();
            joypad = new JOYPAD();
            mdec = new MDEC();

            initMem();
        }

        private unsafe void initMem() {
            ramPtr = (byte*)RAM;
            ex1Ptr = (byte*)EX1;
            scrathpadPtr = (byte*)SCRATHPAD;
            registersPtr = (byte*)REGISTERS;
            biosPtr = (byte*)BIOS;
            ioPtr = (byte*)IO;
        }

        internal void setWindow(Window window) {
            gpu.setWindow(window);
            joypad.setWindow(window);
        }

        internal unsafe uint load32(uint address) {
            //addr &= RegionMask[addr >> 29]; 
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            if (addr < 0x1F00_0000) {
                return load<uint>(addr & 0x1F_FFFF, ramPtr);
            } else if (addr < 0x1F08_0000) {
                return load<uint>(addr & 0x7_FFFF, ex1Ptr);
            } else if (addr >= 0x1f80_0000 && addr < 0x1f80_0400) {
                return load<uint>(addr & 0xFFF, scrathpadPtr);
            } else if (addr >= 0x1F80_1000 && addr < 0x1F80_2000) {
                if (addr == 0x1F801070) {
                    return interruptController.loadISTAT();
                } else if (addr == 0x1F80_1074) {
                    return interruptController.loadIMASK();
                } else if (addr >= 0x1F80_1040 && addr <= 0x1F80_104F) {
                    return joypad.load(Width.WORD, addr);
                } else if (addr >= 0x1F80_1080 && addr <= 0x1F80_10FF) {
                    return dma.load(addr);
                } else if (addr >= 0x1F80_1100 && addr <= 0x1F80_112B) {
                    //Console.WriteLine("[TIMERS] Load32 " + addr.ToString("x8"));
                    return timers.load(Width.WORD, addr);
                } else if (addr >= 0x1F80_1800 && addr <= 0x1F80_1803) {
                    return cdrom.load(Width.WORD, addr);
                } else if (addr == 0x1F80_1810) {
                    return gpu.loadGPUREAD();
                } else if (addr == 0x1F80_1814) {
                    return gpu.loadGPUSTAT();
                } else if (addr == 0x1F80_1820) {
                    return mdec.readMDEC0_Data();
                } else if (addr == 0x1F80_1824) {
                    return mdec.readMDEC1_Status();
                } else {
                    return load<uint>(addr & 0xFFF, registersPtr);
                }
            } else if (addr >= 0x1FC0_0000 && addr < 0x1FC8_0000) {
                return load<uint>(addr & 0x7_FFFF, biosPtr);
            } else if (addr >= 0xFFFE_0000 && addr < 0xFFFE_0200) {
                return load<uint>(addr & 0x1FF, ioPtr);
            } else {
                Console.WriteLine("[BUS] Load32 Unsupported: " + addr.ToString("x8"));
                return 0xFFFF_FFFF;
            }
        }

        internal unsafe uint load16(uint address) {
            //uint addr = address & RegionMask[address >> 29];
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            switch (addr) {
                case uint _ when addr < 0x1F00_0000:
                    return load<ushort>(addr & 0x1F_FFFF, ramPtr);

                case uint _ when addr < 0x1F08_0000:
                    return load<ushort>(addr & 0x7_FFFF, ex1Ptr);

                case uint _ when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                    return load<ushort>(addr & 0xFFF, scrathpadPtr);

                case uint _ when addr >= 0x1F80_1000 && addr < 0x1F80_2000:
                    switch (addr) {
                        case 0x1F801070:
                            return interruptController.loadISTAT();
                        case 0x1F801074:
                            return interruptController.loadIMASK();
                        case uint _ when addr >= 0x1F80_1040 && addr <= 0x1F80_104F:
                            return joypad.load(Width.HALF, addr);
                        case uint _ when addr >= 0x1F80_1080 && addr <= 0x1F80_10FF:
                            return dma.load(addr);
                        case uint _ when addr >= 0x1F80_1100 && addr <= 0x1F80_112B:
                            //Console.WriteLine("[TIMERS] Load16 " + addr.ToString("x8"));
                            //Console.ReadLine();
                            return timers.load(Width.HALF, addr);
                        case uint _ when addr >= 0x1F80_1800 && addr <= 0x1F80_1803:
                            return cdrom.load(Width.HALF, addr);
                        case 0x1F801810:
                            return gpu.loadGPUREAD();
                        case 0x1F801814:
                            return gpu.loadGPUSTAT();
                        default:
                            return load<ushort>(addr & 0xFFF, registersPtr);
                    }

                case uint _ when addr >= 0x1F80_2000 && addr < 0x1F80_2100:
                    return 0; //nocash bios tests

                case uint _ when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                    return load<ushort>(addr & 0x7_FFFF, biosPtr);

                case uint _ when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    return load<ushort>(addr & 0x1FF, ioPtr);

                default:
                    Console.WriteLine("[BUS] Load16 Unsupported: " + addr.ToString("x8"));
                    return 0xFFFF_FFFF;
            }
        }

        internal unsafe uint load8(uint address) {
            //addr &= RegionMask[addr >> 29];
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            switch (addr) {
                case uint _ when addr < 0x1F00_0000:
                    return load<byte>(addr & 0x1F_FFFF, ramPtr);

                case uint _ when addr < 0x1F08_0000:
                    return load<byte>(addr & 0x7_FFFF, ex1Ptr);

                case uint _ when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                    return load<byte>(addr & 0xFFF, scrathpadPtr);

                case uint _ when addr >= 0x1F80_1000 && addr < 0x1F80_2000:
                    switch (addr) {
                        case 0x1F801070:
                            return interruptController.loadISTAT();
                        case 0x1F801074:
                            return interruptController.loadIMASK();
                        case uint _ when addr >= 0x1F80_1040 && addr <= 0x1F80_104F:
                            return joypad.load(Width.BYTE, addr);
                        case uint _ when addr >= 0x1F80_1080 && addr <= 0x1F80_10FF:
                            return dma.load(addr);
                        case uint _ when addr >= 0x1F80_1100 && addr <= 0x1F80_112B:
                            Console.WriteLine("[TIMERS] Load8 " + addr.ToString("x8"));
                            Console.ReadLine();
                            return timers.load(Width.BYTE, addr);
                        case uint _ when addr >= 0x1F80_1800 && addr <= 0x1F80_1803:
                            return cdrom.load(Width.BYTE, addr);
                        case 0x1F801810:
                            return gpu.loadGPUREAD();
                        case 0x1F801814:
                            return gpu.loadGPUSTAT();
                        default:
                            return load<byte>(addr & 0xFFF, registersPtr);
                    }

                case uint _ when addr >= 0x1F80_2000 && addr < 0x1F80_2100:
                    return 0; //nocash bios tests

                case uint _ when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                    return load<byte>(addr & 0x7_FFFF, biosPtr);

                case uint _ when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    return load<byte>(addr & 0x1FF, ioPtr);

                default:
                    Console.WriteLine("[BUS] Load8 Unsupported: " + addr.ToString("x8"));
                    return 0xFFFF_FFFF;
            }
        }

        internal unsafe void write32(uint address, uint value) {
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            switch (addr) {
                case uint _ when addr < 0x1F00_0000:
                    write(addr & 0x1F_FFFF, value, ramPtr);
                    break;

                case uint _ when addr < 0x1F08_0000:
                    write(addr & 0x7_FFFF, value, ex1Ptr);
                    break;

                case uint _ when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                    write(addr & 0xFFF, value, scrathpadPtr);
                    break;

                case uint _ when addr >= 0x1F80_1000 && addr < 0x1F80_2000:
                    switch (addr) {
                        case 0x1F801070:
                            interruptController.writeISTAT(value);
                            break;
                        case 0x1F801074:
                            interruptController.writeIMASK(value);
                            break;
                        case uint _ when addr >= 0x1F80_1040 && addr <= 0x1F80_104F:
                            joypad.write(Width.WORD, addr, value);
                            break;
                        case uint _ when addr >= 0x1F80_1080 && addr <= 0x1F80_10FF:
                            dma.write(addr, value);
                            break;
                        case uint _ when addr >= 0x1F80_1100 && addr <= 0x1F80_112B:
                            //Console.WriteLine("[TIMERS] Write32 " +addr.ToString("x8") + value.ToString("x8"));
                            timers.write(Width.WORD, addr, value);
                            break;
                        case uint _ when addr >= 0x1F80_1800 && addr <= 0x1F80_1803:
                            cdrom.write(Width.WORD, addr, value);
                            break;
                        case 0x1F801810:
                            gpu.writeGP0(value);
                            break;
                        case 0x1F801814:
                            gpu.writeGP1(value);
                            break;
                        case 0x1F801820:
                            mdec.writeMDEC0_Command(value);
                            break;
                        case 0x1F801824:
                            mdec.writeMDEC1_Control(value);
                            break;

                        default:
                            addr &= 0xFFF;
                            write(addr, value, registersPtr);
                            break;
                    }
                    break;

                case uint _ when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                    Console.WriteLine("[BUS] [WARNING] Write on BIOS range" + addr.ToString("x8"));
                    break;

                case uint _ when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    write(addr & 0x1FF, value, ioPtr);
                    break;

                default:
                    Console.WriteLine("[BUS] Write32 Unsupported: " + addr.ToString("x8") + ": " + value.ToString("x8"));
                    break;
            }
        }

        internal unsafe void write16(uint address, ushort value) {
            //addr &= RegionMask[addr >> 29];
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            switch (addr) {
                case uint KUSEG when addr < 0x1F00_0000:
                    write(addr & 0x1F_FFFF, value, ramPtr);
                    break;

                case uint KUSEG when addr < 0x1F08_0000:
                    write(addr & 0x7_FFFF, value, ex1Ptr);
                    break;

                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                    write(addr & 0xFFF, value, scrathpadPtr);
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
                            joypad.write(Width.HALF, addr, value);
                            break;
                        case uint DMA when addr >= 0x1F80_1080 && addr <= 0x1F80_10FF:
                            dma.write(addr, value);
                            break;
                        case uint TIMERS when addr >= 0x1F80_1100 && addr <= 0x1F80_112B:
                            //Console.WriteLine("[TIMERS] Write 16 " +addr.ToString("x8") + value.ToString("x8"));
                            //Console.ReadLine();
                            timers.write(Width.HALF, addr, value);
                            break;
                        case uint CDROM when addr >= 0x1F80_1800 && addr <= 0x1F80_1803:
                            cdrom.write(Width.HALF, addr, value);
                            break;
                        case 0x1F801810:
                            gpu.writeGP0(value);
                            break;
                        case 0x1F801814:
                            gpu.writeGP1(value);
                            break;

                        default:
                            addr &= 0xFFF;
                            write(addr, value, registersPtr);
                            break;
                    }
                    break;

                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                    Console.WriteLine("[BUS] [WARNING] Write on BIOS range" + addr.ToString("x8"));
                    break;

                case uint KSEG2 when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    write(addr & 0x1FF, value, ioPtr);
                    break;

                default:
                    Console.WriteLine("[BUS] Write16 Unsupported: " + addr.ToString("x8") + ": " + value.ToString("x8"));
                    break;
            }
        }

        internal unsafe void write8(uint address, byte value) {
            //addr &= RegionMask[addr >> 29];
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            switch (addr) {
                case uint KUSEG when addr < 0x1F00_0000:
                    write(addr & 0x1F_FFFF, value, ramPtr);
                    break;

                case uint KUSEG when addr < 0x1F08_0000:
                    write(addr & 0x7_FFFF, value, ex1Ptr);
                    break;

                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                    write(addr & 0xFFF, value, scrathpadPtr);
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
                            joypad.write(Width.BYTE, addr, value);
                            break;
                        case uint DMA when addr >= 0x1F80_1080 && addr <= 0x1F80_10FF:
                            dma.write(addr, value);
                            break;
                        case uint TIMERS when addr >= 0x1F80_1100 && addr <= 0x1F80_112B:
                            Console.WriteLine("[TIMERS] Write 8 " + addr.ToString("x8") + value.ToString("x8"));
                            Console.ReadLine();
                            timers.write(Width.BYTE, addr, value);
                            break;
                        case uint CDROM when addr >= 0x1F80_1800 && addr <= 0x1F80_1803:
                            cdrom.write(Width.BYTE, addr, value);
                            break;
                        case 0x1F801810:
                            gpu.writeGP0(value);
                            break;
                        case 0x1F801814:
                            gpu.writeGP1(value);
                            break;

                        default:
                            addr &= 0xFFF;
                            write(addr, value, registersPtr);
                            break;
                    }
                    break;

                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                    Console.WriteLine("[BUS] [WARNING] Write on BIOS range" + addr.ToString("x8"));
                    break;

                case uint KSEG2 when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    write(addr & 0x1FF, value, ioPtr);
                    break;

                default:
                    Console.WriteLine("[BUS] Write8 Unsupported: " + addr.ToString("x8") + ": " + value.ToString("x8"));
                    break;
            }
        }

        private uint maskAddr(uint addr) {
            uint i = addr >> 29;
            return addr & RegionMask[i];
        }

        internal void loadBios() {
            byte[] rom = File.ReadAllBytes(bios);
            Marshal.Copy(rom, 0, BIOS, rom.Length);
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

        internal unsafe (uint, uint, uint, uint) loadEXE(String test) {
            byte[] exe = File.ReadAllBytes(test);
            uint PC = Unsafe.As<byte, uint>(ref exe[0x10]);
            uint R28 = Unsafe.As<byte, uint>(ref exe[0x14]);
            uint R29 = Unsafe.As<byte, uint>(ref exe[0x30]);
            uint R30 = R29; //base
            R30 += Unsafe.As<byte, uint>(ref exe[0x34]); //offset

            uint DestAdress = Unsafe.As<byte, uint>(ref exe[0x18]);

            Marshal.Copy(exe, 0x800, (IntPtr)(ramPtr + (DestAdress & 0x1F_FFFF)), exe.Length - 0x800);

            return (PC, R28, R29, R30);
        }

        internal void loadEXP() {
            byte[] exe = File.ReadAllBytes(ex1);
            Marshal.Copy(exe, 0, EX1, exe.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void tick(int cycles) {
            if (gpu.tick(cycles)) interruptController.set(Interrupt.VBLANK);
            if (cdrom.tick(cycles)) interruptController.set(Interrupt.CDROM);
            if (dma.tick()) interruptController.set(Interrupt.DMA);

            timers.syncGPU(gpu.getBlanksAndDot()); //test

            if (timers.tick(0, cycles)) interruptController.set(Interrupt.TIMER0);
            if (timers.tick(1, cycles)) interruptController.set(Interrupt.TIMER1);
            if (timers.tick(2, cycles)) interruptController.set(Interrupt.TIMER2);
            if (joypad.tick()) interruptController.set(Interrupt.CONTR);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe T load<T>(uint addr, byte* ptr) where T : unmanaged {
            return *(T*)(ptr + addr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void write<T>(uint addr, T value, byte* ptr) where T : unmanaged {
            *(T*)(ptr + addr) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe uint DmaFromRam(uint addr) {
            return *(uint*)(ramPtr + (addr & 0x1F_FFFF));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe uint[] DmaFromRam(uint addr, uint size) {
            int[] buffer = new int[size];
            Marshal.Copy((IntPtr)(ramPtr + (addr & 0x1F_FFFF)), buffer, 0, (int)size);
            return Unsafe.As<int[], uint[]>(ref buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void DmaToRam(uint addr, uint value) {
            *(uint*)(ramPtr + (addr & 0x1F_FFFF)) = value;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void DmaToRam(uint addr, byte[] buffer, uint size) {
            Marshal.Copy(buffer, 0, (IntPtr)(ramPtr + (addr & 0x1F_FFFF)), (int)size * 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint DmaFromGpu() {
            return gpu.loadGPUREAD();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DmaToGpu(uint value) {
            gpu.writeGP0(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DmaToGpu(uint[] buffer) {
            gpu.writeGP0(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint DmaFromCD() {
            return cdrom.getData();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DmaToMdecIn(uint[] load) { //todo: actual process the whole array
            foreach (uint word in load)
                mdec.writeMDEC0_Command(word);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint DmaFromMdecOut() {
            return mdec.readMDEC0_Data();
        }

        private static uint[] RegionMask = {
        0xFFFF_FFFF, 0xFFFF_FFFF, 0xFFFF_FFFF, 0xFFFF_FFFF, // KUSEG: 2048MB
        0x7FFF_FFFF,                                        // KSEG0:  512MB
        0x1FFF_FFFF,                                        // KSEG1:  512MB
        0xFFFF_FFFF, 0xFFFF_FFFF,                           // KSEG2: 1024MB
        };
    }
}
