using System;

namespace ProjectPSX {
    internal class CPU {

        private uint PC = 0xbfc0_0000; // Bios Entry Point
        private uint PC_Predictor = 0xbfc0_0004; //next op for branch delay slot emulation
        private uint PC_Now; // PC on current execution as PC and PC Predictor go ahead after fetch. This is handy on Branch Delay so it dosn't give erronious PC-4

        private uint[] REG = new uint[32];
        private uint HI;
        private uint LO;

        private bool isBranch;
        private bool isDelaySlot;

        private long cycle; //current CPU cycle counter for debug

        //CoPro Regs
        private uint[] Cop0Reg = new uint[16];
        private uint SR     { get { return Cop0Reg[12]; } set { Cop0Reg[12] = value; } }
        private uint CAUSE  { get { return Cop0Reg[13]; } set { Cop0Reg[13] = value; } }
        private uint EPC    { get { return Cop0Reg[14]; } set { Cop0Reg[14] = value; } }

        private struct MEM {
            public uint LoadRegNPostDelay;
            public uint LoadValuePostDelay;
            public uint LoadRegN;
            public uint LoadValue;
        }

        private struct WB {
            public uint WriteRegN;
            public uint WriteValue;
        }

        private struct Opcode {
            public uint instruction;    //Opcode Instruction
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

            public void Decode(uint opcode) {
                instruction = opcode >> 26;
                value = opcode;
                //I-Type
                rs = (opcode >> 21) & 0x1F;
                rt = (opcode >> 16) & 0x1F;
                imm = opcode & 0xFFFF;
                imm_s = (uint)(short)imm;
                //R-Type
                rd = (opcode >> 11) & 0x1F;
                sa = (opcode >> 6) & 0x1F;
                function = opcode & 0x3F;
                //J-Type
                addr = opcode & 0x3FFFFFF;
                //CoProcessor
                format = rs;
                ft = rt;
                fs = rd;
                fd = sa;
            }
        }
        private Opcode opcode;

        private WB wb;
        private MEM mem;

        internal void Run(MMU mmu) {
            fetchDecode(mmu);
            Execute(mmu);
            MemAccess();
            WriteBack();
            TTY();
            //debug
            //disassemble();
            //PrintRegs();
        }

        private void fetchDecode(MMU mmu) {
            uint load = mmu.read32(PC);
            PC_Now = PC;
            PC = PC_Predictor;
            PC_Predictor += 4;
            isDelaySlot = isBranch;
            isBranch = false;

            if((PC_Now % 4) != 0) {
                EXCEPTION(EX.LOAD_ADRESS_ERROR);
                return;
            }

            opcode.Decode(load);
            cycle++;
        }

        private void MemAccess() {
            REG[mem.LoadRegNPostDelay] = mem.LoadValuePostDelay;
            mem.LoadRegNPostDelay = mem.LoadRegN;
            mem.LoadValuePostDelay = mem.LoadValue;
            mem.LoadRegN = 0;
            REG[0] = 0;
        }

        private void WriteBack() {
            REG[wb.WriteRegN] = wb.WriteValue;
            wb.WriteRegN = 0;
            REG[0] = 0;
        }

