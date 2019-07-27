using ProjectPSX.Devices;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProjectPSX {
    //TODO:
    //Do loadX and WriteX simple functions that return a value based on a generic load and write giant switch pointer return
    //like the test loadRAM and get rid of the multiple giant switchs
    public class BUS : DMA_Transfer {

        //Memory
        private byte[] RAM = new byte[2048 * 1024];
        private byte[] EX1 = new byte[512 * 1024];
        private byte[] SCRATHPAD = new byte[1024];
        private byte[] REGISTERS = new byte[4 * 1024];
        private byte[] BIOS = new byte[512 * 1024];
        private byte[] IO = new byte[512];

        //ram tests
        GCHandle ramHandle;
        private unsafe byte* ramPtr;


        //Other Subsystems
        public InterruptController interruptController;
        private DMA dma;
        private GPU gpu;
        private CDROM cdrom;
        private TIMERS timers;
        private JOYPAD joypad;
        private MDEC mdec;

        public BUS() {
            interruptController = new InterruptController(); //refactor this to interface and callbacks
            dma = new DMA();
            gpu = new GPU();
            cdrom = new CDROM();
            timers = new TIMERS();
            joypad = new JOYPAD();
            mdec = new MDEC();

            dma.setDMA_Transfer(this);

            try {
                initMem();
            } finally {
                ramHandle.Free();
            }

        }

        private unsafe void initMem() {
            ramHandle = GCHandle.Alloc(RAM, GCHandleType.Pinned);
            ramPtr = (byte*)ramHandle.AddrOfPinnedObject().ToPointer();
        }

        internal void setWindow(Window window) {
            gpu.setWindow(window);
            joypad.setWindow(window);
        }

        internal uint load32(uint address) {
            //addr &= RegionMask[addr >> 29]; 
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            if (addr < 0x1F00_0000) {
                return loadRAM32(addr & 0x1F_FFFF, RAM);
            } else if (addr < 0x1F08_0000) {
                return load32(addr & 0x7_FFFF, EX1);
            } else if (addr >= 0x1f80_0000 && addr < 0x1f80_0400) {
                return load32(addr & 0xFFF, SCRATHPAD);
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
                    return load32(addr & 0xFFF, REGISTERS);
                }
            } else if (addr >= 0x1FC0_0000 && addr < 0x1FC8_0000) {
                return load32(addr & 0x7_FFFF, BIOS);
            } else if (addr >= 0xFFFE_0000 && addr < 0xFFFE_0200) {
                return load32(addr & 0x1FF, IO);
            } else {
                return 0xFFFF_FFFF;
            }
        }


        internal uint load16(uint address) {
            //uint addr = address & RegionMask[address >> 29];
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            switch (addr) {
                case uint _ when addr < 0x1F00_0000:
                    return load16(addr & 0x1F_FFFF, RAM);

                case uint _ when addr < 0x1F08_0000:
                    return load16(addr & 0x7_FFFF, EX1);

                case uint _ when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                    return load16(addr & 0xFFF, SCRATHPAD);

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
                            //Console.WriteLine("[TIMERS] Load " + addr.ToString("x8"));
                            return timers.load(Width.HALF, addr);
                        case uint _ when addr >= 0x1F80_1800 && addr <= 0x1F80_1803:
                            return cdrom.load(Width.HALF, addr);
                        case 0x1F801810:
                            return gpu.loadGPUREAD();
                        case 0x1F801814:
                            return gpu.loadGPUSTAT();
                        default:
                            return load16(addr & 0xFFF, REGISTERS);
                    }

                case uint _ when addr >= 0x1F80_2000 && addr < 0x1F80_2100:
                    return 0; //nocash bios tests

                case uint _ when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                    return load16(addr & 0x7_FFFF, BIOS);

                case uint _ when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    return load16(addr & 0x1FF, IO);

                default:
                    Console.WriteLine("[BUS] Load Unsupported: " + addr.ToString("x4"));
                    return 0xFFFF_FFFF;
            }
        }


        internal uint load8(uint address) {
            //addr &= RegionMask[addr >> 29];
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            switch (addr) {
                case uint _ when addr < 0x1F00_0000:
                    return load8(addr & 0x1F_FFFF, RAM);

                case uint _ when addr < 0x1F08_0000:
                    return load8(addr & 0x7_FFFF, EX1);

                case uint _ when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                    return load8(addr & 0xFFF, SCRATHPAD);

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
                            //Console.WriteLine("[TIMERS] Load " + addr.ToString("x8"));
                            return timers.load(Width.BYTE, addr);
                        case uint _ when addr >= 0x1F80_1800 && addr <= 0x1F80_1803:
                            return cdrom.load(Width.BYTE, addr);
                        case 0x1F801810:
                            return gpu.loadGPUREAD();
                        case 0x1F801814:
                            return gpu.loadGPUSTAT();
                        default:
                            return load8(addr & 0xFFF, REGISTERS);
                    }

                case uint _ when addr >= 0x1F80_2000 && addr < 0x1F80_2100:
                    return 0; //nocash bios tests

                case uint _ when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                    return load8(addr & 0x7_FFFF, BIOS);

                case uint _ when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    return load8(addr & 0x1FF, IO);

                default:
                    Console.WriteLine("[BUS] Load Unsupported: " + addr.ToString("x4"));
                    return 0xFFFF_FFFF;
            }
        }

        private uint maskAddr(uint addr) {
            uint i = addr >> 29;
            return addr & RegionMask[i];
        }

        internal void write32(uint address, uint value) {
            //Console.WriteLine(addr.ToString("x8"));
            //addr &= RegionMask[addr >> 29];
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            switch (addr) {
                case uint _ when addr < 0x1F00_0000:
                    write32(addr & 0x1F_FFFF, value, RAM);
                    break;

                case uint _ when addr < 0x1F08_0000:
                    write32(addr & 0x7_FFFF, value, EX1);
                    break;

                case uint _ when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                    write32(addr & 0xFFF, value, SCRATHPAD);
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
                            //Console.WriteLine("[TIMERS] Write " +addr.ToString("x8") + value.ToString("x8"));
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
                            write32(addr, value, REGISTERS);
                            break;
                    }
                    break;

                case uint _ when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                    Console.WriteLine("[BUS] [WARNING] Write on BIOS range" + addr.ToString("x8"));
                    break;

                case uint _ when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    write32(addr & 0x1FF, value, IO);
                    break;

                default:
                    Console.WriteLine("[BUS] Write Unsupported: " + addr.ToString("x4") + ": " + value.ToString("x4"));
                    break;
            }
        }

        internal void write16(uint address, uint value) {
            //addr &= RegionMask[addr >> 29];
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            switch (addr) {
                case uint KUSEG when addr < 0x1F00_0000:
                    write16(addr & 0x1F_FFFF, value, RAM);
                    break;

                case uint KUSEG when addr < 0x1F08_0000:
                    write16(addr & 0x7_FFFF, value, EX1);
                    break;

                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                    write16(addr & 0xFFF, value, SCRATHPAD);
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
                            //Console.WriteLine("[TIMERS] Write " +addr.ToString("x8") + value.ToString("x8"));
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
                            write16(addr, value, REGISTERS);
                            break;
                    }
                    break;

                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                    Console.WriteLine("[BUS] [WARNING] Write on BIOS range" + addr.ToString("x8"));
                    break;

                case uint KSEG2 when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    write16(addr & 0x1FF, value, IO);
                    break;

                default:
                    Console.WriteLine("[BUS] Write Unsupported: " + addr.ToString("x4") + ": " + value.ToString("x4"));
                    break;
            }
        }

        internal void write8(uint address, uint value) {
            //addr &= RegionMask[addr >> 29];
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            switch (addr) {
                case uint KUSEG when addr < 0x1F00_0000:
                    write8(addr & 0x1F_FFFF, value, RAM);
                    break;

                case uint KUSEG when addr < 0x1F08_0000:
                    write8(addr & 0x7_FFFF, value, EX1);
                    break;

                case uint KUSEG when addr >= 0x1F80_0000 && addr < 0x1F80_0400:
                    write8(addr & 0xFFF, value, SCRATHPAD);
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
                            //Console.WriteLine("[TIMERS] Write " +addr.ToString("x8") + value.ToString("x8"));
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
                            write8(addr, value, REGISTERS);
                            break;
                    }
                    break;

                case uint KUSEG when addr >= 0x1FC0_0000 && addr < 0x1FC8_0000:
                    Console.WriteLine("[BUS] [WARNING] Write on BIOS range" + addr.ToString("x8"));
                    break;

                case uint KSEG2 when addr >= 0xFFFE_0000 && addr < 0xFFFE_0200:
                    write8(addr & 0x1FF, value, IO);
                    break;

                default:
                    Console.WriteLine("[BUS] Write Unsupported: " + addr.ToString("x4") + ": " + value.ToString("x4"));
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal uint load16(uint addr, byte[] memory) {
            return (uint)(memory[addr + 1] << 8 | memory[addr]);
            //return Unsafe.As<byte, ushort>(ref memory[addr]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal uint load8(uint addr, byte[] memory) {
            return memory[addr];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void write16(uint addr, uint value, byte[] memory) {
            memory[addr] = (byte)value; memory[addr + 1] = (byte)(value >> 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void write8(uint addr, uint value, byte[] memory) {
            memory[addr] = (byte)value;
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
            uint PC = load32(0x10, exe);
            uint R28 = load32(0x14, exe);
            uint R29 = load32(0x30, exe);
            uint R30 = R29; //base
            R30 += load32(0x34, exe); //offset

            uint DestAdress = load32(0x18, exe);
            Array.Copy(exe, 0x800, RAM, DestAdress & 0xFFFFFFF, exe.Length - 0x800);

            return (PC, R28, R29, R30);
        }

        string caetla = "./caetlaEXP.BIN";
        internal void loadEXP() {
            byte[] exe = File.ReadAllBytes(caetla);
            Array.Copy(exe, 0, EX1, 0, exe.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void tick(int cycles) {
            if (gpu.tick(cycles)) interruptController.set(Interrupt.VBLANK);
            if (cdrom.tick(cycles)) interruptController.set(Interrupt.CDROM); // /2 this breaks the PS Logo as it gets "bad hankakus" on TTY but makes FF7 work.
            if (dma.tick()) interruptController.set(Interrupt.DMA);
            if (timers.tick(0, cycles)) interruptController.set(Interrupt.TIMER0);
            if (timers.tick(1, cycles)) interruptController.set(Interrupt.TIMER1);
            if (timers.tick(2, cycles)) interruptController.set(Interrupt.TIMER2);
            if (joypad.tick(cycles)) interruptController.set(Interrupt.CONTR);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void toGPU(uint value) {
            gpu.writeGP0(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override uint fromRAM(uint addr) {
            //return load32(addr, RAM);
            //Console.WriteLine(addr.ToString("x8"));
            return load32(addr & 0x1F_FFFF, RAM);
        }


        private unsafe uint loadRAM32(uint addr, byte[] rAM) {
            return *(uint*)(ramPtr + (addr & 0x1F_FFFF)); //this fixed raiden and ctr
            //return (uint)(rAM[addr + 3] << 24 | rAM[addr + 2] << 16 | rAM[addr + 1] << 8 | rAM[addr]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe uint load32(uint addr, byte[] memory) {
            //1: Naive Approach
            //return (uint)(memory[addr + 3] << 24 | memory[addr + 2] << 16 | memory[addr + 1] << 8 | memory[addr]);
            //2: Pointer Magic Approach
            //fixed (void* ptr = &memory[addr]) {
            //    // p is pinned as well as object, so create another pointer to show incrementing it.
            //    return *(uint*)ptr;
            //}
            //3: fastest approach (it appears that even with the overhead it avoids the pinning and can be inlined)
            return Unsafe.As<byte, uint>(ref memory[addr]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void write32(uint addr, uint value, byte[] memory) {
            //memory[addr] = (byte)value; memory[addr + 1] = (byte)(value >> 8);
            //memory[addr + 2] = (byte)(value >> 16); memory[addr + 3] = (byte)(value >> 24);
            unsafe {
                fixed (byte* ptr = &memory[addr]) {
                    // p is pinned as well as object, so create another pointer to show incrementing it.
                    uint* ptrValue = (uint*)ptr;
                    *ptrValue = value;
                }
            }
            //unsafe {
            //    var ptr = Unsafe.AsPointer(ref memory[addr]);
            //    Unsafe.Write(ptr, value);
            //}

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void toRAM(uint addr, uint value) {
            write32(addr, value, RAM);
            //unsafe {
            //    var ptr = Unsafe.AsPointer(ref RAM[addr]);
            //    Unsafe.Write(ptr, value);
            //}
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void toRAM(uint addr, byte[] buffer, uint size) {
            //Console.WriteLine("cdToRam " + addr.ToString("x8") + " bufferLength " + buffer.Length + " size " + size);
            Buffer.BlockCopy(buffer, 0, RAM, (int)addr, (int)size * 4);
            //Console.WriteLine(load32(addr).ToString("x8"));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override uint fromGPU() {
            return gpu.loadGPUREAD();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override uint fromCD() {
            return cdrom.getData();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void toGPU(uint[] buffer) {
            gpu.writeGP0(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override uint[] fromRAM(uint addr, uint size) {
            uint[] buffer = new uint[size];
            Buffer.BlockCopy(RAM, (int)(addr & 0x1F_FFFF), buffer, 0, (int)size * 4);
            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override byte[] fromCD(uint size) { //test
            return cdrom.getDataBuffer();
        }

        public override void toMDECin(uint[] load) { //todo: actual process the whole array
            foreach(uint word in load)
            mdec.writeMDEC0_Command(word);
        }

        public override uint fromMDECout() {
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