using System;
using System.IO;
using System.Text;

namespace ProjectPSX {
    internal class CPU {  //MIPS R3000A-compatible 32-bit RISC CPU MIPS R3051 with 5 KB L1 cache, running at 33.8688 MHz // 33868800

        private uint PC = 0xbfc0_0000; // Bios Entry Point
        private uint PC_Predictor = 0xbfc0_0004; //next op for branch delay slot emulation
        private uint PC_Now; // PC on current execution as PC and PC Predictor go ahead after fetch. This is handy on Branch Delay so it dosn't give erronious PC-4

        private uint[] GPR = new uint[32];
        private uint HI;
        private uint LO;

        private bool isBranch;
        private bool isDelaySlot;

        //CoPro Regs
        private uint[] Cop0Reg = new uint[16];
        private uint SR { get { return Cop0Reg[12]; } set { Cop0Reg[12] = value; } }
        private uint CAUSE { get { return Cop0Reg[13]; } set { Cop0Reg[13] = value; } }
        private uint EPC { get { return Cop0Reg[14]; } set { Cop0Reg[14] = value; } }

        //GTE
        private GTE gte;

        //Debug
        private long cycle; //current CPU cycle counter for debug
        private BIOS_Disassembler bios = new BIOS_Disassembler();

        private struct MEM {
            public uint register;
            public uint value;
            public uint delayedRegister;
            public uint delayedValue;
        }

        private struct WB {
            public uint register;
            public uint value;
        }

        private struct Instr {
            public uint opcode;         //Instr opcode
            public uint value;          //debug
            //I-Type
            public uint rs;             //Register Source
            public uint rt;             //Register Target
            public uint imm;            //Immediate value
            public uint imm_s;          //Immediate value sign extended
            //R-Type
            public uint rd;
            public uint sa;             //Shift Amount
            public uint function;       //Function
            //J-Type
            public uint addr;           //Target Address
            //CoProcessor
            public uint format;
            public uint ft;
            public uint fs;
            public uint fd;

            public void Decode(uint instr) {
                opcode = instr >> 26;
                value = instr;
                //I-Type
                rs = (instr >> 21) & 0x1F;
                rt = (instr >> 16) & 0x1F;
                imm = instr & 0xFFFF;
                imm_s = (uint)(short)imm;
                //R-Type
                rd = (instr >> 11) & 0x1F;
                sa = (instr >> 6) & 0x1F;
                function = instr & 0x3F;
                //J-Type
                addr = instr & 0x3FFFFFF;
                //CoProcessor
                format = rs;
                ft = rt;
                fs = rd;
                fd = sa;
            }
        }
        private Instr instr;

        private WB writeBack;
        private MEM memoryLoad;

        public bool debug = true;
        private bool isEX1 = true;
        private bool exe = true;

        public CPU() {
            Cop0Reg[15] = 0x2; //PRID Processor ID
            gte = new GTE(this); //debug
        }