        private void Execute(MMU mmu) {
            switch (opcode.instruction) {
                case 0b00_0000: //R-Type Instructions
                    switch (opcode.function) {
                        case 0b00_0000: SLL();            break;
                        case 0b00_0010: SRL();            break;
                        case 0b00_0011: SRA();            break;
                        case 0b00_0100: SLLV();           break;
                        case 0b00_0110: SRLV();           break;
                        case 0b00_0111: SRAV();           break;
                        case 0b00_1000: JR();             break;
                        case 0b00_1001: JALR();           break;
                        case 0b00_1100: SYSCALL();        break;
                        case 0b01_0000: MFHI();           break;
                        case 0b01_0001: MTHI();           break;
                        case 0b01_0010: MFLO();           break;
                        case 0b01_0011: MTLO();           break;
                        case 0b01_1001: MULTU();          break;
                        case 0b01_1010: DIV();            break;
                        case 0b01_1011: DIVU();           break;
                        case 0b10_0000: ADD();            break;
                        case 0b10_0001: ADDU();           break;
                        case 0b10_0011: SUBU();           break;
                        case 0b10_0100: AND();            break;
                        case 0b10_0111: NOR();            break;
                        case 0b10_1010: SLT();            break;
                        case 0b10_1011: SLTU();           break;
                        case 0b10_0101: OR();             break;
                        default: unimplementedWarning();  break;
                    }
                    break;
                case 0b00_0001:
                    switch (opcode.rt) {
                        case 0b00_0000: BLTZ();           break;
                        case 0b00_0001: BGEZ();           break;
                        default: unimplementedWarning();  break;
                    }
                    break;
                
                case 0b00_0010: J();                      break;
                case 0b00_0011: JAL();                    break;
                case 0b00_0100: BEQ();                    break;
                case 0b00_0101: BNE();                    break;
                case 0b00_0110: BLEZ();                   break;
                case 0b00_0111: BGTZ();                   break;
                case 0b00_1000: ADDI();                   break;
                case 0b00_1001: ADDIU();                  break;
                case 0b00_1010: SLTI();                   break;
                case 0b00_1011: SLTIU();                  break;
                case 0b00_1100: ANDI();                   break;
                case 0b00_1101: ORI();                    break;
                case 0b00_1111: LUI();                    break;

                case 0b01_0000: //CoProcessor Instructions
                    switch (opcode.format) {
                        case 0b0_0000: MFC0();            break;
                        case 0b0_0100: MTC0();            break;
                        case 0b1_0000: RFE();             break;
                        default: unimplementedWarning();  break;
                    }
                    break;
               
                case 0b10_0000: LB(mmu);                  break;
                case 0b10_0001: LH(mmu);                  break;
                case 0b10_0011: LW(mmu);                  break;
                case 0b10_0100: LBU(mmu);                 break;
                case 0b10_0101: LHU(mmu);                 break;
                case 0b10_1000: SB(mmu);                  break;
                case 0b10_1001: SH(mmu);                  break;
                case 0b10_1011: SW(mmu);                  break;
                default:
                    PC_Predictor -= 4;
                    unimplementedWarning();
                    break;
            }
        }

        private void MULTU() {
            ulong value = REG[opcode.rs] * REG[opcode.rt];

            HI = (uint)(value >> 32);
            LO = (uint)value;
        }

        private void SRLV() {
            setReg(opcode.rd, REG[opcode.rt] >> (int)(REG[opcode.rs] & 0x1F));
        }

        private void SRAV() {
            setReg(opcode.rd, (uint)((int)REG[opcode.rt] >> (int)(REG[opcode.rs] & 0x1F)));
        }

        private void NOR() {
            setReg(opcode.rd, ~(REG[opcode.rs] | REG[opcode.rt]));
        }

        private void LH(MMU mmu) {
            if ((SR & 0x10000) == 0) {
                uint addr = REG[opcode.rs] + opcode.imm_s;

                if ((addr % 2) != 0) {
                    EXCEPTION(EX.LOAD_ADRESS_ERROR);
                } else {
                    uint value = (uint)(short)mmu.read32(addr);
                    mem.LoadRegN = opcode.rt;
                    mem.LoadValue = value;
                }

            } //else Console.WriteLine("Ignoring Load");
        }

        private void SLLV() {
            setReg(opcode.rd, REG[opcode.rt] << (int)(REG[opcode.rs] & 0x1F));
        }

        private void LHU(MMU mmu) {
            if ((SR & 0x10000) == 0) {
                uint addr = REG[opcode.rs] + opcode.imm_s;

                if ((addr % 2) != 0) {
                    EXCEPTION(EX.LOAD_ADRESS_ERROR);
                } else {
                    uint value = (ushort)mmu.read32(addr);
                    mem.LoadRegN = opcode.rt;
                    mem.LoadValue = value;
                }

            } //else Console.WriteLine("Ignoring Load");
        }

        private void RFE() {
            uint mode = SR & 0x3F;
            SR = (uint)(SR & ~0x3F);
            SR |= mode >> 2;
        }

        private void MTHI() {
            HI = REG[opcode.rs];
        }

        private void MTLO() {
            LO = REG[opcode.rs];
        }

