using System;

namespace ProjectPSX {
    internal class CPU {

        private uint PC = 0xbfc0_0000;
        //private uint PC_Predictor = 0xbfc0_0000; // Bios Entry Point
        private uint[] REG = new uint[32];
        private uint HI;
        private uint LO;

        //CoPro Regs
        private uint[] COPROC0_REG = new uint[16];
        private uint SR { get { return COPROC0_REG[12]; } set { COPROC0_REG[12] = value; } }

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
            public uint load; //debug
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
                load = opcode;
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
        private Opcode next_opcode; //used as MIPS execute next opcode even if branch (Branch Delay Slot)

        private WB wb;
        private MEM mem;

        internal void Run(MMU mmu) {
            //PC = PC_Predictor;
            opcode = next_opcode;
            uint load = mmu.read32(PC);
            next_opcode.Decode(load);
            //PC_Predictor += 4;
            PC += 4;

            disassemble(opcode, mmu);
            Execute(opcode, mmu);
            MemAccess();
            WriteBack();
            cycle++;
            //debug(opcode);
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
            REG[0] = 0;
        }

        private void Execute(Opcode opcode, MMU mmu) {
            switch (opcode.instruction) {
                case 0b00_0000: //R-Type Instructions
                    switch (opcode.function) {
                        case 0b00_0000: SLL(opcode);            break;
                        case 0b00_0010: SRL(opcode);            break;
                        case 0b00_0011: SRA(opcode);            break;
                        case 0b00_1000: JR(opcode);             break;
                        case 0b00_1001: JALR(opcode);           break;
                        case 0b01_0010: MFLO(opcode);           break;
                        case 0b01_1010: DIV(opcode);            break;
                        case 0b10_0000: ADD(opcode);            break;
                        case 0b10_0001: ADDU(opcode);           break;
                        case 0b10_0011: SUBU(opcode);           break;
                        case 0b10_0100: AND(opcode);            break;
                        case 0b10_1011: SLTU(opcode);           break;
                        case 0b10_0101: OR(opcode);             break;
                        default: unimplementedWarning(opcode);  break;
                    }
                    break;
                case 0b00_0001:
                    switch (opcode.rt) {
                        case 0b00_0000: BLTZ(opcode);           break;
                        case 0b00_0001: BGEZ(opcode);           break;
                        default: unimplementedWarning(opcode);  break;
                    }
                    break;
                
                case 0b00_0010: J(opcode);                      break;
                case 0b00_0011: JAL(opcode);                    break;
                case 0b00_0100: BEQ(opcode);                    break;
                case 0b00_0101: BNE(opcode);                    break;
                case 0b00_0110: BLEZ(opcode);                   break;
                case 0b00_0111: BGTZ(opcode);                   break;
                case 0b00_1000: ADDI(opcode);                   break;
                case 0b00_1001: ADDIU(opcode);                  break;
                case 0b00_1010: SLTI(opcode);                   break;
                case 0b00_1011: SLTIU(opcode);                  break;

                case 0b00_1100: ANDI(opcode);                   break;
                case 0b00_1101: ORI(opcode);                    break;
                case 0b00_1111: LUI(opcode);                    break;

                case 0b01_0000: //CoProcessor Instructions
                    switch (opcode.format) {
                        case 0b0_0000: MFC0(opcode);            break;
                        case 0b0_0100: MTC0(opcode);            break;
                        default: unimplementedWarning(opcode);  break;
                    }
                    break;
               
                case 0b10_0000: LB(opcode, mmu);                break;
                case 0b10_0100: LBU(opcode, mmu);               break;
                case 0b10_0011: LW(opcode, mmu);                break;
                case 0b10_1001: SH(opcode, mmu);                break;
                case 0b10_1000: SB(opcode, mmu);                break;
                case 0b10_1011: SW(opcode, mmu);                break;
                default:
                    PC -= 4;
                    unimplementedWarning(opcode);
                    break;
            }
        }

