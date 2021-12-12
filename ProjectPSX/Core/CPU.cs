//#define CPU_EXCEPTIONS
using System;
using System.Runtime.CompilerServices;
using ProjectPSX.Disassembler;

namespace ProjectPSX {
    internal unsafe class CPU {  //MIPS R3000A-compatible 32-bit RISC CPU MIPS R3051 with 5 KB L1 cache, running at 33.8688 MHz // 33868800

        private uint PC_Now; // PC on current execution as PC and PC Predictor go ahead after fetch. This is handy on Branch Delay so it dosn't give erronious PC-4
        private uint PC = 0xbfc0_0000; // Bios Entry Point
        private uint PC_Predictor = 0xbfc0_0004; //next op for branch delay slot emulation

        private uint[] GPR = new uint[32];
        private uint HI;
        private uint LO;

        private bool opcodeIsBranch;
        private bool opcodeIsDelaySlot;

        private bool opcodeTookBranch;
        private bool opcodeInDelaySlotTookBranch;

        private static uint[] ExceptionAdress = new uint[] { 0x8000_0080, 0xBFC0_0180 };

        //CoPro Regs
        private uint[] COP0_GPR = new uint[16];
        private const int SR = 12;
        private const int CAUSE = 13;
        private const int EPC = 14;
        private const int BADA = 8;
        private const int JUMPDEST = 6;

        private bool dontIsolateCache;

        private GTE gte;
        private BUS bus;

        private BIOS_Disassembler bios;
        private MIPS_Disassembler mips;

        private struct MEM {
            public uint register;
            public uint value;
        }
        private MEM writeBack;
        private MEM memoryLoad;
        private MEM delayedMemoryLoad;

        public struct Instr {
            public uint value;                     //raw
            public uint opcode => value >> 26;     //Instr opcode

            //I-Type
            public uint rs => (value >> 21) & 0x1F;//Register Source
            public uint rt => (value >> 16) & 0x1F;//Register Target
            public uint imm => value & 0xFFFF;     //Immediate value
            public uint imm_s => (uint)(short)imm; //Immediate value sign extended

            //R-Type
            public uint rd => (value >> 11) & 0x1F;
            public uint sa => (value >> 6) & 0x1F;  //Shift Amount
            public uint function => value & 0x3F;   //Function

            //J-Type                                       
            public uint addr => value & 0x3FFFFFF;  //Target Address

            //id / Cop
            public uint id => opcode & 0x3; //This is used mainly for coprocesor opcode id but its also used on opcodes that trigger exception
        }
        private Instr instr;

        //Debug
        private long cycle; //current CPU cycle counter for debug
        public bool debug = false;

        public CPU(BUS bus) {
            this.bus = bus;
            bios = new BIOS_Disassembler(bus);
            mips = new MIPS_Disassembler(ref HI, ref LO, GPR, COP0_GPR);
            gte = new GTE();

            COP0_GPR[15] = 0x2; //PRID Processor ID

            initOpCodeTable();
        }

        private static delegate*<CPU, void>[] opcodeMainTable;
        private static delegate*<CPU, void>[] opcodeSpecialTable;