        private void EXCEPTION(uint cause) {
            uint ExAdress;
            if((SR & (1 << 22)) != 0) {
                ExAdress = 0xBFC0_0180;
            } else {
                ExAdress = 0x8000_0080;
            }

            uint mode = SR & 0x3F;
            SR = (uint)(SR & ~0x3F);
            SR |= (mode << 2) & 0x3F;

            CAUSE = cause << 2;
            EPC = PC_Now;

            if (isDelaySlot) {
                EPC = EPC - 4;
                CAUSE = (uint)(CAUSE |(1 << 31));
            }

            PC = ExAdress;
            PC_Predictor = PC + 4;
        }

        private void SYSCALL() {
            EXCEPTION(EX.SYSCALL);
        }

        private void SLT() {
            bool condition = (int)REG[opcode.rs] < (int)REG[opcode.rt];
            setReg(opcode.rd, condition ? 1u : 0u);
        }

        private void MFHI() {
            setReg(opcode.rd, HI);
        }

        private void DIVU() {
            uint n = REG[opcode.rs];
            uint d = REG[opcode.rt];

            if(d == 0) {
                HI = n;
                LO = 0xFFFF_FFFF;
            } else {
                HI = n % d;
                LO = n / d;
            }
        }

        private void SLTIU() {
            bool condition = REG[opcode.rs] < opcode.imm_s;
            setReg(opcode.rt, condition ? 1u : 0u);
        }

        private void SRL() {
            setReg(opcode.rd, REG[opcode.rt] >> (int)opcode.sa);
        }

        private void MFLO() {
            setReg(opcode.rd, LO);
        }