        internal void Run(BUS bus) {
            fetchDecode(bus);
            Execute(bus);
            MemAccess();
            WriteBack();

            /*debug*/
            TTY();
            //forceTest(bus);
            if (isEX1) forceEX1(bus);

            //if(PC > 0x1F00_0000 && PC < 0x1F08_0000) { //EX1
            //    //Console.WriteLine("En Ex" + PC.ToString("x8") + ": " + bus.load(Width.WORD, PC).ToString("x8"));
            //    //Console.ReadLine();
            //}

            if (debug) {
                //bios.verbose(PC_Now, GPR);
                //disassemble();
                //PrintRegs();
                //output();
            }
            //if (debug == false && PC_Now == 0x800583B0) { //0x800583B0 CD COMMAND 0x19
            //    debug = true;
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
            regs += " SR:" + SR.ToString("x8") + "  ";
            regs += "EPC:" + EPC.ToString("x8") + "\n";

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
        private void forceTest(BUS bus) {
            if (PC == 0x8003_0000 && exe == true) {
                (uint PC, uint R28, uint R29, uint R30) = bus.loadEXE(tgte);
                Console.WriteLine("SideLoading PSX EXE: PC {0} R28 {1} R29 {2} R30 {3}", PC.ToString("x8"), R28.ToString("x8"), R29.ToString("x8"), R30.ToString("x8"));
                GPR[29] = R29;//0x801FFF00;//R29;
                GPR[28] = R28;
                GPR[30] = R30;//0x801FFF00;//R30;
                //GPR[31] = 0xBFC03D60;
                this.PC = PC;
                PC_Predictor = PC + 4;

                //debug = true;
                exe = false;
            }
        }

        private void forceEX1(BUS bus) {
            bus.loadEXP();
            bus.write(Width.WORD, 0x1F02_0018, 0x1);
            isEX1 = false;
        }

        public void handleInterrupts(BUS bus) {
            uint I_STAT = bus.load(Width.WORD, 0x1F801070);
            uint I_MASK = bus.load(Width.WORD, 0x1F801074);

            if ((I_STAT & I_MASK) != 0) {
                //Console.WriteLine("I_STAT " + I_STAT.ToString("x8") + " I_MASK " + I_MASK.ToString("x8") + " CAUSE " + CAUSE.ToString("x8"));
                CAUSE |= 0x400;
            } else {
                CAUSE = (uint)(CAUSE & ~0x400);
            }

            bool IEC = (SR & 0x1) == 1;
            byte IM = (byte)((SR >> 8) & 0xFF);
            byte IP = (byte)((CAUSE >> 8) & 0xFF);
            bool Cop0Interrupt = (IM & IP) != 0;

            if (IEC && Cop0Interrupt) {
                //TODO Investigate why this is needed
                fetchDecode(bus);
                //disassemble();
                //PrintRegs();
                //Console.ForegroundColor = ConsoleColor.Red;
                //Console.Write("[EXCEPTION HANDLING] IEC " + IEC + " IM " +IM.ToString("x8") + " IP " +IP.ToString("x8") + " CAUSE " + CAUSE.ToString("x8"));
                //debug = true;
                EXCEPTION(EX.INTERRUPT);
                CAUSE = (uint)(CAUSE & ~0x400);
                //Console.WriteLine(" POST EX CAUSE " + CAUSE.ToString("x8"));
                //Console.ResetColor();
                //if (I_STAT != 0)
                //  bus.write(Width.WORD, 0x1F801070, 0xffff_fffb); //test cd disable

            }
        }

        private void fetchDecode(BUS bus) {
            uint load = bus.load(Width.WORD, PC);
            PC_Now = PC;
            PC = PC_Predictor;
            PC_Predictor += 4;

            isDelaySlot = isBranch;
            isBranch = false;

            if ((PC_Now % 4) != 0) {
                EXCEPTION(EX.LOAD_ADRESS_ERROR);
                return;
            }

            instr.Decode(load);
            cycle++;
        }

        private void MemAccess() {
            if (memoryLoad.delayedRegister != memoryLoad.register) { //if loadDelay on same reg it is lost/overwritten (amidog tests)
                GPR[memoryLoad.register] = memoryLoad.value;
            }
            memoryLoad.register = memoryLoad.delayedRegister;
            memoryLoad.value = memoryLoad.delayedValue;
            memoryLoad.delayedRegister = 0;
            GPR[0] = 0;
        }

        private void WriteBack() {
            GPR[writeBack.register] = writeBack.value;
            writeBack.register = 0;
            GPR[0] = 0;
        }

        private void Execute(BUS bus) {
            switch (instr.opcode) {
                case 0b00_0000: //R-Type opcodes
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
                            //unimplementedWarning();
                            EXCEPTION(EX.ILLEGAL_INSTR);
                            break;
                    }
                    break;
                case 0b00_0001:
                    switch (instr.rt) {
                        case 0b01_0000: BLTZAL(); break;
                        case 0b01_0001: BGEZAL(); break;
                        default:
                            switch (instr.rt & 0x1) {
                                //somehow the psx mips accepts every possible combination of rt as BLTZ/BGEZ
                                //as long its not 0b1_0000 / 0b1_0001. Example: 0b010111 would be BGEZ
                                case 0b00_0000: BLTZ(); break;
                                case 0b00_0001: BGEZ(); break;
                            }
                            break;
                    }
                    break;

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

                case 0b01_0000: //CoProcessor 0 opcodes
                    switch (instr.format) {
                        case 0b0_0000: MFC0(); break;
                        case 0b0_0100: MTC0(); break;
                        case 0b1_0000: RFE(); break;
                        default: unimplementedWarning(); break;
                    }
                    break;

                case 0b01_0001: COP1(); break;

                case 0b01_0010: //CoProcessor 2 - GTE Opcodes
                    switch (instr.format & 0x10) {
                        case 0x0:
                            switch (instr.format) {
                                case 0b0_0000: MFC2(); break;
                                case 0b0_0010: CFC2(); break;
                                case 0b0_0100: MTC2(); break;
                                case 0b0_0110: CTC2(); break;
                                default: unimplementedWarning(); break;
                            }
                            break;
                        case 0x10:
                            gte.execute(instr.imm);
                            break;
                    }
                    break;

                case 0b01_0011: COP3(); break;

                case 0b10_0000: LB(bus); break;
                case 0b10_0001: LH(bus); break;
                case 0b10_0010: LWL(bus); break;
                case 0b10_0011: LW(bus); break;
                case 0b10_0100: LBU(bus); break;
                case 0b10_0101: LHU(bus); break;
                case 0b10_0110: LWR(bus); break;
                case 0b10_1000: SB(bus); break;
                case 0b10_1001: SH(bus); break;
                case 0b10_1010: SWL(bus); break;
                case 0b10_1011: SW(bus); break;
                case 0b10_1110: SWR(bus); break;
                case 0b11_0000: //LWC0
                case 0b11_0001: //LWC1
                case 0b11_0011: //LWC3
                case 0b11_1000: //SWC0
                case 0b11_1001: //SWC1
                case 0b11_1011: UNIMPL_LW_SW_COP0_1_3(); break;
                case 0b11_0010: LWC2(bus); break;
                case 0b11_1010: SWC2(bus); break;
                //pending lwc0-3 and swc0-3 and illegal opc
                default:
                    //unimplementedWarning();
                    EXCEPTION(EX.ILLEGAL_INSTR);
                    break;
            }
        }