        private void SLTIU(Opcode opcode) {
            //unimplementedWarning(opcode);
            bool condition = REG[opcode.rs] < opcode.imm_s;
            setReg(opcode.rt, condition ? 1u : 0u);
        }

        private void SRL(Opcode opcode) {
            setReg(opcode.rd, REG[opcode.rt] >> (int)opcode.sa);
        }

        private void MFLO(Opcode opcode) {
            setReg(opcode.rd, LO);
        }

        private void DIV(Opcode opcode) { //signed division
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

        private void SRA(Opcode opcode) { //TODO revisit this
            //setReg(opcode.rd, (uint)((int)REG[opcode.rt] >> (int)opcode.sa));
            setReg(opcode.rd, (uint)(REG[opcode.rt] >> (int)opcode.sa | REG[opcode.rt] & 0x8000_0000));
        }

        private void SUBU(Opcode opcode) {
            setReg(opcode.rd, REG[opcode.rs] - REG[opcode.rt]);
        }

        private void SLTI(Opcode opcode) {
            bool condition = (int)REG[opcode.rs] < (int)opcode.imm_s;
            setReg(opcode.rt, condition ? 1u : 0u);
        }

        private void BGEZ(Opcode opcode) {
            if (((int)REG[opcode.rs]) >= 0) {
                PC -= 4;
                PC += opcode.imm_s << 2;
            }
        }

        private void BLTZ(Opcode opcode) {
            if (((int)REG[opcode.rs]) < 0) {
                PC -= 4;
                PC += opcode.imm_s << 2;
            }
        }

        private void JALR(Opcode opcode) {
            setReg(opcode.rd, PC);
            PC = REG[opcode.rs];
        }

        private void LBU(Opcode opcode, MMU mmu) { //todo recheck this
            if ((SR & 0x10000) == 0) {
                uint lbu = (byte)mmu.read32(REG[opcode.rs] + opcode.imm_s);
                mem.LoadRegN = opcode.rt;
                mem.LoadValue = lbu;
            } else Console.WriteLine("Ignoring Write");
        }

        private void BLEZ(Opcode opcode) {
            if (((int)REG[opcode.rs]) <= 0) {
                PC -= 4;
                PC += opcode.imm_s << 2;
            }
            //debug(opcode);
            //disassemble(opcode);
            //Console.ReadLine();
        }

        private void BGTZ(Opcode opcode) {
            if (((int)REG[opcode.rs]) > 0) {
                PC -= 4;
                PC += opcode.imm_s << 2;
            }
        }

        private void ADD(Opcode opcode) {
            int rs = (int)REG[opcode.rs];
            int rt = (int)REG[opcode.rt];
            try {
                uint add = (uint)checked(rs + rt);
                setReg(opcode.rd, add);
                //Console.WriteLine("ADD!");
            } catch (OverflowException) {
                //Console.WriteLine("WARNING ADDI OVERFLOW!");
            }
            //In theory till here is ok <---- TODO
        }

        private void AND(Opcode opcode) {
            setReg(opcode.rd, REG[opcode.rs] & REG[opcode.rt]);
        }

        private void MFC0(Opcode opcode) {
            mem.LoadRegN = opcode.ft;
            mem.LoadValue = COPROC0_REG[opcode.fs];
        }

        private void BEQ(Opcode opcode) {
            if (REG[opcode.rs] == REG[opcode.rt]) {
                PC -= 4;
                PC += opcode.imm_s << 2;
            }
        }

        private void LB(Opcode opcode, MMU mmu) { //todo redo this as it unnecesary read32
            if ((SR & 0x10000) == 0) {
                uint lb = (uint)((sbyte)(mmu.read32(REG[opcode.rs] + opcode.imm_s)));
                mem.LoadRegN = opcode.rt;
                mem.LoadValue = lb;
            } else Console.WriteLine("Ignoring Write");
        }

        private void JR(Opcode opcode) {
            PC = REG[opcode.rs];
        }

        private void SB(Opcode opcode, MMU mmu) {
            if ((SR & 0x10000) == 0)
                mmu.write8(REG[opcode.rs] + opcode.imm_s, (byte)REG[opcode.rt]);
            //else Console.WriteLine("Ignoring Write");
        }

        private void ANDI(Opcode opcode) {
            setReg(opcode.rt, REG[opcode.rs] & opcode.imm);
        }

        private void JAL(Opcode opcode) {
            setReg(31, PC);
            PC = (PC & 0xF000_0000) | (opcode.addr << 2);
        }

        private void SH(Opcode opcode, MMU mmu) {
            if ((SR & 0x10000) == 0)
                mmu.write16(REG[opcode.rs] + opcode.imm_s, (ushort)REG[opcode.rt]);
            //else Console.WriteLine("Ignoring Write");
        }

        private void ADDU(Opcode opcode) {
            setReg(opcode.rd, REG[opcode.rs] + REG[opcode.rt]);
        }

        private void SLTU(Opcode opcode) {
            bool condition = REG[opcode.rs] < REG[opcode.rt];
            setReg(opcode.rd, condition ? 1u : 0u);
        }

        private void LW(Opcode opcode, MMU mmu) {
            if ((SR & 0x10000) == 0) {
                uint lw = mmu.read32(REG[opcode.rs] + opcode.imm_s);
                mem.LoadRegN = opcode.rt;
                mem.LoadValue = lw;
            }
            else Console.WriteLine("Ignoring Read");
        }

        private void ADDI(Opcode opcode) {
            int rs = (int)REG[opcode.rs];
            int imm_s = (int)opcode.imm_s;
            try {
                uint addi = (uint) checked(rs + imm_s);
                setReg(opcode.rt, addi);
                //Console.WriteLine("ADDI!");
            } catch (OverflowException) {
                //Console.WriteLine("WARNING ADDI OVERFLOW!");
            }
        }

        private void BNE(Opcode opcode) {
            //Console.WriteLine("BNE Comparing RS " + REG[opcode.rs].ToString("x4") + "  RT " + REG[opcode.rt].ToString("x4"));
            if (REG[opcode.rs] != REG[opcode.rt]) {
                PC -= 4;
                PC += opcode.imm_s << 2;
            }
        }

        private void MTC0(Opcode opcode) {
            COPROC0_REG[opcode.fs] = REG[opcode.ft];
        }

        private void OR(Opcode opcode) {
            setReg(opcode.rd, REG[opcode.rs] | REG[opcode.rt]);
        }

        private void J(Opcode opcode) {
            PC = (PC & 0xF000_0000) | (opcode.addr << 2);
        }

        private void ADDIU(Opcode opcode) {
            setReg(opcode.rt, REG[opcode.rs] + opcode.imm_s);
        }

        private void SLL(Opcode opcode) {
            setReg(opcode.rd, REG[opcode.rt] << (int)opcode.sa);
        }

        private void SW(Opcode opcode, MMU mmu) {
            if ((SR & 0x10000) == 0)
                mmu.write32(REG[opcode.rs] + opcode.imm_s, REG[opcode.rt]);
            //else Console.WriteLine("Ignoring Write");
            //Console.WriteLine("Just in case... READ: " + mmu.read32(REG[opcode.rs] + opcode.imm_s).ToString("x8"));
        }

        private void LUI(Opcode opcode) {
            setReg(opcode.rt, opcode.imm << 16);
        }

        private void ORI(Opcode opcode) {
            setReg(opcode.rt, REG[opcode.rs] | opcode.imm);
        }

        private void setReg(uint regN, uint value) {
            wb.WriteRegN = regN;
            wb.WriteValue = value;
        }

        private void unimplementedWarning(Opcode opcode) {
            Console.WriteLine("Unimplemented OPCODE");
            debug(opcode);
            throw new NotImplementedException();
        }

        long cycle;
        private void debug(Opcode opcode) {
            string funct_string = opcode.instruction == 0 ? " Function: " + opcode.function.ToString("x4") : "";
            Console.WriteLine("Cycle: " + cycle + " PC: " + (PC - 8).ToString("x4") + " Load32: " + opcode.load.ToString("x4")
                + " Instr: " + opcode.instruction.ToString("x4") + funct_string);
            //Console.WriteLine("Debug: REG31 " + REG[31].ToString("x4"));
            if (PC == 0x000000B4 && REG[9] == 0x3D) {
                Console.WriteLine("TEXT!!!!!!!!!!!!!!!!: " + (char)REG[4]);
            }
            PrintRegs();
        }

        private void disassemble(Opcode opcode, MMU mmu) {
            string pc = (PC - 8).ToString("x8");
            string load = opcode.load.ToString("x8");
            string output = "";
            string values = "";

            switch (opcode.instruction) {
                case 0b00_0000: //R-Type Instructions
                    switch (opcode.function) {
                        case 0b00_0000: //SLL(opcode); break;
                            //setReg(opcode.rd, REG[opcode.rt] << (int)opcode.sa);
                            if(opcode.load == 0) {
                                output = "NOP";
                            } else {
                                output = "SLL" + opcode.rd;
                            }
                            break;
                        case 0b00_0010: //SRL(opcode);
                            output = "SRL";
                            break;
                        case 0b00_0011: //SRA(opcode);
                            output = "SRA";
                            break;
                        case 0b00_1000: //JR(opcode);
                            output = "JR";
                            break;
                        case 0b00_1001: //JALR(opcode);
                            output = "JALR";
                            break;
                        case 0b01_0010: //MFLO(opcode);
                            output = "MFLO";
                            break;
                        case 0b01_1010: //DIV(opcode)
                            output = "DIV";
                            ; break;
                        case 0b10_0000: //ADD(opcode);
                            output = "ADD";
                            break;
                        case 0b10_0001: //ADDU(opcode);
                            output = "ADDU";
                            break;
                        case 0b10_0011: //SUBU(opcode);
                            output = "LUI";
                            break;
                        case 0b10_0100: //AND(opcode);
                            output = "LUI";
                            break;
                        case 0b10_1011: //SLTU(opcode);
                            output = "SLTU";
                            break;
                        case 0b10_0101: //OR(opcode);
                            // setReg(opcode.rd, REG[opcode.rs] | REG[opcode.rt]);
                            output = "OR";
                            values = "R" + opcode.rd + "," + (REG[opcode.rs] | REG[opcode.rt]).ToString("x8");
                            break;
                        default: unimplementedWarning(opcode); break;
                    }
                    break;
                case 0b00_0001:
                    switch (opcode.rt) {
                        case 0b00_0000: //BLTZ(opcode);
                            output = "BLTZ";
                            break;
                        case 0b00_0001: //BGEZ(opcode);
                            output = "BGEZ";
                            break;
                        default: unimplementedWarning(opcode); break;
                    }
                    break;

                case 0b00_0010: //J(opcode);
                    // PC = (PC & 0xF000_0000) | (opcode.addr << 2);
                    output = "J";
                    values = ((PC & 0xF000_0000) | (opcode.addr << 2)).ToString("x8");
                    break;
                case 0b00_0011: //JAL(opcode);
                    output = "JAL";
                    break;
                case 0b00_0100: //BEQ(opcode);
                    output = "BEQ";
                    break;
                case 0b00_0101: //BNE(opcode);
                    /*
                      if (REG[opcode.rs] != REG[opcode.rt]) {
                        PC -= 4;
                        PC += opcode.imm_s << 2;
                      }
                     */
                    output = "BNE";
                    values = "R" + opcode.rs +"[" + REG[opcode.rs].ToString("x8") + "]" +"," + "R" + opcode.rt + "[" + REG[opcode.rt].ToString("x8") + "], (" + ((PC-4)+(opcode.imm_s << 2)).ToString("x8") +")";
                    break;
                case 0b00_0110: //BLEZ(opcode);
                    output = "BLEZ";
                    break;
                case 0b00_0111: //BGTZ(opcode);
                    output = "BGTZ";
                    break;
                case 0b00_1000: //ADDI(opcode);
                    //int rs = (int)REG[opcode.rs];
                    //int imm_s = (int)opcode.imm_s;
                    //try {
                    //    uint addi = (uint)checked(rs + imm_s);
                    //    setReg(opcode.rt, addi);
                    //    //Console.WriteLine("ADDI!");
                    //} catch (OverflowException) {
                    //    //Console.WriteLine("WARNING ADDI OVERFLOW!");
                    //}
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
                case 0b00_1001: //ADDIU(opcode);
                    // setReg(opcode.rt, REG[opcode.rs] + opcode.imm_s);
                    output = "ADDIU";
                    values = "R" + opcode.rt + "," + (REG[opcode.rs] + opcode.imm_s).ToString("x8");
                    break;
                case 0b00_1010: //SLTI(opcode);
                    output = "SLTI";
                    break;
                case 0b00_1011: //SLTIU(opcode);
                    output = "SLTIU";
                    break;
                                
                case 0b00_1100: //ANDI(opcode);
                    output = "ANDI";
                    break;
                case 0b00_1101: //ORI(opcode);
                    //setReg(opcode.rt, REG[opcode.rs] | opcode.imm);
                    output = "ORI";
                    values = "R" + opcode.rt + "," + (REG[opcode.rs] | opcode.imm).ToString("x8");
                    break;
                case 0b00_1111: //LUI(opcode);
                    //setReg(opcode.rt, opcode.imm << 16);
                    output = "LUI";
                    values = "R" + opcode.rt + "," + (opcode.imm << 16).ToString("x8");
                    break;


                case 0b01_0000: //CoProcessor Instructions
                    switch (opcode.format) {
                        case 0b0_0000://MFC0(opcode);
                            output = "MFC0";
                            break;
                        case 0b0_0100://MTC0(opcode);
                            // COPROC0_REG[opcode.fs] = REG[opcode.ft];
                            output = "MTC0";
                            values = "R" + opcode.fs + "," + "R" + opcode.ft + "["+REG[opcode.ft].ToString("x8")+"]";
                            break;
                        default: unimplementedWarning(opcode); break;
                    }
                    break;

                case 0b10_0000:// LB(opcode, mmu);
                    output = "LB";
                    break;
                case 0b10_0100:// LBU(opcode, mmu);
                    output = "LBU";
                    break;
                case 0b10_0011:// LW(opcode, mmu);
                    //if ((SR & 0x10000) == 0) {
                    //    uint lw = mmu.read32(REG[opcode.rs] + opcode.imm_s);
                    //    mem.LoadRegN = opcode.rt;
                    //    mem.LoadValue = lw;
                    //} else Console.WriteLine("Ignoring Write");
                    if ((SR & 0x10000) == 0)
                        values = "R" + opcode.rt + "[" + REG[opcode.rt].ToString("x8") + "], " + opcode.imm_s.ToString("x8") + "(" + REG[opcode.rs].ToString("x8") + ")" + "[" + (opcode.imm_s + REG[opcode.rs]).ToString("x8") + "]" + " READ = "+mmu.read32(REG[opcode.rs] + opcode.imm_s).ToString("x8");
                    else values = "R" + opcode.rt + "[" + REG[opcode.rt].ToString("x8") + "], " + opcode.imm_s.ToString("x8") + "(" + REG[opcode.rs].ToString("x8") + ")" + "[" + (opcode.imm_s + REG[opcode.rs]).ToString("x8") + "]" + " READ = " + mmu.read32(REG[opcode.rs] + opcode.imm_s).ToString("x8") + " WARNING IGNORED LOAD";
                    output = "LW";
                    break;
                case 0b10_1001:// SH(opcode, mmu);
                    output = "SH";
                    break;
                case 0b10_1000:// SB(opcode, mmu);
                    output = "SB";
                    break;
                case 0b10_1011:// SW(opcode, mmu);
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
            PrintRegs();
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