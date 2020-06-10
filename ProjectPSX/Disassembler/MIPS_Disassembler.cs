using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ProjectPSX.CPU;

namespace ProjectPSX.Disassembler {
    internal class MIPS_Disassembler {

        public MIPS_Disassembler(ref uint HI, ref uint LO, uint[] GPR, uint[] COP0_GPR) { //TODO FIX HI LO
            this.HI = HI;
            this.LO = LO;
            this.GPR = GPR;
            this.COP0_GPR = COP0_GPR;
        }

        int dev;
        StringBuilder str = new StringBuilder();

        private uint HI;
        private uint LO;
        private uint[] GPR;
        private uint[] COP0_GPR;

        private int cycle;

        private const int SR = 12;
        private const int CAUSE = 13;
        private const int EPC = 14;
        private const int BADA = 8;
        private const int JUMPDEST = 6;

        public void output(Instr instr, uint PC_Now) {
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


        public void disassemble(Instr instr, uint PC_Now, uint PC_Predictor) {
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
                        case 0b00_0010: output = "SRL R" + instr.rs + " " + (GPR[instr.rt] >> (int)instr.sa); break;
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
                        case 0b10_0100: output = "AND R" + instr.rd + " R" + instr.rs + " & R" + instr.rt + " " + GPR[instr.rs].ToString("x8") + " & " + GPR[instr.rt].ToString("x8"); break;
                        case 0b10_0101: output = "OR"; values = "R" + instr.rd + "," + (GPR[instr.rs] | GPR[instr.rt]).ToString("x8"); break;
                        case 0b10_0110: output = "XOR R" + instr.rd + " R" + instr.rs + " ^ R" + instr.rt + " " + GPR[instr.rs].ToString("x8") + " ^ " + GPR[instr.rt].ToString("x8"); break;
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
                    values = "R" + instr.rt + ", " + "R " + GPR[instr.rs] + "AND " + instr.imm + "=" + (GPR[instr.rs] & instr.imm).ToString("x8");
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
                case 0b11_0010: output = "LWC2"; break;
                case 0b11_1010: output = "SWC2";
                                values = "GTE R" + instr.rt +" -> R" + instr.rs + "= " + GPR[instr.rs].ToString("x8") + " + imm: " + instr.imm.ToString("x8") + " = " + (GPR[instr.rs] + instr.imm).ToString("x8"); break;
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