        private void CTC2() {
            gte.writeControl(instr.fs, GPR[instr.ft]);
        }

        private void MTC2() {
            gte.writeData(instr.fs, GPR[instr.ft]);
        }

        private void CFC2() {
            delayedLoad(instr.ft, gte.loadControl(instr.fs));
        }

        private void MFC2() {
            delayedLoad(instr.ft, gte.loadData(instr.fs));
        }

        private void BGEZAL() {
            bool should_branch = ((int)GPR[instr.rs]) >= 0;
            GPR[31] = PC_Predictor;
            if (should_branch) {
                BRANCH();
            }
        }

        private void BLTZAL() {
            bool should_branch = ((int)GPR[instr.rs]) < 0;
            GPR[31] = PC_Predictor;
            if (should_branch) {
                BRANCH();
            }
        }

        private void SWC2(BUS bus) { //TODO WARNING THIS SHOULD HAVE DELAY?
            Console.WriteLine("Store Data FROM GTE");
            uint addr = GPR[instr.rs] + instr.imm;

            if ((addr % 4) != 0) {
                EXCEPTION(EX.LOAD_ADRESS_ERROR);
            } else {
                bus.write(Width.WORD, addr, gte.loadData(instr.rt));
            }
            //Console.ReadLine();
        }

        private void LWC2(BUS bus) { //TODO WARNING THIS SHOULD HAVE DELAY?
            Console.WriteLine("Load Data TO GTE");
            uint addr = GPR[instr.rs] + instr.imm;

            if ((addr % 4) != 0) {
                EXCEPTION(EX.LOAD_ADRESS_ERROR);
            } else {
                uint value = bus.load(Width.WORD, addr);
                gte.writeData(instr.rt, value);
            }
            //Console.ReadLine();
        }