        private void DIV() { //signed division
            int n = (int)REG[opcode.rs];
            int d = (int)REG[opcode.rt];

            if(d == 0) {
                HI = (uint) n;
                if(n >= 0) {
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
            setReg(opcode.rd, (uint)((int)REG[opcode.rt] >> (int)opcode.sa));
        }

        private void SUBU() {
            setReg(opcode.rd, REG[opcode.rs] - REG[opcode.rt]);
        }

        private void SLTI() {
            bool condition = (int)REG[opcode.rs] < (int)opcode.imm_s;
            setReg(opcode.rt, condition ? 1u : 0u);
        }

        private void BRANCH() {
            isBranch = true;
            PC_Predictor -= 4;
            PC_Predictor += opcode.imm_s << 2;
        }

        private void BGEZ() {
            if (((int)REG[opcode.rs]) >= 0) {
                BRANCH();
            }
        }

        private void BLTZ() {
            if (((int)REG[opcode.rs]) < 0) {
                BRANCH();
            }
        }

        private void JALR() {
            isBranch = true;
            setReg(opcode.rd, PC_Predictor);
            JR();
        }

        private void LBU(MMU mmu) { //todo recheck this
            if ((SR & 0x10000) == 0) {
                uint value = (byte)mmu.read32(REG[opcode.rs] + opcode.imm_s);
                mem.LoadRegN = opcode.rt;
                mem.LoadValue = value;
            } //else Console.WriteLine("Ignoring Load");
        }

        private void BLEZ() {
            if (((int)REG[opcode.rs]) <= 0) {
                BRANCH();
            }
        }

        private void BGTZ() {
            if (((int)REG[opcode.rs]) > 0) {
                BRANCH();
            }
        }

        private void ADD() {
            int rs = (int)REG[opcode.rs];
            int rt = (int)REG[opcode.rt];
            try {
                uint add = (uint)checked(rs + rt);
                setReg(opcode.rd, add);
            } catch (OverflowException) {
                EXCEPTION(EX.OVERFLOW);
            }
        }

        private void AND() {
            setReg(opcode.rd, REG[opcode.rs] & REG[opcode.rt]);
        }

        private void MFC0() {
            mem.LoadRegN = opcode.ft;
            mem.LoadValue = Cop0Reg[opcode.fs];
        }

        private void BEQ() {
            if (REG[opcode.rs] == REG[opcode.rt]) {
                BRANCH();
            }
        }

        private void LB(MMU mmu) { //todo redo this as it unnecesary read32
            if ((SR & 0x10000) == 0) {
                uint lb = (uint)((sbyte)(mmu.read32(REG[opcode.rs] + opcode.imm_s)));
                mem.LoadRegN = opcode.rt;
                mem.LoadValue = lb;
            } //else Console.WriteLine("Ignoring Write");
        }

        private void JR() {
            isBranch = true;
            PC_Predictor = REG[opcode.rs];
        }

        private void SB(MMU mmu) {
            if ((SR & 0x10000) == 0)
                mmu.write8(REG[opcode.rs] + opcode.imm_s, (byte)REG[opcode.rt]);
            //else Console.WriteLine("Ignoring Write");
        }

        private void ANDI() {
            setReg(opcode.rt, REG[opcode.rs] & opcode.imm);
        }

        private void JAL() {
            setReg(31, PC_Predictor);
            J();
        }

        private void SH(MMU mmu) {
            if ((SR & 0x10000) == 0) {
                uint addr = REG[opcode.rs] + opcode.imm_s;

                if ((addr % 2) != 0) {
                    EXCEPTION(EX.STORE_ADRESS_ERROR);
                } else {
                    mmu.write16(addr, (ushort)REG[opcode.rt]);
                }
            }
            //else Console.WriteLine("Ignoring Write");
        }

        private void ADDU() {
            setReg(opcode.rd, REG[opcode.rs] + REG[opcode.rt]);
        }

        private void SLTU() {
            bool condition = REG[opcode.rs] < REG[opcode.rt];
            setReg(opcode.rd, condition ? 1u : 0u);
        }

        private void LW(MMU mmu) {
            if ((SR & 0x10000) == 0) {
                uint addr = REG[opcode.rs] + opcode.imm_s;

                if ((addr % 4) != 0) {
                    EXCEPTION(EX.LOAD_ADRESS_ERROR);
                } else {
                    uint value = mmu.read32(addr);
                    mem.LoadRegN = opcode.rt;
                    mem.LoadValue = value;
                }

            } //else Console.WriteLine("Ignoring Load");
        }

        private void ADDI() {
            int rs = (int)REG[opcode.rs];
            int imm_s = (int)opcode.imm_s;
            try {
                uint addi = (uint) checked(rs + imm_s);
                setReg(opcode.rt, addi);
            } catch (OverflowException) {
                EXCEPTION(EX.OVERFLOW);
            }
        }

        private void BNE() {
            if (REG[opcode.rs] != REG[opcode.rt]) {
                BRANCH();
            }
        }

        private void MTC0() {
            Cop0Reg[opcode.fs] = REG[opcode.ft];
        }

        private void OR() {
            setReg(opcode.rd, REG[opcode.rs] | REG[opcode.rt]);
        }

        private void J() {
            isBranch = true;
            PC_Predictor = (PC_Predictor & 0xF000_0000) | (opcode.addr << 2);
        }

        private void ADDIU() {
            setReg(opcode.rt, REG[opcode.rs] + opcode.imm_s);
        }

        private void SLL() {
            setReg(opcode.rd, REG[opcode.rt] << (int)opcode.sa);
        }

        private void SW(MMU mmu) {
            if ((SR & 0x10000) == 0) {
                uint addr = REG[opcode.rs] + opcode.imm_s;

                if ((addr % 4) != 0) {
                    EXCEPTION(EX.STORE_ADRESS_ERROR);
                } else {
                    mmu.write32(addr, REG[opcode.rt]);
                }
            }
            //else Console.WriteLine("Ignoring Write");
        }

            private void LUI() {
            setReg(opcode.rt, opcode.imm << 16);
        }

        private void ORI() {
            setReg(opcode.rt, REG[opcode.rs] | opcode.imm);
        }

        private void setReg(uint regN, uint value) {
            wb.WriteRegN = regN;
            wb.WriteValue = value;
        }

        private void unimplementedWarning() {
            Console.WriteLine("Unimplemented OPCODE");
            string funct_string = opcode.instruction == 0 ? " Function: " + opcode.function.ToString("x8") : "";
            string format_string = opcode.instruction == 0b01_0000 ? " Function: " + opcode.format.ToString("x8") : "";
            Console.WriteLine("Cycle: " + cycle + " PC: " + PC_Now.ToString("x8") + " Load32: " + opcode.value.ToString("x8")
                + " Instr: " + opcode.instruction.ToString("x8") + funct_string + format_string);
            disassemble();
            PrintRegs();
            throw new NotImplementedException();
        }

        private void TTY() {
            if (PC == 0x000000B4 && REG[9] == 0x3D) {
                Console.Write((char)REG[4]);
            }
        }

        private void disassemble() {
            string pc = PC_Now.ToString("x8");
            string load = opcode.value.ToString("x8");
            string output = "";
            string values = "";

            switch (opcode.instruction) {
                case 0b00_0000: //R-Type Instructions
                    switch (opcode.function) {
                        case 0b00_0000: //SLL(); break;
                            if(opcode.value == 0) output = "NOP";
                            else output = "SLL" + opcode.rd;
                            break;
                        case 0b00_0010: output = "SRL";     break;
                        case 0b00_0011: output = "SRA";     break;
                        case 0b00_0100: output = "SLLV";    break;
                        case 0b00_0110: output = "SRLV";    break;
                        case 0b00_0111: output = "SRAV";    break;
                        case 0b00_1000: output = "JR";      break;
                        case 0b00_1001: output = "JALR";    break;
                        case 0b00_1100: output = "SYSCALL"; break;
                        case 0b01_0000: output = "MFHI";    break;
                        case 0b01_0010: output = "MFLO";    break;
                        case 0b01_0011: output = "MTLO";    break;
                        case 0b01_1001: output = "MULTU";   break;
                        case 0b01_1010: output = "DIV";     break;
                        case 0b01_1011: output = "DIVU";    break;
                        case 0b10_0000: output = "ADD";     break;
                        case 0b10_0001: output = "ADDU";    break;
                        case 0b10_0011: output = "LUI";     break;
                        case 0b10_0100: output = "AND";     break;
                        case 0b10_0111: output = "NOR";     break;
                        case 0b10_1010: output = "SLT";     break;
                        case 0b10_1011: output = "SLTU";    break;
                        case 0b10_0101: output = "OR";
                            values = "R" + opcode.rd + "," + (REG[opcode.rs] | REG[opcode.rt]).ToString("x8");
                            break;
                        default: /*unimplementedWarning();*/ break;
                    }
                    break;
                case 0b00_0001:
                    switch (opcode.rt) {
                        case 0b00_0000: output = "BLTZ"; break;
                        case 0b00_0001: output = "BGEZ"; break;
                        default: /*unimplementedWarning();*/ break;
                    }
                    break;

                case 0b00_0010: //J();
                    output = "J";
                    values = ((PC_Predictor & 0xF000_0000) | (opcode.addr << 2)).ToString("x8");
                    break;
                case 0b00_0011: //JAL();
                    output = "JAL";
                    break;
                case 0b00_0100: //BEQ();
                    output = "BEQ";
                    break;
                case 0b00_0101: //BNE();
                    output = "BNE";
                    values = "R" + opcode.rs +"[" + REG[opcode.rs].ToString("x8") + "]" +"," + "R" + opcode.rt + "[" + REG[opcode.rt].ToString("x8") + "], (" + ((PC_Now)+(opcode.imm_s << 2)).ToString("x8") +")";
                    break;
                case 0b00_0110: //BLEZ();
                    output = "BLEZ";
                    break;
                case 0b00_0111: //BGTZ();
                    output = "BGTZ";
                    break;
                case 0b00_1000: //ADDI();
                    output = "ADDI";
                    int rs = (int)REG[opcode.rs];
                    int imm_s = (int)opcode.imm_s;
                    try {
                        uint addi = (uint)checked(rs + imm_s);
                        values = "R" + opcode.rs + "," + (addi).ToString("x8") + " R" + opcode.rs + "=" + REG[opcode.rs].ToString("x8"); ;
                        //Console.WriteLine("ADDI!");
                    } catch (OverflowException) {
                        values = "R" + opcode.rt + "," + REG[opcode.rs].ToString("x8") + " + "  + opcode.imm_s.ToString("x8") + " UNHANDLED OVERFLOW";
                    }
                    break;
                case 0b00_1001: //ADDIU();
                    // setReg(opcode.rt, REG[opcode.rs] + opcode.imm_s);
                    output = "ADDIU";
                    values = "R" + opcode.rt + "," + (REG[opcode.rs] + opcode.imm_s).ToString("x8");
                    break;
                case 0b00_1010: //SLTI();
                    output = "SLTI";
                    break;
                case 0b00_1011: //SLTIU();
                    output = "SLTIU";
                    break;
                                
                case 0b00_1100: //ANDI();
                    output = "ANDI";
                    break;
                case 0b00_1101: //ORI();
                    //setReg(opcode.rt, REG[opcode.rs] | opcode.imm);
                    output = "ORI";
                    values = "R" + opcode.rt + "," + (REG[opcode.rs] | opcode.imm).ToString("x8");
                    break;
                case 0b00_1111: //LUI();
                    //setReg(opcode.rt, opcode.imm << 16);
                    output = "LUI";
                    values = "R" + opcode.rt + "," + (opcode.imm << 16).ToString("x8");
                    break;


                case 0b01_0000: //CoProcessor Instructions
                    switch (opcode.format) {
                        case 0b0_0000://MFC0();
                            output = "MFC0";
                            break;
                        case 0b0_0100://MTC0();
                            // Cop0Reg[opcode.fs] = REG[opcode.ft];
                            output = "MTC0";
                            values = "R" + opcode.fs + "," + "R" + opcode.ft + "["+REG[opcode.ft].ToString("x8")+"]";
                            break;
                        case 0b1_0000: //RFE(); break;
                            output = "RFE";
                            break;
                        default: /*unimplementedWarning();*/ break;
                    }
                    break;

                case 0b10_0000:// LB(mmu);
                    output = "LB";
                    break;
                case 0b10_0001: //LH(mmu); break;
                    output = "LH";
                    break;
                case 0b10_0100:// LBU(mmu);
                    output = "LBU";
                    break;
                case 0b10_0101: //LHU(mmu); break;
                    output = "LHU";
                    break;
                case 0b10_0011:// LW(mmu);
                    if ((SR & 0x10000) == 0)
                        values = "R" + opcode.rt + "[" + REG[opcode.rt].ToString("x8") + "], " + opcode.imm_s.ToString("x8") + "(" + REG[opcode.rs].ToString("x8") + ")" + "[" + (opcode.imm_s + REG[opcode.rs]).ToString("x8") + "]";
                    else values = "R" + opcode.rt + "[" + REG[opcode.rt].ToString("x8") + "], " + opcode.imm_s.ToString("x8") + "(" + REG[opcode.rs].ToString("x8") + ")" + "[" + (opcode.imm_s + REG[opcode.rs]).ToString("x8") + "]" + " WARNING IGNORED LOAD";
                    output = "LW";
                    break;
                case 0b10_1001:// SH(mmu);
                    output = "SH";
                    break;
                case 0b10_1000:// SB(mmu);
                    output = "SB";
                    break;
                case 0b10_1011:// SW(mmu);
                    //if ((SR & 0x10000) == 0)
                    //    mmu.write32(REG[opcode.rs] + opcode.imm_s, REG[opcode.rt]);
                    output = "SW";
                    if ((SR & 0x10000) == 0)
                        values = "R" + opcode.rt + "["+REG[opcode.rt].ToString("x8") + "], " + opcode.imm_s.ToString("x8") + "(" + REG[opcode.rs].ToString("x8") +")" + "["+ (opcode.imm_s + REG[opcode.rs]).ToString("x8") + "]";
                    else values = "R" + opcode.rt + "[" + REG[opcode.rt].ToString("x8") + "], " + opcode.imm_s.ToString("x8") + "(" + REG[opcode.rs].ToString("x8") + ")" + "[" + (opcode.imm_s + REG[opcode.rs]).ToString("x8") + "]" + " WARNING IGNORED WRITE";
                    break;
                default:
                    break;
            }
            Console.WriteLine("{0,-8} {1,-8} {2,-8} {3,-8} {4,-20}", cycle, pc, load, output, values);
        }

        private void PrintRegs() {
            for(int i = 0; i < 32; i++) {
                string padding = (i < 10) ? "0" : "";
                Console.Write("{0,20}",
                "R" + padding + i + " " + REG[i].ToString("x8") + "");
            }
            Console.Write("{0,20}", "HI " + HI.ToString("x8"));
            Console.Write("{0,20}", "LO " + LO.ToString("x8") +"\n");
        }


    }
}