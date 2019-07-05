using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace ProjectPSX {
    internal class CPU {  //MIPS R3000A-compatible 32-bit RISC CPU MIPS R3051 with 5 KB L1 cache, running at 33.8688 MHz // 33868800

        private BUS bus;
        //private static Action[] opTable;
        //private static Action[] specialTable;

        private uint PC_Now; // PC on current execution as PC and PC Predictor go ahead after fetch. This is handy on Branch Delay so it dosn't give erronious PC-4
        private uint PC = 0xbfc0_0000; // Bios Entry Point
        private uint PC_Predictor = 0xbfc0_0004; //next op for branch delay slot emulation

        private uint[] GPR = new uint[32];
        private uint HI;
        private uint LO;

        private bool opcodeIsBranch;
        private bool opcodeIsInDelaySlot;

        private bool branch;
        private bool tookBranch;

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

        //private bool interruptDelay;

        private struct MEM {
            public uint register;
            public uint value;
        }

        private struct Instr {
            public uint value;                     //debug
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

            public void Decode(uint instr) {
                value = instr;
            }
        }
        private Instr instr;

        private MEM writeBack;
        private MEM memoryLoad;
        private MEM delayedMemoryLoad;

        public bool debug = true;
        private bool isEX1 = true;
        private bool exe = true;

        public CPU(BUS bus) {
            //interruptDelay = true;
            this.bus = bus;
            bios = new BIOS_Disassembler(bus);
            COP0_GPR[15] = 0x2; //PRID Processor ID
            gte = new GTE(this); //debug
            //init();
        }

        // FUNCTION TABLE PROVED TO BE SLOWER THAN THE SWITCH BECAUSE C# DELEGATES ARE VERY SLOW STILL HERE FOR TESTS
        //private void init() {
        //    opTable = new Action[] {
        //        SPECIAL2, BCOND,    J,      JAL,    BEQ,    BNE,    BLEZ,   BGTZ,
        //        ADDI,     ADDIU,    SLTI,   SLTIU,  ANDI,   ORI,    XORI,   LUI,
        //        COP0,     NAE,     COP2,   NAE,   NA,     NA,     NA,     NA,
        //        NA,       NA,       NA,     NA,     NA,     NA,     NA,     NA,
        //        LB,       LH,       LWL,    LW,     LBU,    LHU,    LWR,    NA,
        //        SB,       SH,       SWL,    SW,     NA,     NA,     SWR,    NA,
        //        NAE, NAE, LWC2, NAE, NA, NA, NA, NA,
        //        NAE, NAE, SWC2, NAE, NA, NA, NA, NA
        //    };
        //
        //    specialTable = new Action[] {
        //        SLL,     NA,     SRL,    SRA,    SLLV,      NA,     SRLV,   SRAV,
        //        JR,      JALR,   NA,     NA,     SYSCALL,   BREAK,  NA,     NA,
        //        MFHI,    MTHI,   MFLO,   MTLO,   NA,        NA,     NA,     NA,
        //        MULT, MULTU, DIV, DIVU, NA, NA, NA, NA,
        //        ADD, ADDU, SUB, SUBU, AND, OR, XOR, NOR,
        //        NA, NA, SLT, SLTU, NA, NA, NA, NA,
        //        NA, NA, NA, NA, NA, NA, NA, NA,
        //        NA, NA, NA, NA, NA, NA, NA, NA,
        //    };
        //}
        //
        //private void SPECIAL2() {
        //    specialTable[instr.function]();
        //}
        //
        //private void NA() {
        //    EXCEPTION(EX.ILLEGAL_INSTR, instr.opcode & 0x3);
        //}
        //
        //private void NAE() { }

        //private void Execute2()
        //{
        //    opTable[instr.opcode]();
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Run() {
            fetchDecode();
            //if (handleInterrupts()) return;
            Execute();
            //Execute2(); //function table tests
            MemAccess();
            WriteBack();

            /*debug*/
           // TTY();
            //if (exe) forceTest(demo); //tcpu tcpx tgte tgpu demo <----------------------------------------------------------------------------------
            //if (isEX1) forceEX1();

            //if(cycle > 150000000) {
            //    debug = true;
            //}

            //if (debug) {
           //bios.verbose(PC_Now, GPR);
            //disassemble();
            //PrintRegs();
            //output();
            //}
        }

        int dev;
        StringBuilder str = new StringBuilder();
        public void output() {
            dev++;
            string debug = PC_Now.ToString("x8") + " " + instr.value.ToString("x8");
            string regs = "";
            for (int i = 0; i < 32; i++) {
                string padding = (i < 10) ? "0" : "";
                regs += "R" + padding + i + ":" + GPR[i].ToString("x8") + "  ";
                if ((i + 1) % 6 == 0) regs += "\n";
            };
            regs += " HI:" + HI.ToString("x8") + "  ";
            regs += " LO:" + LO.ToString("x8") + "  ";
            regs += " SR:" + COP0_GPR[SR].ToString("x8") + "  ";
            regs += "EPC:" + COP0_GPR[EPC].ToString("x8") + "\n";

            //str.Append(regs);

            if (dev == 1) {
                using (StreamWriter writer = new StreamWriter("log.txt", true)) {
                    writer.WriteLine(debug);
                    writer.WriteLine(regs);
                }
                str.Clear();
                dev = 0;
            }
        }

        string tcpu = "./psxtest_cpu.exe";
        string tcpx = "./psxtest_cpx.exe";
        string tgte = "./psxtest_gte.exe";
        string tgpu = "./psxtest_gpu.exe";
        string demo = "./padtest.exe";
        private void forceTest(string test) {
            if (PC == 0x8003_0000 && exe == true) {
                (uint _PC, uint R28, uint R29, uint R30) = bus.loadEXE(test);
                Console.WriteLine("SideLoading PSX EXE: PC {0} R28 {1} R29 {2} R30 {3}", PC.ToString("x8"), R28.ToString("x8"), R29.ToString("x8"), R30.ToString("x8"));
                GPR[29] = R29;
                GPR[28] = R28;
                GPR[30] = R30;
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
            //if (interruptDelay)
            //{
            //    interruptDelay = false;
            //    return false;
            //}
            uint I_STAT = bus.interruptController.loadISTAT();
            uint I_MASK = bus.interruptController.loadIMASK();

            if ((I_STAT & I_MASK) != 0) {
                //Console.WriteLine("I_STAT " + I_STAT.ToString("x8") + " I_MASK " + I_MASK.ToString("x8") + " CAUSE " + CAUSE.ToString("x8"));
                COP0_GPR[CAUSE] |= 0x400;
            } else {
                COP0_GPR[CAUSE] &= ~(uint)0x400;//(uint)(COP0_GPR[CAUSE] & ~0x400);
            }

            bool IEC = (COP0_GPR[SR] & 0x1) == 1;
            byte IM = (byte)((COP0_GPR[SR] >> 8) & 0xFF);
            byte IP = (byte)((COP0_GPR[CAUSE] >> 8) & 0xFF);
            bool Cop0Interrupt = (IM & IP) != 0;

            if (IEC && Cop0Interrupt) {
                //TODO Investigate why this is needed
                //if (((IP & IM)& 0x3) != 0)
                //{
                //Console.WriteLine("IM & IP:" + ((IP & IM)));
                fetchDecode();
                //}

                //instr.Decode(PC_Now);
                //disassemble();
                //PrintRegs();
                //Console.ForegroundColor = ConsoleColor.Red;
                //Console.Write("[EXCEPTION HANDLING] IEC " + IEC + " IM " +IM.ToString("x8") + " IP " +IP.ToString("x8") + " CAUSE " + CAUSE.ToString("x8"));
                //debug = true;
                EXCEPTION(EX.INTERRUPT);
                COP0_GPR[CAUSE] &= ~(uint)0x400;
                //fetchDecode(bus);
                //Console.WriteLine(" POST EX CAUSE " + CAUSE.ToString("x8"));
                //Console.ResetColor();
                //interruptDelay = true;
                //return true;
            }

            //return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void fetchDecode() {
            uint load = bus.load32(PC);
            PC_Now = PC;
            PC = PC_Predictor;
            PC_Predictor += 4;

            opcodeIsInDelaySlot = opcodeIsBranch;
            opcodeIsBranch = false;
            
            tookBranch = branch;
            branch = false;

            if ((PC_Now & 0x3) != 0) { // faster than PC_Now % 4 != 0
                COP0_GPR[BADA] = PC_Now; //TODO check this
                EXCEPTION(EX.LOAD_ADRESS_ERROR);
                return;
            }

            instr.Decode(load);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Execute() {
            switch (instr.opcode) {
                case 0b00_0000: SPECIAL(); break;//R-Type opcodes
                case 0b00_0001: BCOND(); break;
                case 0b00_0010: J(); break;
                case 0b00_0011: JAL(); break;
                case 0b00_0100: BEQ(); break;
                case 0b00_0101: BNE(); break;
                case 0b00_0110: BLEZ(); break;
                case 0b00_0111: BGTZ(); break;
                case 0b00_1000: ADDI(); break;
                case 0b00_1001: ADDIU(); break;
                case 0b00_1010: SLTI(); break;
                case 0b00_1011: SLTIU(); break;
                case 0b00_1100: ANDI(); break;
                case 0b00_1101: ORI(); break;
                case 0b00_1110: XORI(); break;
                case 0b00_1111: LUI(); break;
                case 0b01_0000: COP0(); break;
                case 0b01_0001: /*COP1()*/ break;
                case 0b01_0010: COP2(); break;
                case 0b01_0011: /*COP3()*/ break;
                case 0b10_0000: LB(); break;
                case 0b10_0001: LH(); break;
                case 0b10_0010: LWL(); break;
                case 0b10_0011: LW(); break;
                case 0b10_0100: LBU(); break;
                case 0b10_0101: LHU(); break;
                case 0b10_0110: LWR(); break;
                case 0b10_1000: SB(); break;
                case 0b10_1001: SH(); break;
                case 0b10_1010: SWL(); break;
                case 0b10_1011: SW(); break;
                case 0b10_1110: SWR(); break;
                case 0b11_0000: //LWC0
                case 0b11_0001: //LWC1
                case 0b11_0011: //LWC3
                case 0b11_1000: //SWC0
                case 0b11_1001: //SWC1
                case 0b11_1011: /*SWC3*/break; //All copro and lw sw instr dont trigger exception.
                case 0b11_0010: LWC2(); break;
                case 0b11_1010: SWC2(); break;
                default:
                    EXCEPTION(EX.ILLEGAL_INSTR, instr.opcode & 0x3);
                    break;
            }
        }

        private void SPECIAL() {
            switch (instr.function) {
                case 0b00_0000: SLL(); break;
                case 0b00_0010: SRL(); break;
                case 0b00_0011: SRA(); break;
                case 0b00_0100: SLLV(); break;
                case 0b00_0110: SRLV(); break;
                case 0b00_0111: SRAV(); break;
                case 0b00_1000: JR(); break;
                case 0b00_1001: JALR(); break;
                case 0b00_1100: SYSCALL(); break;
                case 0b00_1101: BREAK(); break;
                case 0b01_0000: MFHI(); break;
                case 0b01_0001: MTHI(); break;
                case 0b01_0010: MFLO(); break;
                case 0b01_0011: MTLO(); break;
                case 0b01_1000: MULT(); break;
                case 0b01_1001: MULTU(); break;
                case 0b01_1010: DIV(); break;
                case 0b01_1011: DIVU(); break;
                case 0b10_0000: ADD(); break;
                case 0b10_0001: ADDU(); break;
                case 0b10_0010: SUB(); break;
                case 0b10_0011: SUBU(); break;
                case 0b10_0100: AND(); break;
                case 0b10_0101: OR(); break;
                case 0b10_0110: XOR(); break;
                case 0b10_0111: NOR(); break;
                case 0b10_1010: SLT(); break;
                case 0b10_1011: SLTU(); break;
                default:
                    EXCEPTION(EX.ILLEGAL_INSTR);
                    break;
            }
        }

        private void COP2() {
            switch (instr.rs & 0x10) {
                case 0x0:
                    switch (instr.rs) {
                        case 0b0_0000: MFC2(); break;
                        case 0b0_0010: CFC2(); break;
                        case 0b0_0100: MTC2(); break;
                        case 0b0_0110: CTC2(); break;
                        default: EXCEPTION(EX.ILLEGAL_INSTR); break;
                    }
                    break;
                case 0x10:
                    gte.execute(instr.value);
                    break;
            }
        }

        private void COP0() {
            switch (instr.rs) {
                case 0b0_0000: MFC0(); break;
                case 0b0_0100: MTC0(); break;
                case 0b1_0000: RFE(); break;
                default: EXCEPTION(EX.ILLEGAL_INSTR); break;
            }
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
        private void CTC2() {
            gte.writeControl(instr.rd, GPR[instr.rt]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MTC2() {
            gte.writeData(instr.rd, GPR[instr.rt]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CFC2() {
            delayedLoad(instr.rt, gte.loadControl(instr.rd));
        }

        private void MFC2() {
            delayedLoad(instr.rt, gte.loadData(instr.rd));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SWC2() { //TODO WARNING THIS SHOULD HAVE DELAY?
            //Console.WriteLine("Store Data FROM GTE");
            uint addr = GPR[instr.rs] + instr.imm;

            if ((addr & 0x3) == 0) {
                bus.write32(addr, gte.loadData(instr.rt));
            } else {
                COP0_GPR[BADA] = addr;
                EXCEPTION(EX.LOAD_ADRESS_ERROR);
            }
            //Console.ReadLine();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LWC2() { //TODO WARNING THIS SHOULD HAVE DELAY?
            //Console.WriteLine("Load Data TO GTE");
            uint addr = GPR[instr.rs] + instr.imm;

            if ((addr & 0x3) == 0) {
                uint value = bus.load32(addr);
                gte.writeData(instr.rt, value);
            } else {
                COP0_GPR[BADA] = addr;
                EXCEPTION(EX.LOAD_ADRESS_ERROR);
            }
            //Console.ReadLine();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SWR() {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = (uint)(addr & ~0b11);

            uint aligned_load = bus.load32(aligned_addr & 0xFFFF_FFFC);

            uint value = 0;
            switch (addr & 0b11) {
                case 0: value = GPR[instr.rt]; break;
                case 1: value = (aligned_load & 0x0000_00FF) | (GPR[instr.rt] << 8); break;
                case 2: value = (aligned_load & 0x0000_FFFF) | (GPR[instr.rt] << 16); break;
                case 3: value = (aligned_load & 0x00FF_FFFF) | (GPR[instr.rt] << 24); break;
            }

            bus.write32(addr & 0xFFFF_FFFC, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SWL() {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = (uint)(addr & ~0b11);

            uint aligned_load = bus.load32(aligned_addr & 0xFFFF_FFFC);

            uint value = 0;
            switch (addr & 0b11) {
                case 0: value = (aligned_load & 0xFFFF_FF00) | (GPR[instr.rt] >> 24); break;
                case 1: value = (aligned_load & 0xFFFF_0000) | (GPR[instr.rt] >> 16); break;
                case 2: value = (aligned_load & 0xFF00_0000) | (GPR[instr.rt] >> 8); break;
                case 3: value = GPR[instr.rt]; break;
            }

            bus.write32(addr & 0xFFFF_FFFC, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LWR() {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = (uint)(addr & ~0b11);

            uint aligned_load = bus.load32(aligned_addr & 0xFFFF_FFFC);

            //Console.WriteLine("Addr {0}   Aligned Addr {1}", addr.ToString("x8"), aligned_addr.ToString("x8"));

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

            //Console.WriteLine("case " + (addr & 0b11) + " LWR Value " + value.ToString("x8"));
            delayedLoad(instr.rt, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LWL() {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = (uint)(addr & ~0b11);

            uint aligned_load = bus.load32(aligned_addr & 0xFFFF_FFFC);

            //Console.WriteLine("Addr {0}   Aligned Addr {1}", addr.ToString("x8"), aligned_addr.ToString("x8"));

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SUB() {
            int rs = (int)GPR[instr.rs];
            int rt = (int)GPR[instr.rt];
            try {
                uint sub = (uint)checked(rs - rt);
                setGPR(instr.rd, sub);
            } catch (OverflowException) {
                EXCEPTION(EX.OVERFLOW);
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
                    uint value = (uint)(short)bus.load16(addr);
                    delayedLoad(instr.rt, value);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.LOAD_ADRESS_ERROR);
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
                    uint value = bus.load16(addr);
                    //Console.WriteLine("LHU: " + addr.ToString("x8") + " value: " + value.ToString("x8"));
                    delayedLoad(instr.rt, value);
                } else {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.LOAD_ADRESS_ERROR);
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
            //Console.WriteLine(cause + " " + coprocessor);
            //uint ExAdress;
            //if ((COP0_GPR[SR] & (1 << 22)) == 0) {
            //    ExAdress = 0x8000_0080;
            //} else {
            //    ExAdress = 0xBFC0_0180;
            //}
            //uint ExAdress = COP0_GPR[SR] & 0x400000 >> 22;

            //Console.ForegroundColor = ConsoleColor.Yellow;
            //Console.WriteLine("[EXCEPTION F] PRE SR" + SR.ToString("x8"));

            uint mode = COP0_GPR[SR] & 0x3F;
            COP0_GPR[SR] &= ~(uint)0x3F;
            COP0_GPR[SR] |= (mode << 2) & 0x3F;

            //Console.WriteLine("[EXCEPTION F] POST SR" + SR.ToString("x8"));

            uint OldCause = COP0_GPR[CAUSE] & 0xff00;

            COP0_GPR[CAUSE] = (uint)cause << 2;
            COP0_GPR[CAUSE] |= OldCause;

            COP0_GPR[CAUSE] |= coprocessor << 28;

            //Console.WriteLine("[EXCEPTION F] PRE EPC " + EPC.ToString("x8"));
            //Console.WriteLine(((COP0[CAUSE] >> 8) & 0x3));


            COP0_GPR[EPC] = PC_Now;
            //if (((COP0[CAUSE] >> 8) & 0x3) != 0) {
            //    COP0[EPC] = PC;
            //}

            if (opcodeIsInDelaySlot) {
                //Console.WriteLine("isDelaySlot");
                COP0_GPR[EPC] -= 4;
                COP0_GPR[CAUSE] |= (uint)1 << 31;
                COP0_GPR[JUMPDEST] = PC;

                if (tookBranch) {
                    //Console.WriteLine("tookBranch");
                    COP0_GPR[CAUSE] |= (1 << 30);
                }
            }

            //Console.WriteLine("[EXCEPTION F] POST EPC " + EPC.ToString("x8"));
            //disassemble();
            //PrintRegs();
            //Console.ResetColor();
            //Console.ReadLine();

            PC = ExceptionAdress[COP0_GPR[SR] & 0x400000 >> 22];
            PC_Predictor = PC + 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SYSCALL() {
            EXCEPTION(EX.SYSCALL);
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
            branch = true;
            PC_Predictor -= 4;
            PC_Predictor += instr.imm_s << 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void JALR() {
            setGPR(instr.rd, PC_Predictor);
            JR();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LBU() {
            if ((COP0_GPR[SR] & 0x10000) == 0) {
                uint value = bus.load8(GPR[instr.rs] + instr.imm_s);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ADD() {
            int rs = (int)GPR[instr.rs];
            int rt = (int)GPR[instr.rt];
            try {
                uint add = (uint)checked(rs + rt);
                setGPR(instr.rd, add);
            } catch (OverflowException) {
                EXCEPTION(EX.OVERFLOW);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AND() {
            setGPR(instr.rd, GPR[instr.rs] & GPR[instr.rt]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MFC0() {
            uint mfc = instr.rd & 0xF;
            if(mfc != 0 || mfc != 1 || mfc != 2 || mfc != 4 || mfc != 10)
            {
                delayedLoad(instr.rt, COP0_GPR[mfc]);
            } else
            {
                EXCEPTION(EX.ILLEGAL_INSTR);
            }
            //switch (instr.rd) {
            //    case 3:
            //    case 5:
            //    case 6:
            //    case 7:
            //    case 8:
            //    case 9:
            //    case 11:
            //    case 12:
            //    case 13:
            //    case 14:
            //    case 15:
            //        delayedLoad(instr.rt, COP0_GPR[instr.rd]);
            //        break;
            //    default:
            //        EXCEPTION(EX.ILLEGAL_INSTR);
            //        break;
            //}

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
                uint value = (uint)(sbyte)bus.load8(GPR[instr.rs] + instr.imm_s);
                delayedLoad(instr.rt, value);
            } //else Console.WriteLine("Ignoring Write");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void JR() {
            opcodeIsBranch = true;
            branch = true;
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

                if ((addr & 0x1) != 0) {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.STORE_ADRESS_ERROR);
                } else {
                    bus.write16(addr, (ushort)GPR[instr.rt]);
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
                    EXCEPTION(EX.LOAD_ADRESS_ERROR, 3);
                }

            } //else Console.WriteLine("Ignoring Load");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ADDI() {
            int rs = (int)GPR[instr.rs];
            int imm_s = (int)instr.imm_s;
            try {
                uint addi = (uint)checked(rs + imm_s);
                setGPR(instr.rt, addi);
            } catch (OverflowException) {
                EXCEPTION(EX.OVERFLOW);
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

            if (register == 13) {
                //Console.WriteLine("[WARNING MTC0 CAUSE] " + GPR[instr.ft].ToString("x8"));
                //Console.WriteLine(COP0[13].ToString("x8"));
                //disassemble();
                //PrintRegs();
                value &= 0x300; //only bits 8 and 8 are writable
            }
            COP0_GPR[instr.rd] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OR() {
            setGPR(instr.rd, GPR[instr.rs] | GPR[instr.rt]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void J() {
            opcodeIsBranch = true;
            branch = true;
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

                if ((addr & 0x3) != 0) {
                    COP0_GPR[BADA] = addr;
                    EXCEPTION(EX.STORE_ADRESS_ERROR);
                } else {
                    bus.write32(addr, GPR[instr.rt]);
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

        public void disassemble() {
            string pc = PC_Now.ToString("x8");
            string load = instr.value.ToString("x8");
            string output = "";
            string values = "";

            switch (instr.opcode) {
                case 0b00_0000: //R-Type opcodes
                    switch (instr.function) {
                        case 0b00_0000: //SLL(); break;
                            if (instr.value == 0) output = "NOP";
                            else output = "SLL " + instr.rd;
                            break;
                        case 0b00_0010: output = "SRL"; break;
                        case 0b00_0011: output = "SRA"; break;
                        case 0b00_0100: output = "SLLV"; break;
                        case 0b00_0110: output = "SRLV"; break;
                        case 0b00_0111: output = "SRAV"; break;
                        case 0b00_1000: output = "JR R" + instr.rs + " " + GPR[instr.rs].ToString("x8"); break;
                        case 0b00_1001: output = "JALR"; break;
                        case 0b00_1100: output = "SYSCALL"; break;
                        case 0b00_1101: output = "BREAK"; break;
                        case 0b01_0000: output = "MFHI"; break;
                        case 0b01_0010: output = "MFLO"; break;
                        case 0b01_0011: output = "MTLO"; break;
                        case 0b01_1000: output = "MULT"; break;
                        case 0b01_1001: output = "MULTU"; break;
                        case 0b01_1010: output = "DIV"; break;
                        case 0b01_1011: output = "DIVU"; break;
                        case 0b10_0000: output = "ADD"; break;
                        case 0b10_0001: output = "ADDU"; break;
                        case 0b10_0010: output = "SUB"; break;
                        case 0b10_0011: output = "SUBU"; break;
                        case 0b10_0100: output = "AND"; break;
                        case 0b10_0101: output = "OR"; values = "R" + instr.rd + "," + (GPR[instr.rs] | GPR[instr.rt]).ToString("x8"); break;
                        case 0b10_0110: output = "XOR"; break;
                        case 0b10_0111: output = "NOR"; break;
                        case 0b10_1010: output = "SLT"; break;
                        case 0b10_1011: output = "SLTU"; break;
                        default: /*unimplementedWarning();*/ break;
                    }
                    break;
                case 0b00_0001:
                    switch (instr.rt) {
                        case 0b00_0000: output = "BLTZ"; break;
                        case 0b00_0001: output = "BGEZ"; break;
                        default: /*unimplementedWarning();*/ break;
                    }
                    break;

                case 0b00_0010: //J();
                    output = "J";
                    values = ((PC_Predictor & 0xF000_0000) | (instr.addr << 2)).ToString("x8");
                    break;
                case 0b00_0011: //JAL();
                    output = "JAL";
                    break;
                case 0b00_0100: //BEQ();
                    output = "BEQ";
                    values = "R" + instr.rs + ": " + GPR[instr.rs] + " R" + instr.rt + ": " + GPR[instr.rt] + " " + (GPR[instr.rs] == GPR[instr.rt]);
                    break;
                case 0b00_0101: //BNE();
                    output = "BNE";
                    values = "R" + instr.rs + "[" + GPR[instr.rs].ToString("x8") + "]" + "," + "R" + instr.rt + "[" + GPR[instr.rt].ToString("x8") + "], (" + ((PC_Now) + (instr.imm_s << 2)).ToString("x8") + ")";
                    break;
                case 0b00_0110: //BLEZ();
                    output = "BLEZ";
                    break;
                case 0b00_0111: //BGTZ();
                    output = "BGTZ";
                    break;
                case 0b00_1000: //ADDI();
                    output = "ADDI";
                    int rs = (int)GPR[instr.rs];
                    int imm_s = (int)instr.imm_s;
                    try {
                        uint addi = (uint)checked(rs + imm_s);
                        values = "R" + instr.rs + "," + (addi).ToString("x8") + " R" + instr.rs + "=" + GPR[instr.rs].ToString("x8"); ;
                        //Console.WriteLine("ADDI!");
                    } catch (OverflowException) {
                        values = "R" + instr.rt + "," + GPR[instr.rs].ToString("x8") + " + " + instr.imm_s.ToString("x8") + " UNHANDLED OVERFLOW";
                    }
                    break;
                case 0b00_1001: //ADDIU();
                                // setGPR(instr.rt, REG[instr.rs] + instr.imm_s);
                    output = "ADDIU";
                    values = "R" + instr.rt + "," + (GPR[instr.rs] + instr.imm_s).ToString("x8");
                    break;
                case 0b00_1010: //SLTI();
                    output = "SLTI";
                    break;
                case 0b00_1011: //SLTIU();
                    output = "SLTIU";
                    break;

                case 0b00_1100: //ANDI();
                    output = "ANDI";
                    values = "R" + instr.rt + ", " + (GPR[instr.rs] & instr.imm).ToString("x8");
                    break;
                case 0b00_1101: //ORI();
                                //setGPR(instr.rt, REG[instr.rs] | instr.imm);
                    output = "ORI";
                    values = "R" + instr.rt + "," + (GPR[instr.rs] | instr.imm).ToString("x8");
                    break;
                case 0b00_1111: //LUI();
                                //setGPR(instr.rt, instr.imm << 16);
                    output = "LUI";
                    values = "R" + instr.rt + "," + (instr.imm << 16).ToString("x8");
                    break;


                case 0b01_0000: //CoProcessor opcodes
                    switch (instr.rs) {
                        case 0b0_0000://MFC0();
                            output = "MFC0";
                            break;
                        case 0b0_0100://MTC0();
                                      // COP0[instr.fs] = REG[instr.ft];
                            output = "MTC0";
                            values = "R" + instr.rd + "," + "R" + instr.rt + "[" + GPR[instr.rt].ToString("x8") + "]";
                            break;
                        case 0b1_0000: //RFE(); break;
                            output = "RFE";
                            break;
                        default: /*unimplementedWarning();*/ break;
                    }
                    break;

                case 0b10_0000:// LB(bus);
                    output = "LB";
                    break;
                case 0b10_0001: //LH(bus); break;
                    output = "LH";
                    break;
                case 0b10_0010: output = "LWL"; break;
                case 0b10_0100:// LBU(bus);
                    output = "LBU";
                    break;
                case 0b10_0101: //LHU(bus); break;
                    output = "LHU";
                    values = "cached " + ((COP0_GPR[SR] & 0x10000) == 0) + "addr:" + (GPR[instr.rs] + instr.imm_s).ToString("x8") + "on R" + instr.rt;
                    break;
                case 0b10_0011:// LW(bus);
                    if ((COP0_GPR[SR] & 0x10000) == 0)
                        values = "R" + instr.rt + "[" + GPR[instr.rt].ToString("x8") + "], " + instr.imm_s.ToString("x8") + "(" + GPR[instr.rs].ToString("x8") + ")" + "[" + (instr.imm_s + GPR[instr.rs]).ToString("x8") + "]";
                    else values = "R" + instr.rt + "[" + GPR[instr.rt].ToString("x8") + "], " + instr.imm_s.ToString("x8") + "(" + GPR[instr.rs].ToString("x8") + ")" + "[" + (instr.imm_s + GPR[instr.rs]).ToString("x8") + "]" + " WARNING IGNORED LOAD";
                    output = "LW";
                    break;
                case 0b10_1001:// SH(bus);
                    output = "SH";
                    break;
                case 0b10_1010: output = "SWL"; break;
                case 0b10_0110: output = "LWR"; break;
                case 0b10_1000:// SB(bus);
                    output = "SB";
                    break;
                case 0b10_1011:// SW(bus);
                               //if ((SR & 0x10000) == 0)
                               //    bus.write32(REG[instr.rs] + instr.imm_s, REG[instr.rt]);
                    output = "SW";
                    if ((COP0_GPR[SR] & 0x10000) == 0)
                        values = "R" + instr.rt + "[" + GPR[instr.rt].ToString("x8") + "], " + instr.imm_s.ToString("x8") + "(" + GPR[instr.rs].ToString("x8") + ")" + "[" + (instr.imm_s + GPR[instr.rs]).ToString("x8") + "]";
                    else values = "R" + instr.rt + "[" + GPR[instr.rt].ToString("x8") + "], " + instr.imm_s.ToString("x8") + "(" + GPR[instr.rs].ToString("x8") + ")" + "[" + (instr.imm_s + GPR[instr.rs]).ToString("x8") + "]" + " WARNING IGNORED WRITE";
                    break;
                case 0b10_1110: output = "SWR"; break;
                default:
                    break;
            }
            Console.WriteLine("{0,-8} {1,-8} {2,-8} {3,-8} {4,-20}", cycle, pc, load, output, values);
        }

        public void PrintRegs() {
            string regs = "";
            for (int i = 0; i < 32; i++) {
                string padding = (i < 10) ? "0" : "";
                regs += "R" + padding + i + ":" + GPR[i].ToString("x8") + "  ";
                if ((i + 1) % 6 == 0) regs += "\n";
            }
            Console.Write(regs);
            Console.Write(" HI:" + HI.ToString("x8") + "  ");
            Console.Write(" LO:" + LO.ToString("x8") + "  ");
            Console.Write(" SR:" + COP0_GPR[SR].ToString("x8") + "  ");
            Console.Write("EPC:" + COP0_GPR[EPC].ToString("x8") + "\n");
            //bool IEC = (SR & 0x1) == 1;
            //byte IM = (byte)((SR >> 8) & 0xFF);
            //byte IP = (byte)((CAUSE >> 8) & 0xFF);
            //Console.WriteLine("[EXCEPTION INFO] IEC " + IEC + " IM " + IM.ToString("x8") + " IP " + IP.ToString("x8") + " CAUSE " + CAUSE.ToString("x8"));

            //

            //bool IEC = (SR & 0x1) == 1;
            //byte IM = (byte)((SR >> 8) & 0xFF);
            //byte IP = (byte)((CAUSE >> 8) & 0xFF);
            //bool Cop0Int = (IM & IP) != 0;
            //Console.WriteLine("IEC " + IEC + " IM " + IM + " IP " + IP + " Cop0Int" + Cop0Int);
            //if(REG[23] == 0xE1003000 || REG[23] == 0xE1001000) { Console.WriteLine("========== Warning!!!! ==== REF24 E100"); Console.ReadLine(); }
        }


    }
}