        private void UNIMPL_LW_SW_COP0_1_3() { //PSX Unimplemented CoProcessor Ops
            EXCEPTION(EX.COPROCESSOR_ERROR);
        }

        private void SWR(BUS bus) {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = (uint)(addr & ~0b11);

            uint aligned_load = bus.load(Width.WORD, aligned_addr & 0xFFFF_FFFC);

            uint value = 0;
            switch (addr & 0b11) {
                case 0: value = GPR[instr.rt]; break;
                case 1: value = (aligned_load & 0x0000_00FF) | (GPR[instr.rt] << 8); break;
                case 2: value = (aligned_load & 0x0000_FFFF) | (GPR[instr.rt] << 16); break;
                case 3: value = (aligned_load & 0x00FF_FFFF) | (GPR[instr.rt] << 24); break;
            }

            bus.write(Width.WORD, addr & 0xFFFF_FFFC, value);
        }

        private void SWL(BUS bus) {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = (uint)(addr & ~0b11);

            uint aligned_load = bus.load(Width.WORD, aligned_addr & 0xFFFF_FFFC);

            uint value = 0;
            switch (addr & 0b11) {
                case 0: value = (aligned_load & 0xFFFF_FF00) | (GPR[instr.rt] >> 24); break;
                case 1: value = (aligned_load & 0xFFFF_0000) | (GPR[instr.rt] >> 16); break;
                case 2: value = (aligned_load & 0xFF00_0000) | (GPR[instr.rt] >> 8); break;
                case 3: value = GPR[instr.rt]; break;
            }

            bus.write(Width.WORD, addr & 0xFFFF_FFFC, value);
        }

        private void LWR(BUS bus) {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = (uint)(addr & ~0b11);

            uint aligned_load = bus.load(Width.WORD, aligned_addr & 0xFFFF_FFFC);

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

        private void LWL(BUS bus) {
            uint addr = GPR[instr.rs] + instr.imm_s;
            uint aligned_addr = (uint)(addr & ~0b11);

            uint aligned_load = bus.load(Width.WORD, aligned_addr & 0xFFFF_FFFC);

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

        private void COP3() {
            EXCEPTION(EX.COPROCESSOR_ERROR);
        }

        private void COP1() {
            EXCEPTION(EX.COPROCESSOR_ERROR);
        }

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
                EXCEPTION(EX.OVERFLOW);
            }
        }

        private void MULT() {
            long value = (long)(int)GPR[instr.rs] * (long)(int)GPR[instr.rt]; //sign extend to pass amidog cpu test

            HI = (uint)(value >> 32);
            LO = (uint)value;
        }

        private void BREAK() {
            EXCEPTION(EX.BREAK);
        }

        private void XOR() {
            setGPR(instr.rd, GPR[instr.rs] ^ GPR[instr.rt]);
        }

        private void MULTU() {
            ulong value = (ulong)GPR[instr.rs] * (ulong)GPR[instr.rt]; //sign extend to pass amidog cpu test

            HI = (uint)(value >> 32);
            LO = (uint)value;
        }

        private void SRLV() {
            setGPR(instr.rd, GPR[instr.rt] >> (int)(GPR[instr.rs] & 0x1F));
        }

        private void SRAV() {
            setGPR(instr.rd, (uint)((int)GPR[instr.rt] >> (int)(GPR[instr.rs] & 0x1F)));
        }

        private void NOR() {
            setGPR(instr.rd, ~(GPR[instr.rs] | GPR[instr.rt]));
        }

