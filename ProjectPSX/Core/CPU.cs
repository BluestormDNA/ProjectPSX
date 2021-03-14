using System;
using System.Runtime.CompilerServices;
using ProjectPSX.Disassembler;

namespace ProjectPSX {
    internal unsafe class CPU {  //MIPS R3000A-compatible 32-bit RISC CPU MIPS R3051 with 5 KB L1 cache, running at 33.8688 MHz // 33868800

        private BUS bus;

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

        //GTE
        private GTE gte;

        //Debug
        private long cycle; //current CPU cycle counter for debug
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

        //debug expansion and exe
        public bool debug = false;
        private bool isEX1 = true;
        private bool exe = true;

        public CPU(BUS bus) {
            this.bus = bus;
            bios = new BIOS_Disassembler(bus);
            mips = new MIPS_Disassembler(ref HI, ref LO, GPR, COP0_GPR);
            gte = new GTE();

            COP0_GPR[15] = 0x2; //PRID Processor ID

            initOpCodeTable();
        }

        public static delegate*<CPU, void>[] opcodeMainTable;
        public static delegate*<CPU, void>[] opcodeSpecialTable;

        public void initOpCodeTable() {
            static void SPECIAL2(CPU cpu) => cpu.SPECIAL();
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
                &SPECIAL2, &BCOND,  &J,      &JAL,    &BEQ,    &BNE,    &BLEZ,   &BGTZ,
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

        private void SPECIAL() => opcodeSpecialTable[instr.function](this);

        private void NOP() { /*nop*/ }

        private void NA() => EXCEPTION(EX.ILLEGAL_INSTR, instr.id);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Run() {
            fetchDecode();
            if(instr.value != 0) { //Skip Nops
                //Execute();
                opcodeMainTable[instr.opcode](this);
            }
            MemAccess();
            WriteBack();

            /*debug*/
            //if (exe) forceTest(demo); //tcpu tcpx tgte tgpu demo <---------------------
            //if (isEX1) forceEX1();

            //if (debug) {
            //mips.PrintRegs();
            //mips.disassemble(instr, PC_Now, PC_Predictor);
            //}

            //TTY();
            //bios.verbose(PC_Now, GPR);
        }

        string tcpu = "./psxtest_cpu.exe";
        string tcpx = "./psxtest_cpx.exe";
        string tgte = "./psxtest_gte.exe";
        string tgpu = "./psxtest_gpu.exe";
        string demo = "./bench.exe";
        private void forceTest(string test) {
            if (PC == 0x8003_0000 && exe == true) {
                (uint _PC, uint R28, uint R29, uint R30) = bus.loadEXE(test);
                Console.WriteLine($"SideLoading PSX EXE: PC {PC:x8} R28 {R28:x8} R29 {R29:x8} R30 {R30:x8}");
                GPR[28] = R28;

                if(R29 != 0) {
                    GPR[29] = R29;
                    GPR[30] = R30;
                }

                PC = _PC;
                PC_Predictor = PC + 4;

                //debug = true;
                exe = false;
            }
        }

        private void forceEX1() {
            bus.loadEXP();
            bus.write32(0x1F02_0018, 0x1);
            isEX1 = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void handleInterrupts() {
            //Executable address space is limited to ram and bios on psx
            uint maskedPC = PC & 0x1FFF_FFFF;
            uint load;
            if (maskedPC < 0x1F00_0000) {
                load = bus.LoadFromRam(maskedPC);
            }
            else {
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

            if ((PC_Now & 0x3) != 0) { // faster than PC_Now % 4 != 0
                COP0_GPR[BADA] = PC_Now; //TODO check this
                EXCEPTION(EX.LOAD_ADRESS_ERROR);
                return;
            }

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

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private void Execute() {
        //    switch (instr.opcode) {
        //        case 0b00_0000: SPECIAL(); break;//R-Type opcodes
        //        case 0b10_1011: SW(); break;
        //        case 0b10_0011: LW(); break;
        //        case 0b00_0100: BEQ(); break;
        //        case 0b00_0101: BNE(); break;
        //        case 0b00_0001: BCOND(); break;
        //        case 0b00_0010: J(); break;
        //        case 0b00_0011: JAL(); break;
        //        case 0b01_0010: COP2(); break;
        //
        //        case 0b00_0110: BLEZ(); break;
        //        case 0b00_0111: BGTZ(); break;
        //        case 0b00_1000: ADDI(); break;
        //        case 0b00_1001: ADDIU(); break;
        //        case 0b00_1010: SLTI(); break;
        //        case 0b00_1011: SLTIU(); break;
        //        case 0b00_1100: ANDI(); break;
        //        case 0b00_1101: ORI(); break;
        //        case 0b00_1110: XORI(); break;
        //        case 0b00_1111: LUI(); break;
        //        case 0b01_0000: COP0(); break;
        //        case 0b01_0001: /*COP1()*/ break;
        //
        //        case 0b01_0011: /*COP3()*/ break;
        //        case 0b10_0000: LB(); break;
        //        case 0b10_0001: LH(); break;
        //        case 0b10_0010: LWL(); break;
        //
        //        case 0b10_0100: LBU(); break;
        //        case 0b10_0101: LHU(); break;
        //        case 0b10_0110: LWR(); break;
        //        case 0b10_1000: SB(); break;
        //        case 0b10_1001: SH(); break;
        //        case 0b10_1010: SWL(); break;
        //
        //        case 0b10_1110: SWR(); break;
        //        case 0b11_0000: //LWC0
        //        case 0b11_0001: //LWC1
        //        case 0b11_0011: //LWC3
        //        case 0b11_1000: //SWC0
        //        case 0b11_1001: //SWC1
        //        case 0b11_1011: /*SWC3*/break; //All copro and lw sw instr dont trigger exception.
        //        case 0b11_0010: LWC2(); break;
        //        case 0b11_1010: SWC2(); break;
        //        default:
        //            EXCEPTION(EX.ILLEGAL_INSTR, instr.id);
        //            break;
        //    }
        //}

        //private void SPECIAL() {
        //    switch (instr.function) {
        //        case 0b00_0000: SLL(); break;
        //        case 0b00_0010: SRL(); break;
        //        case 0b00_0011: SRA(); break;
        //        case 0b00_0100: SLLV(); break;
        //        case 0b00_0110: SRLV(); break;
        //        case 0b00_0111: SRAV(); break;
        //        case 0b00_1000: JR(); break;
        //        case 0b00_1001: JALR(); break;
        //        case 0b00_1100: SYSCALL(); break;
        //        case 0b00_1101: BREAK(); break;
        //        case 0b01_0000: MFHI(); break;
        //        case 0b01_0001: MTHI(); break;
        //        case 0b01_0010: MFLO(); break;
        //        case 0b01_0011: MTLO(); break;
        //        case 0b01_1000: MULT(); break;
        //        case 0b01_1001: MULTU(); break;
        //        case 0b01_1010: DIV(); break;
        //        case 0b01_1011: DIVU(); break;
        //        case 0b10_0000: ADD(); break;
        //        case 0b10_0001: ADDU(); break;
        //        case 0b10_0010: SUB(); break;
        //        case 0b10_0011: SUBU(); break;
        //        case 0b10_0100: AND(); break;
        //        case 0b10_0101: OR(); break;
        //        case 0b10_0110: XOR(); break;
        //        case 0b10_0111: NOR(); break;
        //        case 0b10_1010: SLT(); break;
        //        case 0b10_1011: SLTU(); break;
        //        default:
        //            EXCEPTION(EX.ILLEGAL_INSTR, instr.id);
        //            break;
        //    }
        //}

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

        private void COP0() {
            if (instr.rs == 0b0_0000) MFC0();
            else if (instr.rs == 0b0_0100) MTC0();
            else if (instr.rs == 0b1_0000) RFE();
            else EXCEPTION(EX.ILLEGAL_INSTR, instr.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BCOND() {
            opcodeIsBranch = true;
            uint op = instr.rt;

            bool should_link = (op & 0x1E) == 0x10;
            bool should_branch = (int)(GPR[instr.rs] ^ (op << 31)) < 0;

            if (should_link) GPR[31] = PC_Predictor;
            if (should_branch) BRANCH();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CTC2() => gte.writeControl(instr.rd, GPR[instr.rt]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MTC2() => gte.writeData(instr.rd, GPR[instr.rt]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CFC2() => delayedLoad(instr.rt, gte.loadControl(instr.rd));

        private void MFC2() => delayedLoad(instr.rt, gte.loadData(instr.rd));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SWC2() { //TODO WARNING THIS SHOULD HAVE DELAY?
            //Console.WriteLine("Store Data FROM GTE");
            uint addr = GPR[instr.rs] + instr.imm_s;

            if ((addr & 0x3) == 0) {
                bus.write32(addr, gte.loadData(instr.rt));
            } else {
                COP0_GPR[BADA] = addr;
                EXCEPTION(EX.LOAD_ADRESS_ERROR, instr.id);
            }
            //Console.ReadLine();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LWC2() { //TODO WARNING THIS SHOULD HAVE DELAY?
            //Console.WriteLine("Load Data TO GTE");
            uint addr = GPR[instr.rs] + instr.imm_s;

            if ((addr & 0x3) == 0) {
                uint value = bus.load32(addr);
                gte.writeData(instr.rt, value);
            } else {
                COP0_GPR[BADA] = addr;
                EXCEPTION(EX.LOAD_ADRESS_ERROR, instr.id);
            }
            //Console.ReadLine();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            //Console.WriteLine("case " + (addr & 0b11) + " LWL Value " + value.ToString("x8"));
            delayedLoad(instr.rt, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void XORI() {
            setGPR(instr.rt, GPR[instr.rs] ^ instr.imm);
        }

        private void SUB() {
            int rs = (int)GPR[instr.rs];
            int rt = (int)GPR[instr.rt];
            try {
                uint sub = (uint)checked(rs - rt);
                setGPR(instr.rd, sub);
            } catch (OverflowException) {
                EXCEPTION(EX.OVERFLOW, instr.id);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MULT() {
            long value = (long)(int)GPR[instr.rs] * (long)(int)GPR[instr.rt]; //sign extend to pass amidog cpu test

            HI = (uint)(value >> 32);
            LO = (uint)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BREAK() {
            EXCEPTION(EX.BREAK);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void XOR() {
            setGPR(instr.rd, GPR[instr.rs] ^ GPR[instr.rt]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MULTU() {
            ulong value = (ulong)GPR[instr.rs] * (ulong)GPR[instr.rt]; //sign extend to pass amidog cpu test

            HI = (uint)(value >> 32);
            LO = (uint)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SRLV() {
            setGPR(instr.rd, GPR[instr.rt] >> (int)(GPR[instr.rs] & 0x1F));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SRAV() {
            setGPR(instr.rd, (uint)((int)GPR[instr.rt] >> (int)(GPR[instr.rs] & 0x1F)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void NOR() {
            setGPR(instr.rd, ~(GPR[instr.rs] | GPR[instr.rt]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LH() {
            if ((COP0_GPR[SR] & 0x10000) == 0) {
                uint addr = GPR[instr.rs] + instr.imm_s;

                if ((addr & 0x1) == 0) {
                    uint value = (uint)(short)bus.load32(addr);
                    delayedLoad(instr.rt, value);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.LOAD_ADRESS_ERROR, instr.id);
                }

            } //else Console.WriteLine("Ignoring Load");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SLLV() {
            setGPR(instr.rd, GPR[instr.rt] << (int)(GPR[instr.rs] & 0x1F));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LHU() {
            if ((COP0_GPR[SR] & 0x10000) == 0) {
                uint addr = GPR[instr.rs] + instr.imm_s;

                if ((addr & 0x1) == 0) {
                    uint value = (ushort)bus.load32(addr);
                    //Console.WriteLine("LHU: " + addr.ToString("x8") + " value: " + value.ToString("x8"));
                    delayedLoad(instr.rt, value);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.LOAD_ADRESS_ERROR, instr.id);
                }

            } //else Console.WriteLine("Ignoring Load");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RFE() {
            //Console.ForegroundColor = ConsoleColor.Yellow;
            //Console.WriteLine("[RFE] PRE SR" + SR.ToString("x8"));
            uint mode = COP0_GPR[SR] & 0x3F;
            COP0_GPR[SR] &= ~(uint)0xF;
            COP0_GPR[SR] |= mode >> 2;
            //Console.WriteLine("[RFE] POST SR" + SR.ToString("x8"));
            //Console.ResetColor();
            //Console.ReadLine();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MTHI() {
            HI = GPR[instr.rs];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MTLO() {
            LO = GPR[instr.rs];
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SYSCALL() {
            EXCEPTION(EX.SYSCALL, instr.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SLT() {
            bool condition = (int)GPR[instr.rs] < (int)GPR[instr.rt];
            setGPR(instr.rd, condition ? 1u : 0u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MFHI() {
            setGPR(instr.rd, HI);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SLTIU() {
            bool condition = GPR[instr.rs] < instr.imm_s;
            setGPR(instr.rt, condition ? 1u : 0u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SRL() {
            setGPR(instr.rd, GPR[instr.rt] >> (int)instr.sa);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MFLO() {
            setGPR(instr.rd, LO);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SRA() {
            setGPR(instr.rd, (uint)((int)GPR[instr.rt] >> (int)instr.sa));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SUBU() {
            setGPR(instr.rd, GPR[instr.rs] - GPR[instr.rt]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SLTI() {
            bool condition = (int)GPR[instr.rs] < (int)instr.imm_s;
            setGPR(instr.rt, condition ? 1u : 0u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BRANCH() {
            opcodeTookBranch = true;
            PC_Predictor = PC + (instr.imm_s << 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void JALR() {
            setGPR(instr.rd, PC_Predictor);
            JR();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LBU() {
            if ((COP0_GPR[SR] & 0x10000) == 0) {
                uint value = (byte)bus.load32(GPR[instr.rs] + instr.imm_s);
                delayedLoad(instr.rt, value);
            } //else Console.WriteLine("Ignoring Load");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BLEZ() {
            opcodeIsBranch = true;
            if (((int)GPR[instr.rs]) <= 0) {
                BRANCH();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BGTZ() {
            opcodeIsBranch = true;
            if (((int)GPR[instr.rs]) > 0) {
                BRANCH();
            }
        }

        private void ADD() {
            int rs = (int)GPR[instr.rs];
            int rt = (int)GPR[instr.rt];
            try {
                uint add = (uint)checked(rs + rt);
                setGPR(instr.rd, add);
            } catch (OverflowException) {
                EXCEPTION(EX.OVERFLOW, instr.id);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AND() {
            setGPR(instr.rd, GPR[instr.rs] & GPR[instr.rt]);
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
        private void BEQ() {
            opcodeIsBranch = true;
            if (GPR[instr.rs] == GPR[instr.rt]) {
                BRANCH();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LB() { //todo redo this as it unnecesary load32
            if ((COP0_GPR[SR] & 0x10000) == 0) {
                uint value = (uint)(sbyte)bus.load32(GPR[instr.rs] + instr.imm_s);
                delayedLoad(instr.rt, value);
            } //else Console.WriteLine("Ignoring Write");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void JR() {
            opcodeIsBranch = true;
            opcodeTookBranch = true;
            PC_Predictor = GPR[instr.rs];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SB() {
            if ((COP0_GPR[SR] & 0x10000) == 0)
                bus.write8(GPR[instr.rs] + instr.imm_s, (byte)GPR[instr.rt]);
            //else Console.WriteLine("Ignoring Write");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ANDI() {
            setGPR(instr.rt, GPR[instr.rs] & instr.imm);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void JAL() {
            setGPR(31, PC_Predictor);
            J();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SH() {
            if ((COP0_GPR[SR] & 0x10000) == 0) {
                uint addr = GPR[instr.rs] + instr.imm_s;

                if ((addr & 0x1) == 0) {
                    bus.write16(addr, (ushort)GPR[instr.rt]);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.STORE_ADRESS_ERROR, instr.id);
                }
            }
            //else Console.WriteLine("Ignoring Write");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ADDU() {
            setGPR(instr.rd, GPR[instr.rs] + GPR[instr.rt]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SLTU() {
            bool condition = GPR[instr.rs] < GPR[instr.rt];
            setGPR(instr.rd, condition ? 1u : 0u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LW() {
            if ((COP0_GPR[SR] & 0x10000) == 0) {
                uint addr = GPR[instr.rs] + instr.imm_s;

                if ((addr & 0x3) == 0) {
                    uint value = bus.load32(addr);
                    delayedLoad(instr.rt, value);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.LOAD_ADRESS_ERROR, instr.id);
                }

            } //else Console.WriteLine("Ignoring Load");
        }

        private void ADDI() {
            int rs = (int)GPR[instr.rs];
            int imm_s = (int)instr.imm_s;
            try {
                uint addi = (uint)checked(rs + imm_s);
                setGPR(instr.rt, addi);
            } catch (OverflowException) {
                EXCEPTION(EX.OVERFLOW, instr.id);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BNE() {
            opcodeIsBranch = true;
            if (GPR[instr.rs] != GPR[instr.rt]) {
                BRANCH();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MTC0() {
            uint value = GPR[instr.rt];
            uint register = instr.rd;

            //MTC0 can trigger soft interrupts
            bool prevIEC = (COP0_GPR[SR] & 0x1) == 1;

            if (register == CAUSE) { //only bits 8 and 9 are writable
                COP0_GPR[CAUSE] &= ~(uint)0x300;
                COP0_GPR[CAUSE] |= value & 0x300;
            } else {
                COP0_GPR[register] = value; //There are some zeros on SR that shouldnt be writtable also todo: GPR > 16?
            }

            uint IM = (COP0_GPR[SR] >> 8) & 0x3;
            uint IP = (COP0_GPR[CAUSE] >> 8) & 0x3;

            if (!prevIEC && (COP0_GPR[SR] & 0x1) == 1 && (IM & IP) > 0) {
                PC = PC_Predictor;
                EXCEPTION(EX.INTERRUPT, instr.id);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OR() {
            setGPR(instr.rd, GPR[instr.rs] | GPR[instr.rt]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void J() {
            opcodeIsBranch = true;
            opcodeTookBranch = true;
            PC_Predictor = (PC_Predictor & 0xF000_0000) | (instr.addr << 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ADDIU() {
            setGPR(instr.rt, GPR[instr.rs] + instr.imm_s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SLL() {
            setGPR(instr.rd, GPR[instr.rt] << (int)instr.sa);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SW() {
            if ((COP0_GPR[SR] & 0x10000) == 0) {
                uint addr = GPR[instr.rs] + instr.imm_s;

                if ((addr & 0x3) == 0) {
                    bus.write32(addr, GPR[instr.rt]);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.STORE_ADRESS_ERROR, instr.id);
                }
            }
            //else Console.WriteLine("Ignoring Write");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LUI() {
            setGPR(instr.rt, instr.imm << 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ORI() {
            setGPR(instr.rt, GPR[instr.rs] | instr.imm);
        }

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