        private void initOpCodeTable() {
            static void SPECIAL(CPU cpu) => cpu.SPECIAL();
            static void BCOND(CPU cpu) => cpu.BCOND();
            static void J(CPU cpu) => cpu.J();
            static void JAL(CPU cpu) => cpu.JAL();
            static void BEQ(CPU cpu) => cpu.BEQ();
            static void BNE(CPU cpu) => cpu.BNE();
            static void BLEZ(CPU cpu) => cpu.BLEZ();
            static void BGTZ(CPU cpu) => cpu.BGTZ();
            static void ADDI(CPU cpu) => cpu.ADDI();
            static void ADDIU(CPU cpu) => cpu.ADDIU();
            static void SLTI(CPU cpu) => cpu.SLTI();
            static void SLTIU(CPU cpu) => cpu.SLTIU();
            static void ANDI(CPU cpu) => cpu.ANDI();
            static void ORI(CPU cpu) => cpu.ORI();
            static void XORI(CPU cpu) => cpu.XORI();
            static void LUI(CPU cpu) => cpu.LUI();
            static void COP0(CPU cpu) => cpu.COP0();
            static void NOP(CPU cpu) => cpu.NOP();
            static void COP2(CPU cpu) => cpu.COP2();
            static void NA(CPU cpu) => cpu.NA();
            static void LB(CPU cpu) => cpu.LB();
            static void LH(CPU cpu) => cpu.LH();
            static void LWL(CPU cpu) => cpu.LWL();
            static void LW(CPU cpu) => cpu.LW();
            static void LBU(CPU cpu) => cpu.LBU();
            static void LHU(CPU cpu) => cpu.LHU();
            static void LWR(CPU cpu) => cpu.LWR();
            static void SB(CPU cpu) => cpu.SB();
            static void SH(CPU cpu) => cpu.SH();
            static void SWL(CPU cpu) => cpu.SWL();
            static void SW(CPU cpu) => cpu.SW();
            static void SWR(CPU cpu) => cpu.SWR();
            static void LWC2(CPU cpu) => cpu.LWC2();
            static void SWC2(CPU cpu) => cpu.SWC2();

            opcodeMainTable = new delegate*<CPU, void>[] {
                &SPECIAL,  &BCOND,  &J,      &JAL,    &BEQ,    &BNE,    &BLEZ,   &BGTZ,
                &ADDI,     &ADDIU,  &SLTI,   &SLTIU,  &ANDI,   &ORI,    &XORI,   &LUI,
                &COP0,     &NOP,    &COP2,   &NOP,    &NA,     &NA,     &NA,     &NA,
                &NA,       &NA,     &NA,     &NA,     &NA,     &NA,     &NA,     &NA,
                &LB,       &LH,     &LWL,    &LW,     &LBU,    &LHU,    &LWR,    &NA,
                &SB,       &SH,     &SWL,    &SW,     &NA,     &NA,     &SWR,    &NA,
                &NOP,      &NOP,    &LWC2,   &NOP,    &NA,     &NA,     &NA,     &NA,
                &NOP,      &NOP,    &SWC2,   &NOP,    &NA,     &NA,     &NA,     &NA,
            };

            static void SLL(CPU cpu) => cpu.SLL();
            static void SRL(CPU cpu) => cpu.SRL();
            static void SRA(CPU cpu) => cpu.SRA();
            static void SLLV(CPU cpu) => cpu.SLLV();
            static void SRLV(CPU cpu) => cpu.SRLV();
            static void SRAV(CPU cpu) => cpu.SRAV();
            static void JR(CPU cpu) => cpu.JR();
            static void SYSCALL(CPU cpu) => cpu.SYSCALL();
            static void BREAK(CPU cpu) => cpu.BREAK();
            static void JALR(CPU cpu) => cpu.JALR();
            static void MFHI(CPU cpu) => cpu.MFHI();
            static void MTHI(CPU cpu) => cpu.MTHI();
            static void MFLO(CPU cpu) => cpu.MFLO();
            static void MTLO(CPU cpu) => cpu.MTLO();
            static void MULT(CPU cpu) => cpu.MULT();
            static void MULTU(CPU cpu) => cpu.MULTU();
            static void DIV(CPU cpu) => cpu.DIV();
            static void DIVU(CPU cpu) => cpu.DIVU();
            static void ADD(CPU cpu) => cpu.ADD();
            static void ADDU(CPU cpu) => cpu.ADDU();
            static void SUB(CPU cpu) => cpu.SUB();
            static void SUBU(CPU cpu) => cpu.SUBU();
            static void AND(CPU cpu) => cpu.AND();
            static void OR(CPU cpu) => cpu.OR();
            static void XOR(CPU cpu) => cpu.XOR();
            static void NOR(CPU cpu) => cpu.NOR();
            static void SLT(CPU cpu) => cpu.SLT();
            static void SLTU(CPU cpu) => cpu.SLTU();

            opcodeSpecialTable = new delegate*<CPU, void>[] {
                &SLL,   &NA,    &SRL,   &SRA,   &SLLV,    &NA,     &SRLV, &SRAV,
                &JR,    &JALR,  &NA,    &NA,    &SYSCALL, &BREAK,  &NA,   &NA,
                &MFHI,  &MTHI,  &MFLO,  &MTLO,  &NA,      &NA,     &NA,   &NA,
                &MULT,  &MULTU, &DIV,   &DIVU,  &NA,      &NA,     &NA,   &NA,
                &ADD,   &ADDU,  &SUB,   &SUBU,  &AND,     &OR,     &XOR,  &NOR,
                &NA,    &NA,    &SLT,   &SLTU,  &NA,      &NA,     &NA,   &NA,
                &NA,    &NA,    &NA,    &NA,    &NA,      &NA,     &NA,   &NA,
                &NA,    &NA,    &NA,    &NA,    &NA,      &NA,     &NA,   &NA,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Run() {
            fetchDecode();
            if (instr.value != 0) { //Skip Nops
                opcodeMainTable[instr.opcode](this); //Execute
            }
            MemAccess();
            WriteBack();

            //if (debug) {
            //  mips.PrintRegs();
            //  mips.disassemble(instr, PC_Now, PC_Predictor);
            //}

            //TTY();
            //bios.verbose(PC_Now, GPR);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void handleInterrupts() {
            //Executable address space is limited to ram and bios on psx
            uint maskedPC = PC & 0x1FFF_FFFF;
            uint load;
            if (maskedPC < 0x1F00_0000) {
                load = bus.LoadFromRam(maskedPC);
            } else {
                load = bus.LoadFromBios(maskedPC);
            }

            //This is actually the "next" opcode if it's a GTE one
            //just postpone the interrupt so it doesn't glitch out
            //Crash Bandicoot intro is a good example for this
            uint instr = load >> 26;
            if (instr == 0x12) { //COP2 MTC2
                //Console.WriteLine("WARNING COP2 OPCODE ON INTERRUPT");
                return;
            }

            if (bus.interruptController.interruptPending()) {
                COP0_GPR[CAUSE] |= 0x400;
            } else {
                COP0_GPR[CAUSE] &= ~(uint)0x400;
            }

            bool IEC = (COP0_GPR[SR] & 0x1) == 1;
            uint IM = (COP0_GPR[SR] >> 8) & 0xFF;
            uint IP = (COP0_GPR[CAUSE] >> 8) & 0xFF;

            if (IEC && (IM & IP) > 0) {
                EXCEPTION(EX.INTERRUPT);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void fetchDecode() {
            //Executable address space is limited to ram and bios on psx
            uint maskedPC = PC & 0x1FFF_FFFF;
            uint load;
            if (maskedPC < 0x1F00_0000) {
                load = bus.LoadFromRam(maskedPC);
            } else {
                load = bus.LoadFromBios(maskedPC);
            }

            PC_Now = PC;
            PC = PC_Predictor;
            PC_Predictor += 4;

            opcodeIsDelaySlot = opcodeIsBranch;
            opcodeInDelaySlotTookBranch = opcodeTookBranch;
            opcodeIsBranch = false;
            opcodeTookBranch = false;

#if CPU_EXCEPTIONS
            if ((PC_Now & 0x3) != 0) {
                COP0_GPR[BADA] = PC_Now;
                EXCEPTION(EX.LOAD_ADRESS_ERROR);
                return;
            }
#endif

            instr.value = load;
            //cycle++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MemAccess() {
            if (delayedMemoryLoad.register != memoryLoad.register) { //if loadDelay on same reg it is lost/overwritten (amidog tests)
                GPR[memoryLoad.register] = memoryLoad.value;
            }
            memoryLoad = delayedMemoryLoad;
            delayedMemoryLoad.register = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteBack() {
            GPR[writeBack.register] = writeBack.value;
            writeBack.register = 0;
            GPR[0] = 0;
        }

        // Non Implemented by the CPU Opcodes
        private void NOP() { /*nop*/ }

        private void NA() => EXCEPTION(EX.ILLEGAL_INSTR, instr.id);


        // Main Table Opcodes
        private void SPECIAL() => opcodeSpecialTable[instr.function](this);

        private void BCOND() {
            opcodeIsBranch = true;
            uint op = instr.rt;

            bool should_link = (op & 0x1E) == 0x10;
            bool should_branch = (int)(GPR[instr.rs] ^ (op << 31)) < 0;

            if (should_link) GPR[31] = PC_Predictor;
            if (should_branch) BRANCH();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void J() {
            opcodeIsBranch = true;
            opcodeTookBranch = true;
            PC_Predictor = (PC_Predictor & 0xF000_0000) | (instr.addr << 2);
        }

        private void JAL() {
            setGPR(31, PC_Predictor);
            J();
        }

        private void BEQ() {
            opcodeIsBranch = true;
            if (GPR[instr.rs] == GPR[instr.rt]) {
                BRANCH();
            }
        }

        private void BNE() {
            opcodeIsBranch = true;
            if (GPR[instr.rs] != GPR[instr.rt]) {
                BRANCH();
            }
        }

        private void BLEZ() {
            opcodeIsBranch = true;
            if (((int)GPR[instr.rs]) <= 0) {
                BRANCH();
            }
        }

        private void BGTZ() {
            opcodeIsBranch = true;
            if (((int)GPR[instr.rs]) > 0) {
                BRANCH();
            }
        }

        private void ADDI() {
            uint rs = GPR[instr.rs];
            uint imm_s = instr.imm_s;
            uint result = rs + imm_s;

#if CPU_EXCEPTIONS
            if(checkOverflow(rs, imm_s, result)) {
                EXCEPTION(EX.OVERFLOW, instr.id);
            } else {
                setGPR(instr.rt, result);
            }
#else
            setGPR(instr.rt, result);
#endif
        }

        private void ADDIU() => setGPR(instr.rt, GPR[instr.rs] + instr.imm_s);

        private void SLTI() {
            bool condition = (int)GPR[instr.rs] < (int)instr.imm_s;
            setGPR(instr.rt, Unsafe.As<bool, uint>(ref condition));
        }

        private void SLTIU() {
            bool condition = GPR[instr.rs] < instr.imm_s;
            setGPR(instr.rt, Unsafe.As<bool, uint>(ref condition));
        }

        private void ANDI() => setGPR(instr.rt, GPR[instr.rs] & instr.imm);

        private void ORI() => setGPR(instr.rt, GPR[instr.rs] | instr.imm);

        private void XORI() => setGPR(instr.rt, GPR[instr.rs] ^ instr.imm);

        private void LUI() => setGPR(instr.rt, instr.imm << 16);

        private void COP0() {
            if (instr.rs == 0b0_0000) MFC0();
            else if (instr.rs == 0b0_0100) MTC0();
            else if (instr.rs == 0b1_0000) RFE();
            else EXCEPTION(EX.ILLEGAL_INSTR, instr.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MFC0() {
            uint mfc = instr.rd;
            if (mfc == 3 || mfc >= 5 && mfc <= 9 || mfc >= 11 && mfc <= 15) {
                delayedLoad(instr.rt, COP0_GPR[mfc]);
            } else {
                EXCEPTION(EX.ILLEGAL_INSTR, instr.id);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MTC0() {
            uint value = GPR[instr.rt];
            uint register = instr.rd;

            if (register == CAUSE) { //only bits 8 and 9 are writable
                COP0_GPR[CAUSE] &= ~(uint)0x300;
                COP0_GPR[CAUSE] |= value & 0x300;
            } else if (register == SR) {
                //This can trigger soft interrupts
                dontIsolateCache = (value & 0x10000) == 0;
                bool prevIEC = (COP0_GPR[SR] & 0x1) == 1;
                bool currentIEC = (value & 0x1) == 1;

                COP0_GPR[SR] = value;

                uint IM = (value >> 8) & 0x3;
                uint IP = (COP0_GPR[CAUSE] >> 8) & 0x3;

                if (!prevIEC && currentIEC && (IM & IP) > 0) {
                    PC = PC_Predictor;
                    EXCEPTION(EX.INTERRUPT, instr.id);
                }

            } else {
                COP0_GPR[register] = value;
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RFE() {
            uint mode = COP0_GPR[SR] & 0x3F;
            COP0_GPR[SR] &= ~(uint)0xF;
            COP0_GPR[SR] |= mode >> 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EXCEPTION(EX cause, uint coprocessor = 0) {
            uint mode = COP0_GPR[SR] & 0x3F;
            COP0_GPR[SR] &= ~(uint)0x3F;
            COP0_GPR[SR] |= (mode << 2) & 0x3F;

            uint OldCause = COP0_GPR[CAUSE] & 0xff00;
            COP0_GPR[CAUSE] = (uint)cause << 2;
            COP0_GPR[CAUSE] |= OldCause;
            COP0_GPR[CAUSE] |= coprocessor << 28;

            if (cause == EX.INTERRUPT) {
                COP0_GPR[EPC] = PC;
                //hack: related to the delay of the ex interrupt
                opcodeIsDelaySlot = opcodeIsBranch;
                opcodeInDelaySlotTookBranch = opcodeTookBranch;
            } else {
                COP0_GPR[EPC] = PC_Now;
            }

            if (opcodeIsDelaySlot) {
                COP0_GPR[EPC] -= 4;
                COP0_GPR[CAUSE] |= (uint)1 << 31;
                COP0_GPR[JUMPDEST] = PC;

                if (opcodeInDelaySlotTookBranch) {
                    COP0_GPR[CAUSE] |= (1 << 30);
                }
            }

            PC = ExceptionAdress[COP0_GPR[SR] & 0x400000 >> 22];
            PC_Predictor = PC + 4;
        }

        private void COP2() {
            if ((instr.rs & 0x10) == 0) {
                switch (instr.rs) {
                    case 0b0_0000: MFC2(); break;
                    case 0b0_0010: CFC2(); break;
                    case 0b0_0100: MTC2(); break;
                    case 0b0_0110: CTC2(); break;
                    default: EXCEPTION(EX.ILLEGAL_INSTR, instr.id); break;
                }
            } else {
                gte.execute(instr.value);
            }
        }

        private void MFC2() => delayedLoad(instr.rt, gte.loadData(instr.rd));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CFC2() => delayedLoad(instr.rt, gte.loadControl(instr.rd));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MTC2() => gte.writeData(instr.rd, GPR[instr.rt]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CTC2() => gte.writeControl(instr.rd, GPR[instr.rt]);

        private void LWC2() { //TODO WARNING THIS SHOULD HAVE DELAY?
            uint addr = GPR[instr.rs] + instr.imm_s;

#if CPU_EXCEPTIONS
            if ((addr & 0x3) == 0) {
                uint value = bus.load32(addr);
                gte.writeData(instr.rt, value);
            } else {
                COP0_GPR[BADA] = addr;
                EXCEPTION(EX.LOAD_ADRESS_ERROR, instr.id);
            }
#else
            uint value = bus.load32(addr);
            gte.writeData(instr.rt, value);
#endif
        }

        private void SWC2() { //TODO WARNING THIS SHOULD HAVE DELAY?
            uint addr = GPR[instr.rs] + instr.imm_s;

#if CPU_EXCEPTIONS
            if ((addr & 0x3) == 0) {
                bus.write32(addr, gte.loadData(instr.rt));
            } else {
                COP0_GPR[BADA] = addr;
                EXCEPTION(EX.LOAD_ADRESS_ERROR, instr.id);
            }
#else
            bus.write32(addr, gte.loadData(instr.rt));
#endif
        }

        private void LB() { //todo redo this as it unnecesary load32
            if (dontIsolateCache) {
                uint value = (uint)(sbyte)bus.load32(GPR[instr.rs] + instr.imm_s);
                delayedLoad(instr.rt, value);
            } //else Console.WriteLine("IsolatedCache: Ignoring Load");
        }

        private void LBU() {
            if (dontIsolateCache) {
                uint value = (byte)bus.load32(GPR[instr.rs] + instr.imm_s);
                delayedLoad(instr.rt, value);
            } //else Console.WriteLine("IsolatedCache: Ignoring Load");
        }

        private void LH() {
            if (dontIsolateCache) {
                uint addr = GPR[instr.rs] + instr.imm_s;

#if CPU_EXCEPTIONS
                if ((addr & 0x1) == 0) {
                    uint value = (uint)(short)bus.load32(addr);
                    delayedLoad(instr.rt, value);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.LOAD_ADRESS_ERROR, instr.id);
                }
#else
                uint value = (uint)(short)bus.load32(addr);
                delayedLoad(instr.rt, value);
#endif

            } //else Console.WriteLine("IsolatedCache: Ignoring Load");
        }

        private void LHU() {
            if (dontIsolateCache) {
                uint addr = GPR[instr.rs] + instr.imm_s;

#if CPU_EXCEPTIONS
                if ((addr & 0x1) == 0) {
                    uint value = (ushort)bus.load32(addr);
                    delayedLoad(instr.rt, value);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.LOAD_ADRESS_ERROR, instr.id);
                }
#else
                uint value = (ushort)bus.load32(addr);
                delayedLoad(instr.rt, value);
#endif

            } //else Console.WriteLine("IsolatedCache: Ignoring Load");
        }

        private void LW() {
            if (dontIsolateCache) {
                uint addr = GPR[instr.rs] + instr.imm_s;

#if CPU_EXCEPTIONS
                if ((addr & 0x3) == 0) {
                    uint value = bus.load32(addr);
                    delayedLoad(instr.rt, value);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.LOAD_ADRESS_ERROR, instr.id);
                }
#else
                uint value = bus.load32(addr);
                delayedLoad(instr.rt, value);
#endif

            } //else Console.WriteLine("IsolatedCache: Ignoring Load");
        }

        private void LWL() {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = addr & 0xFFFF_FFFC;
            uint aligned_load = bus.load32(aligned_addr);

            uint value = 0;
            uint LRValue = GPR[instr.rt];

            if (instr.rt == memoryLoad.register) {
                LRValue = memoryLoad.value;
            }

            switch (addr & 0b11) {
                case 0: value = (LRValue & 0x00FF_FFFF) | (aligned_load << 24); break;
                case 1: value = (LRValue & 0x0000_FFFF) | (aligned_load << 16); break;
                case 2: value = (LRValue & 0x0000_00FF) | (aligned_load << 8); break;
                case 3: value = aligned_load; break;
            }

            delayedLoad(instr.rt, value);
        }

        private void LWR() {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = addr & 0xFFFF_FFFC;
            uint aligned_load = bus.load32(aligned_addr);

            uint value = 0;
            uint LRValue = GPR[instr.rt];

            if (instr.rt == memoryLoad.register) {
                LRValue = memoryLoad.value;
            }

            switch (addr & 0b11) {
                case 0: value = aligned_load; break;
                case 1: value = (LRValue & 0xFF00_0000) | (aligned_load >> 8); break;
                case 2: value = (LRValue & 0xFFFF_0000) | (aligned_load >> 16); break;
                case 3: value = (LRValue & 0xFFFF_FF00) | (aligned_load >> 24); break;
            }

            delayedLoad(instr.rt, value);
        }

        private void SB() {
            if (dontIsolateCache)
                bus.write8(GPR[instr.rs] + instr.imm_s, (byte)GPR[instr.rt]);
            //else Console.WriteLine("IsolatedCache: Ignoring Write");
        }

        private void SH() {
            if (dontIsolateCache) {
                uint addr = GPR[instr.rs] + instr.imm_s;

#if CPU_EXCEPTIONS
                if ((addr & 0x1) == 0) {
                    bus.write16(addr, (ushort)GPR[instr.rt]);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.STORE_ADRESS_ERROR, instr.id);
                }
#else
                bus.write16(addr, (ushort)GPR[instr.rt]);
#endif
            } //else Console.WriteLine("IsolatedCache: Ignoring Write");
        }

        private void SW() {
            if (dontIsolateCache) {
                uint addr = GPR[instr.rs] + instr.imm_s;

#if CPU_EXCEPTIONS
                if ((addr & 0x3) == 0) {
                    bus.write32(addr, GPR[instr.rt]);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.STORE_ADRESS_ERROR, instr.id);
                }
#else
                bus.write32(addr, GPR[instr.rt]);
#endif
            } //else Console.WriteLine("IsolatedCache: Ignoring Write");
        }

        private void SWR() {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = addr & 0xFFFF_FFFC;
            uint aligned_load = bus.load32(aligned_addr);

            uint value = 0;
            switch (addr & 0b11) {
                case 0: value = GPR[instr.rt]; break;
                case 1: value = (aligned_load & 0x0000_00FF) | (GPR[instr.rt] << 8); break;
                case 2: value = (aligned_load & 0x0000_FFFF) | (GPR[instr.rt] << 16); break;
                case 3: value = (aligned_load & 0x00FF_FFFF) | (GPR[instr.rt] << 24); break;
            }

            bus.write32(aligned_addr, value);
        }

        private void SWL() {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = addr & 0xFFFF_FFFC;
            uint aligned_load = bus.load32(aligned_addr);

            uint value = 0;
            switch (addr & 0b11) {
                case 0: value = (aligned_load & 0xFFFF_FF00) | (GPR[instr.rt] >> 24); break;
                case 1: value = (aligned_load & 0xFFFF_0000) | (GPR[instr.rt] >> 16); break;
                case 2: value = (aligned_load & 0xFF00_0000) | (GPR[instr.rt] >> 8); break;
                case 3: value = GPR[instr.rt]; break;
            }

            bus.write32(aligned_addr, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BRANCH() {
            opcodeTookBranch = true;
            PC_Predictor = PC + (instr.imm_s << 2);
        }


        // Special Table Opcodes (Nested on Opcode 0x00 with additional function param)

        private void SLL() => setGPR(instr.rd, GPR[instr.rt] << (int)instr.sa);

        private void SRL() => setGPR(instr.rd, GPR[instr.rt] >> (int)instr.sa);

        private void SRA() => setGPR(instr.rd, (uint)((int)GPR[instr.rt] >> (int)instr.sa));

        private void SLLV() => setGPR(instr.rd, GPR[instr.rt] << (int)(GPR[instr.rs] & 0x1F));

        private void SRLV() => setGPR(instr.rd, GPR[instr.rt] >> (int)(GPR[instr.rs] & 0x1F));

        private void SRAV() => setGPR(instr.rd, (uint)((int)GPR[instr.rt] >> (int)(GPR[instr.rs] & 0x1F)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void JR() {
            opcodeIsBranch = true;
            opcodeTookBranch = true;
            PC_Predictor = GPR[instr.rs];
        }

        private void SYSCALL() => EXCEPTION(EX.SYSCALL, instr.id);

        private void BREAK() => EXCEPTION(EX.BREAK);

        private void JALR() {
            setGPR(instr.rd, PC_Predictor);
            JR();
        }

        private void MFHI() => setGPR(instr.rd, HI);

        private void MTHI() => HI = GPR[instr.rs];

        private void MFLO() => setGPR(instr.rd, LO);

        private void MTLO() => LO = GPR[instr.rs];

        private void MULT() {
            long value = (long)(int)GPR[instr.rs] * (long)(int)GPR[instr.rt]; //sign extend to pass amidog cpu test

            HI = (uint)(value >> 32);
            LO = (uint)value;
        }

        private void MULTU() {
            ulong value = (ulong)GPR[instr.rs] * (ulong)GPR[instr.rt]; //sign extend to pass amidog cpu test

            HI = (uint)(value >> 32);
            LO = (uint)value;
        }

        private void DIV() {
            int n = (int)GPR[instr.rs];
            int d = (int)GPR[instr.rt];

            if (d == 0) {
                HI = (uint)n;
                if (n >= 0) {
                    LO = 0xFFFF_FFFF;
                } else {
                    LO = 1;
                }
            } else if ((uint)n == 0x8000_0000 && d == -1) {
                HI = 0;
                LO = 0x8000_0000;
            } else {
                HI = (uint)(n % d);
                LO = (uint)(n / d);
            }
        }

        private void DIVU() {
            uint n = GPR[instr.rs];
            uint d = GPR[instr.rt];

            if (d == 0) {
                HI = n;
                LO = 0xFFFF_FFFF;
            } else {
                HI = n % d;
                LO = n / d;
            }
        }

        private void ADD() {
            uint rs = GPR[instr.rs];
            uint rt = GPR[instr.rt];
            uint result = rs + rt;

#if CPU_EXCEPTIONS
            if (checkOverflow(rs, rt, result)) {
                EXCEPTION(EX.OVERFLOW, instr.id);
            } else {
                setGPR(instr.rd, result);
            }
#else
            setGPR(instr.rd, result);
#endif
        }

        private void ADDU() => setGPR(instr.rd, GPR[instr.rs] + GPR[instr.rt]);

        private void SUB() {
            uint rs = GPR[instr.rs];
            uint rt = GPR[instr.rt];
            uint result = rs - rt;

#if CPU_EXCEPTIONS
            if (checkUnderflow(rs, rt, result)) {
                EXCEPTION(EX.OVERFLOW, instr.id);
            } else {
                setGPR(instr.rd, result);
            }
#else
            setGPR(instr.rd, result);
#endif
        }

        private void SUBU() => setGPR(instr.rd, GPR[instr.rs] - GPR[instr.rt]);

        private void AND() => setGPR(instr.rd, GPR[instr.rs] & GPR[instr.rt]);

        private void OR() => setGPR(instr.rd, GPR[instr.rs] | GPR[instr.rt]);

        private void XOR() => setGPR(instr.rd, GPR[instr.rs] ^ GPR[instr.rt]);

        private void NOR() => setGPR(instr.rd, ~(GPR[instr.rs] | GPR[instr.rt]));

        private void SLT() {
            bool condition = (int)GPR[instr.rs] < (int)GPR[instr.rt];
            setGPR(instr.rd, Unsafe.As<bool, uint>(ref condition));
        }

        private void SLTU() {
            bool condition = GPR[instr.rs] < GPR[instr.rt];
            setGPR(instr.rd, Unsafe.As<bool, uint>(ref condition));
        }


        // Accesory methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool checkOverflow(uint a, uint b, uint r) => ((r ^ a) & (r ^ b) & 0x8000_0000) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool checkUnderflow(uint a, uint b, uint r) => ((r ^ a) & (a ^ b) & 0x8000_0000) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void setGPR(uint regN, uint value) {
            writeBack.register = regN;
            writeBack.value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void delayedLoad(uint regN, uint value) {
            delayedMemoryLoad.register = regN;
            delayedMemoryLoad.value = value;
        }

        private void TTY() {
            if (PC == 0x00000B0 && GPR[9] == 0x3D || PC == 0x00000A0 && GPR[9] == 0x3C) {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.Write((char)GPR[4]);
                Console.ResetColor();
            }
        }

    }
}
