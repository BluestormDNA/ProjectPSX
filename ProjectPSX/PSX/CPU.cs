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
        private uint STATUS { get { return COPROC0_REG[12]; } set { COPROC0_REG[12] = value; } }

        private struct MEM {
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

        internal void execute(MMU mmu) {

            //PC = PC_Predictor;
            opcode = next_opcode;
            uint load = mmu.read32(PC);
            next_opcode.Decode(load);
            //PC_Predictor += 4;
            PC += 4;

            debug(opcode);

            switch (opcode.instruction) {
                case 0b00_0000: //R-Type Instructions
                    switch (opcode.function) {
                        case 0b00_0000: SLL(opcode);        break;
                        case 0b10_0101: OR(opcode);         break;
                        default: unimplementedWarning(opcode);    break;
                    }
                    break;

                case 0b00_0010: J(opcode);          break;
                case 0b00_0101: BNE(opcode);        break;
                case 0b00_1000: ADDI(opcode);       break;
                case 0b00_1001: ADDIU(opcode);      break;

                case 0b00_1101: ORI(opcode);        break;
                case 0b00_1111: LUI(opcode);        break;

                case 0b01_0000: //CoProcessor Instructions
                    switch (opcode.format) {
                        case 0b0_0100: MTC0(opcode);    break;
                        default: unimplementedWarning(opcode); break;
                    }
                    break;
                case 0b10_0011: LW(opcode, mmu);    break;
                case 0b10_1011: SW(opcode, mmu);    break;
                default:
                    PC -= 4;
                    unimplementedWarning(opcode);
                    break;
            }
        }

        private void LW(Opcode opcode, MMU mmu) {
            if ((STATUS & 0x10000) == 0) {
                uint lw = mmu.read32(REG[opcode.rs] + opcode.imm_s);
                setReg(REG[opcode.rt], lw);
            }
            else Console.WriteLine("Ignoring Write");
        }

        private void ADDI(Opcode opcode) {
            int rs = (int)REG[opcode.rs];
            int imm_s = (int)opcode.imm_s;
            try {
                uint addi = (uint) checked(rs + imm_s);
                setReg(opcode.rt, addi);
                //Console.WriteLine("ADDI rt " + REG[opcode.rt].ToString("x4") + " value" + addi.ToString("x4"));
                //Console.WriteLine("ADDI REG10 " + REG[10].ToString("x4") + " REG11 " + REG[11].ToString("x4"));
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
            if ((STATUS & 0x10000) == 0)
                mmu.write32(REG[opcode.rs] + opcode.imm_s, REG[opcode.rt]);
            else Console.WriteLine("Ignoring Write");
        }

        private void LUI(Opcode opcode) {
            setReg(opcode.rt, opcode.imm << 16);
        }

        private void ORI(Opcode opcode) {
            setReg(opcode.rt, REG[opcode.rs] | opcode.imm);
        }

        private void setReg(uint regN, uint value) {
            REG[regN] = value;
            REG[0] = 0;
        }

        private void unimplementedWarning(Opcode opcode) {
            Console.WriteLine("Unimplemented OPCODE");
            debug(opcode);
            throw new NotImplementedException();
        }

        private void debug(Opcode opcode) {
            string funct_string = opcode.instruction == 0 ? " Function: " + opcode.function.ToString("x4") : "";
            Console.WriteLine("PC: " + (PC - 8).ToString("x4") + " Load32: " + opcode.load.ToString("x4")
                + " Instr: " + opcode.instruction.ToString("x4") + funct_string);
        }


    }
}