        private void LH(BUS bus) {
            if ((SR & 0x10000) == 0) {
                uint addr = GPR[instr.rs] + instr.imm_s;

                if ((addr % 2) != 0) {
                    EXCEPTION(EX.LOAD_ADRESS_ERROR);
                } else {
                    uint value = (uint)(short)bus.load(Width.HALF, addr);
                    delayedLoad(instr.rt, value);
                }

            } //else Console.WriteLine("Ignoring Load");
        }

        private void SLLV() {
            setGPR(instr.rd, GPR[instr.rt] << (int)(GPR[instr.rs] & 0x1F));
        }

        private void LHU(BUS bus) {
            if ((SR & 0x10000) == 0) {
                uint addr = GPR[instr.rs] + instr.imm_s;

                if ((addr % 2) != 0) {
                    EXCEPTION(EX.LOAD_ADRESS_ERROR);
                } else {
                    uint value = bus.load(Width.HALF, addr);
                    //Console.WriteLine("LHU: " + addr.ToString("x8") + " value: " + value.ToString("x8"));
                    delayedLoad(instr.rt, value);
                }

            } //else Console.WriteLine("Ignoring Load");
        }

        private void RFE() {
            //Console.ForegroundColor = ConsoleColor.Yellow;
            //Console.WriteLine("[RFE] PRE SR" + SR.ToString("x8"));
            uint mode = SR & 0x3F;
            SR = (uint)(SR & ~0xF);
            SR |= mode >> 2;
            //Console.WriteLine("[RFE] POST SR" + SR.ToString("x8"));
            //Console.ResetColor();
            //Console.ReadLine();
        }

        private void MTHI() {
            HI = GPR[instr.rs];
        }

        private void MTLO() {
            LO = GPR[instr.rs];
        }

        private void EXCEPTION(EX cause) {
            uint ExAdress;
            if ((SR & (1 << 22)) != 0) {
                ExAdress = 0xBFC0_0180;
            } else {
                ExAdress = 0x8000_0080;
            }

            //Console.ForegroundColor = ConsoleColor.Yellow;
            //Console.WriteLine("[EXCEPTION F] PRE SR" + SR.ToString("x8"));

            uint mode = SR & 0x3F;
            SR = (uint)(SR & ~0x3F);
            SR |= (mode << 2) & 0x3F;

            //Console.WriteLine("[EXCEPTION F] POST SR" + SR.ToString("x8"));

            uint OldCause = CAUSE & 0x3000ff00;

            CAUSE = (uint)cause << 2;
            CAUSE |= OldCause;

            //Console.WriteLine("[EXCEPTION F] PRE EPC " + EPC.ToString("x8"));

            EPC = PC_Now;

            if (isDelaySlot) {
                EPC = EPC - 4;
                CAUSE = (uint)(CAUSE | (1 << 31));
                Cop0Reg[6] = PC_Now; // WIP: JUMPDEST
            }

            //Console.WriteLine("[EXCEPTION F] POST EPC " + EPC.ToString("x8"));
            //disassemble();
            //PrintRegs();
            //Console.ResetColor();
            //Console.ReadLine();

            PC = ExAdress;
            PC_Predictor = PC + 4;
        }

        private void SYSCALL() {
            EXCEPTION(EX.SYSCALL);
        }

        private void SLT() {
            bool condition = (int)GPR[instr.rs] < (int)GPR[instr.rt];
            setGPR(instr.rd, condition ? 1u : 0u);
        }

