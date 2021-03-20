﻿using ProjectPSX.Devices;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ProjectPSX {
    //TODO:
    //Do loadX and WriteX simple functions that return a value based on a generic load and write giant switch pointer return
    // WIP: Already got rid of the multiple loadX writeX variants need to adress the giant switches but then how to handle the individual
    // components? they expect allways an uint and they transform to variables. (uint)(object) is out probably because it kills perf and
    // Unsafe.As is still noticeable so... some rework needed on the interaction between components and bus.
    public class BUS {

        //Memory
        private unsafe byte* ramPtr = (byte*)Marshal.AllocHGlobal(2048 * 1024);
        private unsafe byte* ex1Ptr = (byte*)Marshal.AllocHGlobal(512 * 1024);
        private unsafe byte* scrathpadPtr = (byte*)Marshal.AllocHGlobal(1024);
        private unsafe byte* biosPtr = (byte*)Marshal.AllocHGlobal(512 * 1024);
        private unsafe byte* sio = (byte*)Marshal.AllocHGlobal(0x10);
        private unsafe byte* memoryControl1 = (byte*)Marshal.AllocHGlobal(0x40);
        private unsafe byte* memoryControl2 = (byte*)Marshal.AllocHGlobal(0x10);

        private uint memoryCache;

        //Other Subsystems
        public InterruptController interruptController;
        private DMA dma;
        private GPU gpu;
        private CDROM cdrom;
        private TIMERS timers;
        private JOYPAD joypad;
        private MDEC mdec;
        private SPU spu;

        //temporary hardcoded bios/ex1
        private static string bios = "./SCPH1001.BIN";
        private static string ex1 = "./caetlaEXP.BIN";

        public BUS(GPU gpu, CDROM cdrom, SPU spu, JOYPAD joypad, TIMERS timers, MDEC mdec, InterruptController interruptController) {
            dma = new DMA(this);
            this.gpu = gpu;
            this.cdrom = cdrom;
            this.timers = timers;
            this.mdec = mdec;
            this.spu = spu;
            this.joypad = joypad;
            this.interruptController = interruptController;
        }

        public unsafe uint load32(uint address) {
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            if (addr < 0x1F00_0000) {
                return load<uint>(addr & 0x1F_FFFF, ramPtr);
            } else if (addr < 0x1F80_0000) {
                return load<uint>(addr & 0x7_FFFF, ex1Ptr);
            } else if (addr < 0x1f80_0400) {
                return load<uint>(addr & 0xFFF, scrathpadPtr);
            } else if (addr < 0x1F80_1040) {
                return load<uint>(addr & 0x3F, memoryControl1);
            } else if (addr < 0x1F80_1050) {
                return joypad.load(addr);
            } else if (addr < 0x1F80_1060) {
                Console.WriteLine($"[BUS] Read Unsupported to SIO address: {addr:x8}");
                return load<uint>(addr & 0x3F, sio);
            } else if (addr < 0x1F80_1070) {
                return load<uint>(addr & 0xF, memoryControl2);
            } else if (addr < 0x1F801080) {
                return interruptController.load(addr);
            } else if (addr < 0x1F80_1100) {
                return dma.load(addr);
            } else if (addr < 0x1F80_1140) {
                return timers.load(addr);
            } else if (addr <= 0x1F80_1803) {
                return cdrom.load(addr);
            } else if (addr == 0x1F80_1810) {
                return gpu.loadGPUREAD();
            } else if (addr == 0x1F80_1814) {
                return gpu.loadGPUSTAT();
            } else if (addr == 0x1F80_1820) {
                return mdec.readMDEC0_Data();
            } else if (addr == 0x1F80_1824) {
                return mdec.readMDEC1_Status();
            } else if (addr < 0x1F802000) {
                return spu.load(addr);
            } else if (addr < 0x1FC8_0000) {
                return load<uint>(addr & 0x7_FFFF, biosPtr);
            } else if (addr == 0xFFFE0130) {
                return memoryCache;
            } else {
                Console.WriteLine("[BUS] Load32 Unsupported: " + addr.ToString("x8"));
                return 0xFFFF_FFFF;
            }
        }

        public unsafe void write32(uint address, uint value) {
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            if (addr < 0x1F00_0000) {
                write(addr & 0x1F_FFFF, value, ramPtr);
            } else if (addr < 0x1F80_0000) {
                write(addr & 0x7_FFFF, value, ex1Ptr);
            } else if (addr < 0x1f80_0400) {
                write(addr & 0x3FF, value, scrathpadPtr);
            } else if (addr < 0x1F80_1040) {
                write(addr & 0x3F, value, memoryControl1);
            } else if (addr < 0x1F80_1050) {
                joypad.write(addr, value);
            } else if (addr < 0x1F80_1060) {
                Console.WriteLine($"[BUS] Write Unsupported to SIO address: {addr:x8} : {value:x8}");
                write(addr & 0xF, value, sio);
            } else if (addr < 0x1F80_1070) {
                write(addr & 0xF, value, memoryControl2);
            } else if (addr < 0x1F801080) {
                interruptController.write(addr, value);
            } else if (addr < 0x1F80_1100) {
                dma.write(addr, value);
            } else if (addr < 0x1F80_1140) {
                timers.write(addr, value);
            } else if (addr < 0x1F80_1810) {
                cdrom.write(addr, value);
            } else if (addr < 0x1F80_1820) {
                //Task.Run(() => gpu.write(addr, value));
                gpu.write(addr, value);
            } else if (addr < 0x1F80_1830) {
                mdec.write(addr, value);
            } else if (addr < 0x1F802000) {
                spu.write(addr, (ushort)value);
            } else if (addr == 0xFFFE0130) {
                memoryCache = value;
            } else {
                Console.WriteLine($"[BUS] Write32 Unsupported: {addr:x8}");
            }
        }

        public unsafe void write16(uint address, ushort value) {
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            if (addr < 0x1F00_0000) {
                write(addr & 0x1F_FFFF, value, ramPtr);
            } else if (addr < 0x1F80_0000) {
                write(addr & 0x7_FFFF, value, ex1Ptr);
            } else if (addr < 0x1f80_0400) {
                write(addr & 0x3FF, value, scrathpadPtr);
            } else if (addr < 0x1F80_1040) {
                write(addr & 0x3F, value, memoryControl1);
            } else if (addr < 0x1F80_1050) {
                joypad.write(addr, value);
            } else if (addr < 0x1F80_1060) {
                Console.WriteLine($"[BUS] Write Unsupported to SIO address: {addr:x8} : {value:x8}");
                write(addr & 0xF, value, sio);
            } else if (addr < 0x1F80_1070) {
                write(addr & 0xF, value, memoryControl2);
            } else if (addr < 0x1F801080) {
                interruptController.write(addr, value);
            } else if (addr < 0x1F80_1100) {
                dma.write(addr, value);
            } else if (addr < 0x1F80_1140) {
                timers.write(addr, value);
            } else if (addr < 0x1F80_1810) {
                cdrom.write(addr, value);
            } else if (addr < 0x1F80_1820) {
                gpu.write(addr, value);
            } else if (addr < 0x1F80_1830) {
                mdec.write(addr, value);
            } else if (addr < 0x1F802000) {
                spu.write(addr, (ushort)value);
            } else if (addr == 0xFFFE0130) {
                memoryCache = value;
            } else {
                Console.WriteLine($"[BUS] Write16 Unsupported: {addr:x8}");
            }
        }

        public unsafe void write8(uint address, byte value) {
            uint i = address >> 29;
            uint addr = address & RegionMask[i];
            if (addr < 0x1F00_0000) {
                write(addr & 0x1F_FFFF, value, ramPtr);
            } else if (addr < 0x1F80_0000) {
                write(addr & 0x7_FFFF, value, ex1Ptr);
            } else if (addr < 0x1f80_0400) {
                write(addr & 0x3FF, value, scrathpadPtr);
            } else if (addr < 0x1F80_1040) {
                write(addr & 0x3F, value, memoryControl1);
            } else if (addr < 0x1F80_1050) {
                joypad.write(addr, value);
            } else if (addr < 0x1F80_1060) {
                Console.WriteLine($"[BUS] Write Unsupported to SIO address: {addr:x8} : {value:x8}");
                write(addr & 0xF, value, sio);
            } else if (addr < 0x1F80_1070) {
                write(addr & 0xF, value, memoryControl2);
            } else if (addr < 0x1F801080) {
                interruptController.write(addr, value);
            } else if (addr < 0x1F80_1100) {
                dma.write(addr, value);
            } else if (addr < 0x1F80_1140) {
                timers.write(addr, value);
            } else if (addr < 0x1F80_1810) {
                cdrom.write(addr, value);
            } else if (addr < 0x1F80_1820) {
                gpu.write(addr, value);
            } else if (addr < 0x1F80_1830) {
                mdec.write(addr, value);
            } else if (addr < 0x1F802000) {
                spu.write(addr, (ushort)value);
            } else if (addr == 0xFFFE0130) {
                memoryCache = value;
            } else {
                Console.WriteLine($"[BUS] Write8 Unsupported: {addr:x8}");
            }
        }

        private uint maskAddr(uint addr) {
            uint i = addr >> 29;
            return addr & RegionMask[i];
        }

        internal unsafe void loadBios() {
            byte[] rom = File.ReadAllBytes(bios);
            Marshal.Copy(rom, 0, (IntPtr)biosPtr, rom.Length);
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

        public unsafe void loadEXE(String fileName) {
            byte[] exe = File.ReadAllBytes(fileName);
            uint PC = Unsafe.As<byte, uint>(ref exe[0x10]);
            uint R28 = Unsafe.As<byte, uint>(ref exe[0x14]);
            uint R29 = Unsafe.As<byte, uint>(ref exe[0x30]);
            uint R30 = R29; //base
            R30 += Unsafe.As<byte, uint>(ref exe[0x34]); //offset

            uint DestAdress = Unsafe.As<byte, uint>(ref exe[0x18]);

            Console.WriteLine($"SideLoading PSX EXE: PC {PC:x8} R28 {R28:x8} R29 {R29:x8} R30 {R30:x8}");

            Marshal.Copy(exe, 0x800, (IntPtr)(ramPtr + (DestAdress & 0x1F_FFFF)), exe.Length - 0x800);

            // Patch Bios LoadRunShell() at 0xBFC06FF0 before the jump to 0x80030000 so we don't poll the address every cycle
            // Instructions are LUI and ORI duos that load to the specified register but PC that loads to R8/Temp0
            // The last 2 instr are a JR to R8 and a NOP.
            write(0x6FF0 +  0, 0x3C080000 | PC >> 16, biosPtr);
            write(0x6FF0 +  4, 0x35080000 | PC & 0xFFFF, biosPtr);

            write(0x6FF0 +  8, 0x3C1C0000 | R28 >> 16, biosPtr);
            write(0x6FF0 + 12, 0x379C0000 | R28 & 0xFFFF, biosPtr);

            if(R29 != 0) {
                write(0x6FF0 + 16, 0x3C1D0000 | R29 >> 16, biosPtr);
                write(0x6FF0 + 20, 0x37BD0000 | R29 & 0xFFFF, biosPtr);

                write(0x6FF0 + 24, 0x3C1E0000 | R30 >> 16, biosPtr);
                write(0x6FF0 + 28, 0x37DE0000 | R30 & 0xFFFF, biosPtr);

                write(0x6FF0 + 32, 0x01000008, biosPtr);
                write(0x6FF0 + 36, 0x00000000, biosPtr);
            } else {
                write(0x6FF0 + 16, 0x01000008, biosPtr);
                write(0x6FF0 + 20, 0x00000000, biosPtr);
            }
        }

        public unsafe void loadEXP() {
            byte[] exe = File.ReadAllBytes(ex1);
            Marshal.Copy(exe, 0, (IntPtr)ex1Ptr, exe.Length);

            write32(0x1F02_0018, 0x1); //Enable exp flag
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void tick(int cycles) {
            if (gpu.tick(cycles)) interruptController.set(Interrupt.VBLANK);
            if (cdrom.tick(cycles)) interruptController.set(Interrupt.CDROM);
            if (dma.tick()) interruptController.set(Interrupt.DMA);

            timers.syncGPU(gpu.getBlanksAndDot());

            if (timers.tick(0, cycles)) interruptController.set(Interrupt.TIMER0);
            if (timers.tick(1, cycles)) interruptController.set(Interrupt.TIMER1);
            if (timers.tick(2, cycles)) interruptController.set(Interrupt.TIMER2);
            if (joypad.tick()) interruptController.set(Interrupt.CONTR);
            if (spu.tick(cycles)) interruptController.set(Interrupt.SPU);
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
        public unsafe uint LoadFromRam(uint addr) {
            return *(uint*)(ramPtr + (addr & 0x1F_FFFF));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe uint LoadFromBios(uint addr) {
            return *(uint*)(biosPtr + (addr & 0x7_FFFF));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Span<uint> DmaFromRam(uint addr, uint size) {
            return new Span<uint>(ramPtr + (addr & 0x1F_FFFF), (int)size);
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
        public void DmaFromGpu(uint address, int size) { //todo handle the whole array/span
            for (int i = 0; i < size; i++) {
                var word = gpu.loadGPUREAD();
                DmaToRam(address, word);
                address += 4;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DmaToGpu(Span<uint> buffer) {
            var array = buffer.ToArray();
            //Task.Run(() => gpu.processDma(array));
            gpu.processDma(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DmaFromCD(uint address, int size) { //todo handle the whole array/span
            for(int i = 0; i < size; i++) {
                var word = cdrom.getData();
                DmaToRam(address, word);
                address += 4;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DmaToMdecIn(Span<uint> dma) { //todo: actual process the whole array
            foreach (uint word in dma)
                mdec.writeMDEC0_Command(word);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void DmaFromMdecOut(uint address, int size) {
            var dma = mdec.processDmaLoad(size);
            var dest = new Span<uint>(ramPtr + (address & 0x1F_FFFC), size);
            dma.CopyTo(dest);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DmaToSpu(Span<uint> dma) {
            spu.processDmaWrite(dma);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void DmaFromSpu(uint address, int size) {
            var dma = spu.processDmaLoad(size);
            var dest = new Span<uint>(ramPtr + (address & 0x1F_FFFC), size);
            dma.CopyTo(dest);
        }
        public unsafe void DmaOTC(uint baseAddress, int size) {
            //uint destAddress = (uint)(baseAddress - ((size - 1) * 4));
            //
            //Span<uint> dma = stackalloc uint[size];
            //
            //for (int i = dma.Length - 1; i > 0; i--) {
            //baseAddress -= 4;
            //dma[i] = baseAddress & 0xFF_FFFF;
            //}
            //
            //dma[0] = 0xFF_FFFF;
            //
            //var dest = new Span<uint>(ramPtr + (destAddress & 0x1F_FFFC), size);
            //dma.CopyTo(dest);

            for(int i = 0; i < size - 1; i++) {
                DmaToRam(baseAddress, baseAddress - 4);
                baseAddress -= 4;
            }
            
            DmaToRam(baseAddress, 0xFF_FFFF);
        }

        private static uint[] RegionMask = {
        0xFFFF_FFFF, 0xFFFF_FFFF, 0xFFFF_FFFF, 0xFFFF_FFFF, // KUSEG: 2048MB
        0x7FFF_FFFF,                                        // KSEG0:  512MB
        0x1FFF_FFFF,                                        // KSEG1:  512MB
        0xFFFF_FFFF, 0xFFFF_FFFF,                           // KSEG2: 1024MB
        };
    }
}