        private void MFHI() {
            setGPR(instr.rd, HI);
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

        private void SLTIU() {
            bool condition = GPR[instr.rs] < instr.imm_s;
            setGPR(instr.rt, condition ? 1u : 0u);
        }

        private void SRL() {
            setGPR(instr.rd, GPR[instr.rt] >> (int)instr.sa);
        }

        private void MFLO() {
            setGPR(instr.rd, LO);
        }

        private void DIV() { //signed division
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

        private void SRA() { //TODO revisit this
            setGPR(instr.rd, (uint)((int)GPR[instr.rt] >> (int)instr.sa));
        }

        private void SUBU() {
            setGPR(instr.rd, GPR[instr.rs] - GPR[instr.rt]);
        }

        private void SLTI() {
            bool condition = (int)GPR[instr.rs] < (int)instr.imm_s;
            setGPR(instr.rt, condition ? 1u : 0u);
        }

        private void BRANCH() {
            isBranch = true;
            PC_Predictor -= 4;
            PC_Predictor += instr.imm_s << 2;
        }

        private void BGEZ() {
            if (((int)GPR[instr.rs]) >= 0) {
                BRANCH();
            }
        }

        private void BLTZ() {
            if (((int)GPR[instr.rs]) < 0) {
                BRANCH();
            }
        }

        private void JALR() {
            isBranch = true;
            setGPR(instr.rd, PC_Predictor);
            JR();
        }

        private void LBU(BUS bus) { //todo recheck this
            if ((SR & 0x10000) == 0) {
                uint value = bus.load(Width.BYTE, GPR[instr.rs] + instr.imm_s);
                delayedLoad(instr.rt, value);
            } //else Console.WriteLine("Ignoring Load");
        }

        private void BLEZ() {
            if (((int)GPR[instr.rs]) <= 0) {
                BRANCH();
            }
        }

        private void BGTZ() {
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
                //if(rs + rt != 2408)
                //Console.WriteLine("ADD NO OVERLOW -- " + rs.ToString("x8") + " + " + rt.ToString("x8") + " " + (rs + rt) );
            } catch (OverflowException) {
                //Console.WriteLine("ADD OVERLOW -- " + rs.ToString("x8") + " + " + rt.ToString("x8"));
                EXCEPTION(EX.OVERFLOW);
            }
        }

        private void AND() {
            setGPR(instr.rd, GPR[instr.rs] & GPR[instr.rt]);
        }

        private void MFC0() {
            delayedLoad(instr.ft, Cop0Reg[instr.fs]);
        }

        private void BEQ() {
            if (GPR[instr.rs] == GPR[instr.rt]) {
                BRANCH();
            }
        }

        private void LB(BUS bus) { //todo redo this as it unnecesary load32
            if ((SR & 0x10000) == 0) {
                uint value = (uint)(sbyte)bus.load(Width.BYTE, GPR[instr.rs] + instr.imm_s);
                delayedLoad(instr.rt, value);
            } //else Console.WriteLine("Ignoring Write");
        }

        private void JR() {
            isBranch = true;
            PC_Predictor = GPR[instr.rs];
        }

        private void SB(BUS bus) {
            if ((SR & 0x10000) == 0)
                bus.write(Width.BYTE, GPR[instr.rs] + instr.imm_s, (byte)GPR[instr.rt]);
            //else Console.WriteLine("Ignoring Write");
        }

        private void ANDI() {
            setGPR(instr.rt, GPR[instr.rs] & instr.imm);
        }

        private void JAL() {
            setGPR(31, PC_Predictor);
            J();
        }

        private void SH(BUS bus) {
            if ((SR & 0x10000) == 0) {
                uint addr = GPR[instr.rs] + instr.imm_s;

                if ((addr % 2) != 0) {
                    EXCEPTION(EX.STORE_ADRESS_ERROR);
                } else {
                    bus.write(Width.HALF, addr, (ushort)GPR[instr.rt]);
                }
            }
            //else Console.WriteLine("Ignoring Write");
        }

        private void ADDU() {
            setGPR(instr.rd, GPR[instr.rs] + GPR[instr.rt]);
        }

        private void SLTU() {
            bool condition = GPR[instr.rs] < GPR[instr.rt];
            setGPR(instr.rd, condition ? 1u : 0u);
        }

        private void LW(BUS bus) {
            if ((SR & 0x10000) == 0) {
                uint addr = GPR[instr.rs] + instr.imm_s;

                if ((addr % 4) != 0) {
                    EXCEPTION(EX.LOAD_ADRESS_ERROR);
                } else {
                    uint value = bus.load(Width.WORD, addr);
                    delayedLoad(instr.rt, value);
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
                EXCEPTION(EX.OVERFLOW);
            }
        }

        private void BNE() {
            if (GPR[instr.rs] != GPR[instr.rt]) {
                BRANCH();
            }
        }

        private void MTC0() {
            uint value = GPR[instr.ft];
            uint register = instr.fs;

            if (register == 12) {
                //Console.WriteLine("[WARNING MTC0 SR] " + GPR[instr.ft].ToString("x8"));
                //Console.ReadLine();
                //disassemble();
                //PrintRegs();
            } else if (register == 13) {
                //Console.WriteLine("[WARNING MTC0 CAUSE] " + GPR[instr.ft].ToString("x8"));
                //Console.WriteLine(Cop0Reg[13].ToString("x8"));
                //disassemble();
                //PrintRegs();
                value &= 0x300; //only bits 8 and 8 are writable
            }
            Cop0Reg[instr.fs] = value;
        }

        private void OR() {
            setGPR(instr.rd, GPR[instr.rs] | GPR[instr.rt]);
        }

        private void J() {
            isBranch = true;
            PC_Predictor = (PC_Predictor & 0xF000_0000) | (instr.addr << 2);
        }

        private void ADDIU() {
            setGPR(instr.rt, GPR[instr.rs] + instr.imm_s);
        }

        private void SLL() {
            setGPR(instr.rd, GPR[instr.rt] << (int)instr.sa);
        }

        private void SW(BUS bus) {
            if ((SR & 0x10000) == 0) {
                uint addr = GPR[instr.rs] + instr.imm_s;

                if ((addr % 4) != 0) {
                    EXCEPTION(EX.STORE_ADRESS_ERROR);
                } else {
                    bus.write(Width.WORD, addr, GPR[instr.rt]);
                }
            }
            //else Console.WriteLine("Ignoring Write");
        }

        private void LUI() {
            setGPR(instr.rt, instr.imm << 16);
        }

        private void ORI() {
            setGPR(instr.rt, GPR[instr.rs] | instr.imm);
        }

        private void setGPR(uint regN, uint value) {
            writeBack.register = regN;
            writeBack.value = value;
        }

        private void delayedLoad(uint regN, uint value) {
            memoryLoad.delayedRegister = regN;
            memoryLoad.delayedValue = value;
        }

        private void unimplementedWarning() {
            Console.WriteLine("[CPU] Unimplemented instruction: ");
            string funct_string = instr.opcode == 0 ? " Function: " + instr.function.ToString("x8") : "";
            string format_string = instr.opcode == 0b01_0000 ? " Function: " + instr.format.ToString("x8") : "";
            Console.WriteLine("Cycle: " + cycle + " PC: " + PC_Now.ToString("x8") + " Load32: " + instr.value.ToString("x8")
                + " Instr: " + instr.opcode.ToString("x8") + funct_string + format_string);
            disassemble();
            PrintRegs();
            throw new NotImplementedException();
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
                    switch (instr.format) {
                        case 0b0_0000://MFC0();
                            output = "MFC0";
                            break;
                        case 0b0_0100://MTC0();
                                      // Cop0Reg[instr.fs] = REG[instr.ft];
                            output = "MTC0";
                            values = "R" + instr.fs + "," + "R" + instr.ft + "[" + GPR[instr.ft].ToString("x8") + "]";
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
                    values = "cached " + ((SR & 0x10000) == 0) + "addr:" + (GPR[instr.rs] + instr.imm_s).ToString("x8") + "on R" + instr.rt;
                    break;
                case 0b10_0011:// LW(bus);
                    if ((SR & 0x10000) == 0)
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
                    if ((SR & 0x10000) == 0)
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
            Console.Write(" SR:" + SR.ToString("x8") + "  ");
            Console.Write("EPC:" + EPC.ToString("x8") + "\n");
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