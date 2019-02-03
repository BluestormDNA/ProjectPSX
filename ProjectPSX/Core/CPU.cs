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

        private WB wb;
        private MEM mem;

        internal void Run(BUS bus) {
            fetchDecode(bus);
            Execute(bus);
            MemAccess();
            WriteBack();

            //debug
            TTY();
            //disassemble();
            //PrintRegs();
        }

        private void fetchDecode(BUS bus) {
            uint load = bus.load32(PC);
            PC_Now = PC;
            PC = PC_Predictor;
            PC_Predictor += 4;
            isDelaySlot = isBranch;
            isBranch = false;

            if((PC_Now % 4) != 0) {
                EXCEPTION(EX.LOAD_ADRESS_ERROR);
                return;
            }

            instr.Decode(load);
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

        private void Execute(BUS bus) {
            switch (instr.opcode) {
                case 0b00_0000: //R-Type opcodes
                    switch (instr.function) {
                        case 0b00_0000: SLL();            break;
                        case 0b00_0010: SRL();            break;
                        case 0b00_0011: SRA();            break;
                        case 0b00_0100: SLLV();           break;
                        case 0b00_0110: SRLV();           break;
                        case 0b00_0111: SRAV();           break;
                        case 0b00_1000: JR();             break;
                        case 0b00_1001: JALR();           break;
                        case 0b00_1100: SYSCALL();        break;
                        case 0b00_1101: BREAK();          break;
                        case 0b01_0000: MFHI();           break;
                        case 0b01_0001: MTHI();           break;
                        case 0b01_0010: MFLO();           break;
                        case 0b01_0011: MTLO();           break;
                        case 0b01_1000: MULT();           break;
                        case 0b01_1001: MULTU();          break;
                        case 0b01_1010: DIV();            break;
                        case 0b01_1011: DIVU();           break;
                        case 0b10_0000: ADD();            break;
                        case 0b10_0001: ADDU();           break;
                        case 0b10_0010: SUB();            break;
                        case 0b10_0011: SUBU();           break;
                        case 0b10_0100: AND();            break;
                        case 0b10_0101: OR();             break;
                        case 0b10_0110: XOR();            break;
                        case 0b10_0111: NOR();            break;
                        case 0b10_1010: SLT();            break;
                        case 0b10_1011: SLTU();           break;
                        default:
                            EXCEPTION(EX.ILLEGAL_INSTR);
                            unimplementedWarning();
                            break;
                    }
                    break;
                case 0b00_0001:
                    switch (instr.rt) {
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
                case 0b00_1110: XORI();                   break;
                case 0b00_1111: LUI();                    break;

                case 0b01_0000: //CoProcessor opcodes Cop0
                    switch (instr.format) {
                        case 0b0_0000: MFC0();            break;
                        case 0b0_0100: MTC0();            break;
                        case 0b1_0000: RFE();             break;
                        default: unimplementedWarning();  break;
                    }
                    break;

                case 0b01_0001: COP1();                   break;
                case 0b01_0010: COP2();                   break;
                case 0b01_0011: COP3();                   break;

                case 0b10_0000: LB(bus);                  break;
                case 0b10_0001: LH(bus);                  break;
                case 0b10_0010: LWL(bus);                 break;
                case 0b10_0011: LW(bus);                  break;
                case 0b10_0100: LBU(bus);                 break;
                case 0b10_0101: LHU(bus);                 break;
                case 0b10_0110: LWR(bus);                 break;
                case 0b10_1000: SB(bus);                  break;
                case 0b10_1001: SH(bus);                  break;
                case 0b10_1010: SWL(bus);                 break;
                case 0b10_1011: SW(bus);                  break;
                case 0b10_1110: SWR(bus);                 break;
                case 0b11_0000: //LWC0
                case 0b11_0001: //LWC1
                case 0b11_0011: //LWC3
                case 0b11_1000: //SWC0
                case 0b11_1001: //SWC1
                case 0b11_1011: UNIMPL_LW_SW_COP0_1_3();  break;
                case 0b11_0010: LWC2(bus);                break;
                case 0b11_1010: SWC2(bus);                break;
                //pending lwc0-3 and swc0-3 and illegal opc
                default:
                    EXCEPTION(EX.ILLEGAL_INSTR);
                    unimplementedWarning();
                    break;
            }
        }

        private void SWC2(BUS bus) {
            Console.WriteLine("Store at GTE");
            throw new NotImplementedException();
        }

        private void LWC2(BUS bus) {
            Console.WriteLine("Load from GTE");
            throw new NotImplementedException();
        }

        private void UNIMPL_LW_SW_COP0_1_3() { //PSX Unimplemented CoProcessor Ops
            EXCEPTION(EX.COPROCESSOR_ERROR);
        }

        private void SWR(BUS bus) {
            uint addr = REG[instr.rs] + instr.imm_s;
            uint aligned_addr = (uint)(addr & ~0b11);

            uint aligned_load = bus.load32(aligned_addr);

            uint value = 0;
            switch (addr & 0b11) {
                case 0: value = aligned_load | REG[instr.rt]; break;
                case 1: value = aligned_load & 0xFF | REG[instr.rt] << 8; break;
                case 2: value = aligned_load & 0xFFFF | REG[instr.rt] << 16; break;
                case 3: value = aligned_load & 0xFF_FFFF | REG[instr.rt] << 24; break;
            }

            bus.write32(addr, value);
        }

        private void SWL(BUS bus) {
            uint addr = REG[instr.rs] + instr.imm_s;
            uint aligned_addr = (uint)(addr & ~0b11);

            uint aligned_load = bus.load32(aligned_addr);

            uint value = 0;
            switch (addr & 0b11) {
                case 3: value = aligned_load | REG[instr.rt]; break;
                case 2: value = aligned_load & 0xFF | REG[instr.rt] >> 8; break;
                case 1: value = aligned_load & 0xFFFF | REG[instr.rt] >> 16; break;
                case 0: value = aligned_load & 0xFF_FFFF | REG[instr.rt] >> 24; break;
            }

            bus.write32(addr, value);
        }

        private void LWR(BUS bus) {
            uint addr = REG[instr.rs] + instr.imm_s;
            uint aligned_addr = (uint)(addr & ~0b11);

            uint aligned_load = bus.load32(aligned_addr);

            uint value = 0;
            switch (addr & 0b11) {
                case 0: value = aligned_load;       break;
                case 1: value = aligned_load >> 8;  break;
                case 2: value = aligned_load >> 16; break;
                case 3: value = aligned_load >> 24; break;
            }

            uint prev_value = mem.LoadRegNPostDelay == instr.rt ?
                mem.LoadValuePostDelay : REG[instr.rt];
            delayedLoad(instr.rt, prev_value | value);
        }

        private void LWL(BUS bus) {
            uint addr = REG[instr.rs] + instr.imm_s;
            uint aligned_addr = (uint)(addr & ~0b11);

            uint aligned_load = bus.load32(aligned_addr);

            uint value = 0;
            switch(addr & 0b11) {
                case 0: value = aligned_load << 24; break;
                case 1: value = aligned_load << 16; break;
                case 2: value = aligned_load << 8;  break;
                case 3: value = aligned_load;       break;
            }

            uint prev_value = mem.LoadRegNPostDelay == instr.rt ?
                mem.LoadValuePostDelay : REG[instr.rt];
            delayedLoad(instr.rt, prev_value | value);
        }

        private void COP2() {
            Console.WriteLine("GTE ACCESS");
            throw new NotImplementedException();
        }

        private void COP3() {
            EXCEPTION(EX.COPROCESSOR_ERROR);
        }

        private void COP1() {
            EXCEPTION(EX.COPROCESSOR_ERROR);
        }

        private void XORI() {
            setReg(instr.rt, REG[instr.rs] ^ REG[instr.imm]);
        }

        private void SUB() {
            int rs = (int)REG[instr.rs];
            int rt = (int)REG[instr.rt];
            try {
                uint sub = (uint)checked(rs - rt);
                setReg(instr.rd, sub);
            } catch (OverflowException) {
                EXCEPTION(EX.OVERFLOW);
            }
        }

        private void MULT() {
            long value = (int)REG[instr.rs] * (int)REG[instr.rt];

            HI = (uint)(value >> 32);
            LO = (uint)value;
        }

        private void BREAK() {
            EXCEPTION(EX.BREAK);
        }

        private void XOR() {
            setReg(instr.rd, REG[instr.rs] ^ REG[instr.rt]);
        }

        private void MULTU() {
            ulong value = REG[instr.rs] * REG[instr.rt];

            HI = (uint)(value >> 32);
            LO = (uint)value;
        }

        private void SRLV() {
            setReg(instr.rd, REG[instr.rt] >> (int)(REG[instr.rs] & 0x1F));
        }

        private void SRAV() {
            setReg(instr.rd, (uint)((int)REG[instr.rt] >> (int)(REG[instr.rs] & 0x1F)));
        }

        private void NOR() {
            setReg(instr.rd, ~(REG[instr.rs] | REG[instr.rt]));
        }

        private void LH(BUS bus) {
            if ((SR & 0x10000) == 0) {
                uint addr = REG[instr.rs] + instr.imm_s;

                if ((addr % 2) != 0) {
                    EXCEPTION(EX.LOAD_ADRESS_ERROR);
                } else {
                    uint value = (uint)(short)bus.load32(addr);
                    delayedLoad(instr.rt, value);
                }

            } //else Console.WriteLine("Ignoring Load");
        }

        private void SLLV() {
            setReg(instr.rd, REG[instr.rt] << (int)(REG[instr.rs] & 0x1F));
        }

        private void LHU(BUS bus) {
            if ((SR & 0x10000) == 0) {
                uint addr = REG[instr.rs] + instr.imm_s;

                if ((addr % 2) != 0) {
                    EXCEPTION(EX.LOAD_ADRESS_ERROR);
                } else {
                    uint value = (ushort)bus.load32(addr);
                    delayedLoad(instr.rt, value);
                }

            } //else Console.WriteLine("Ignoring Load");
        }

        private void RFE() {
            uint mode = SR & 0x3F;
            SR = (uint)(SR & ~0x3F);
            SR |= mode >> 2;
        }

        private void MTHI() {
            HI = REG[instr.rs];
        }

        private void MTLO() {
            LO = REG[instr.rs];
        }

        private void EXCEPTION(EX cause) {
            uint ExAdress;
            if((SR & (1 << 22)) != 0) {
                ExAdress = 0xBFC0_0180;
            } else {
                ExAdress = 0x8000_0080;
            }

            uint mode = SR & 0x3F;
            SR = (uint)(SR & ~0x3F);
            SR |= (mode << 2) & 0x3F;

            CAUSE = (uint)cause << 2;
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
            bool condition = (int)REG[instr.rs] < (int)REG[instr.rt];
            setReg(instr.rd, condition ? 1u : 0u);
        }

        private void MFHI() {
            setReg(instr.rd, HI);
        }

        private void DIVU() {
            uint n = REG[instr.rs];
            uint d = REG[instr.rt];

            if(d == 0) {
                HI = n;
                LO = 0xFFFF_FFFF;
            } else {
                HI = n % d;
                LO = n / d;
            }
        }

        private void SLTIU() {
            bool condition = REG[instr.rs] < instr.imm_s;
            setReg(instr.rt, condition ? 1u : 0u);
        }

        private void SRL() {
            setReg(instr.rd, REG[instr.rt] >> (int)instr.sa);
        }

        private void MFLO() {
            setReg(instr.rd, LO);
        }

        private void DIV() { //signed division
            int n = (int)REG[instr.rs];
            int d = (int)REG[instr.rt];

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
            setReg(instr.rd, (uint)((int)REG[instr.rt] >> (int)instr.sa));
        }

        private void SUBU() {
            setReg(instr.rd, REG[instr.rs] - REG[instr.rt]);
        }

        private void SLTI() {
            bool condition = (int)REG[instr.rs] < (int)instr.imm_s;
            setReg(instr.rt, condition ? 1u : 0u);
        }

        private void BRANCH() {
            isBranch = true;
            PC_Predictor -= 4;
            PC_Predictor += instr.imm_s << 2;
        }

        private void BGEZ() {
            if (((int)REG[instr.rs]) >= 0) {
                BRANCH();
            }
        }

        private void BLTZ() {
            if (((int)REG[instr.rs]) < 0) {
                BRANCH();
            }
        }

        private void JALR() {
            isBranch = true;
            setReg(instr.rd, PC_Predictor);
            JR();
        }

        private void LBU(BUS bus) { //todo recheck this
            if ((SR & 0x10000) == 0) {
                uint value = (byte)bus.load32(REG[instr.rs] + instr.imm_s);
                delayedLoad(instr.rt, value);
            } //else Console.WriteLine("Ignoring Load");
        }

        private void BLEZ() {
            if (((int)REG[instr.rs]) <= 0) {
                BRANCH();
            }
        }

        private void BGTZ() {
            if (((int)REG[instr.rs]) > 0) {
                BRANCH();
            }
        }

        private void ADD() {
            int rs = (int)REG[instr.rs];
            int rt = (int)REG[instr.rt];
            try {
                uint add = (uint)checked(rs + rt);
                setReg(instr.rd, add);
            } catch (OverflowException) {
                EXCEPTION(EX.OVERFLOW);
            }
        }

        private void AND() {
            setReg(instr.rd, REG[instr.rs] & REG[instr.rt]);
        }

        private void MFC0() {
            delayedLoad(instr.ft, Cop0Reg[instr.fs]);
        }

        private void BEQ() {
            if (REG[instr.rs] == REG[instr.rt]) {
                BRANCH();
            }
        }

        private void LB(BUS bus) { //todo redo this as it unnecesary load32
            if ((SR & 0x10000) == 0) {
                uint value = (uint)((sbyte)(bus.load32(REG[instr.rs] + instr.imm_s)));
                delayedLoad(instr.rt, value);
            } //else Console.WriteLine("Ignoring Write");
        }

        private void JR() {
            isBranch = true;
            PC_Predictor = REG[instr.rs];
        }

        private void SB(BUS bus) {
            if ((SR & 0x10000) == 0)
                bus.write8(REG[instr.rs] + instr.imm_s, (byte)REG[instr.rt]);
            //else Console.WriteLine("Ignoring Write");
        }

        private void ANDI() {
            setReg(instr.rt, REG[instr.rs] & instr.imm);
        }

        private void JAL() {
            setReg(31, PC_Predictor);
            J();
        }

        private void SH(BUS bus) {
            if ((SR & 0x10000) == 0) {
                uint addr = REG[instr.rs] + instr.imm_s;

                if ((addr % 2) != 0) {
                    EXCEPTION(EX.STORE_ADRESS_ERROR);
                } else {
                    bus.write16(addr, (ushort)REG[instr.rt]);
                }
            }
            //else Console.WriteLine("Ignoring Write");
        }

        private void ADDU() {
            setReg(instr.rd, REG[instr.rs] + REG[instr.rt]);
        }

        private void SLTU() {
            bool condition = REG[instr.rs] < REG[instr.rt];
            setReg(instr.rd, condition ? 1u : 0u);
        }

        private void LW(BUS bus) {
            if ((SR & 0x10000) == 0) {
                uint addr = REG[instr.rs] + instr.imm_s;

                if ((addr % 4) != 0) {
                    EXCEPTION(EX.LOAD_ADRESS_ERROR);
                } else {
                    uint value = bus.load32(addr);
                    delayedLoad(instr.rt, value);
                }

            } //else Console.WriteLine("Ignoring Load");
        }

        private void ADDI() {
            int rs = (int)REG[instr.rs];
            int imm_s = (int)instr.imm_s;
            try {
                uint addi = (uint) checked(rs + imm_s);
                setReg(instr.rt, addi);
            } catch (OverflowException) {
                EXCEPTION(EX.OVERFLOW);
            }
        }

        private void BNE() {
            if (REG[instr.rs] != REG[instr.rt]) {
                BRANCH();
            }
        }

        private void MTC0() {
            Cop0Reg[instr.fs] = REG[instr.ft];
        }

        private void OR() {
            setReg(instr.rd, REG[instr.rs] | REG[instr.rt]);
        }

        private void J() {
            isBranch = true;
            PC_Predictor = (PC_Predictor & 0xF000_0000) | (instr.addr << 2);
        }

        private void ADDIU() {
            setReg(instr.rt, REG[instr.rs] + instr.imm_s);
        }

        private void SLL() {
            setReg(instr.rd, REG[instr.rt] << (int)instr.sa);
        }

        private void SW(BUS bus) {
            if ((SR & 0x10000) == 0) {
                uint addr = REG[instr.rs] + instr.imm_s;

                if ((addr % 4) != 0) {
                    EXCEPTION(EX.STORE_ADRESS_ERROR);
                } else {
                    bus.write32(addr, REG[instr.rt]);
                }
            }
            //else Console.WriteLine("Ignoring Write");
        }

            private void LUI() {
            setReg(instr.rt, instr.imm << 16);
        }

        private void ORI() {
            setReg(instr.rt, REG[instr.rs] | instr.imm);
        }

        private void setReg(uint regN, uint value) {
            wb.WriteRegN = regN;
            wb.WriteValue = value;
        }

        private void delayedLoad(uint regN, uint value) {
            mem.LoadRegN = regN;
            mem.LoadValue = value;
        }

        private void unimplementedWarning() {
            Console.WriteLine("Unimplemented instr");
            string funct_string = instr.opcode == 0 ? " Function: " + instr.function.ToString("x8") : "";
            string format_string = instr.opcode == 0b01_0000 ? " Function: " + instr.format.ToString("x8") : "";
            Console.WriteLine("Cycle: " + cycle + " PC: " + PC_Now.ToString("x8") + " Load32: " + instr.value.ToString("x8")
                + " Instr: " + instr.opcode.ToString("x8") + funct_string + format_string);
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
            string load = instr.value.ToString("x8");
            string output = "";
            string values = "";

            switch (instr.opcode) {
                case 0b00_0000: //R-Type opcodes
                    switch (instr.function) {
                        case 0b00_0000: //SLL(); break;
                            if(instr.value == 0) output = "NOP";
                            else output = "SLL" + instr.rd;
                            break;
                        case 0b00_0010: output = "SRL";     break;
                        case 0b00_0011: output = "SRA";     break;
                        case 0b00_0100: output = "SLLV";    break;
                        case 0b00_0110: output = "SRLV";    break;
                        case 0b00_0111: output = "SRAV";    break;
                        case 0b00_1000: output = "JR";      break;
                        case 0b00_1001: output = "JALR";    break;
                        case 0b00_1100: output = "SYSCALL"; break;
                        case 0b00_1101: output = "BREAK";   break;
                        case 0b01_0000: output = "MFHI";    break;
                        case 0b01_0010: output = "MFLO";    break;
                        case 0b01_0011: output = "MTLO";    break;
                        case 0b01_1000: output = "MULT";    break;
                        case 0b01_1001: output = "MULTU";   break;
                        case 0b01_1010: output = "DIV";     break;
                        case 0b01_1011: output = "DIVU";    break;
                        case 0b10_0000: output = "ADD";     break;
                        case 0b10_0001: output = "ADDU";    break;
                        case 0b10_0010: output = "SUB";     break;
                        case 0b10_0011: output = "SUBU";    break;
                        case 0b10_0100: output = "AND";     break;
                        case 0b10_0101: output = "OR"; values = "R" + instr.rd + "," + (REG[instr.rs] | REG[instr.rt]).ToString("x8"); break;
                        case 0b10_0110: output = "XOR";     break;
                        case 0b10_0111: output = "NOR";     break;
                        case 0b10_1010: output = "SLT";     break;
                        case 0b10_1011: output = "SLTU";    break;
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
                    break;
                case 0b00_0101: //BNE();
                    output = "BNE";
                    values = "R" + instr.rs +"[" + REG[instr.rs].ToString("x8") + "]" +"," + "R" + instr.rt + "[" + REG[instr.rt].ToString("x8") + "], (" + ((PC_Now)+(instr.imm_s << 2)).ToString("x8") +")";
                    break;
                case 0b00_0110: //BLEZ();
                    output = "BLEZ";
                    break;
                case 0b00_0111: //BGTZ();
                    output = "BGTZ";
                    break;
                case 0b00_1000: //ADDI();
                    output = "ADDI";
                    int rs = (int)REG[instr.rs];
                    int imm_s = (int)instr.imm_s;
                    try {
                        uint addi = (uint)checked(rs + imm_s);
                        values = "R" + instr.rs + "," + (addi).ToString("x8") + " R" + instr.rs + "=" + REG[instr.rs].ToString("x8"); ;
                        //Console.WriteLine("ADDI!");
                    } catch (OverflowException) {
                        values = "R" + instr.rt + "," + REG[instr.rs].ToString("x8") + " + "  + instr.imm_s.ToString("x8") + " UNHANDLED OVERFLOW";
                    }
                    break;
                case 0b00_1001: //ADDIU();
                    // setReg(instr.rt, REG[instr.rs] + instr.imm_s);
                    output = "ADDIU";
                    values = "R" + instr.rt + "," + (REG[instr.rs] + instr.imm_s).ToString("x8");
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
                    //setReg(instr.rt, REG[instr.rs] | instr.imm);
                    output = "ORI";
                    values = "R" + instr.rt + "," + (REG[instr.rs] | instr.imm).ToString("x8");
                    break;
                case 0b00_1111: //LUI();
                    //setReg(instr.rt, instr.imm << 16);
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
                            values = "R" + instr.fs + "," + "R" + instr.ft + "["+REG[instr.ft].ToString("x8")+"]";
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
                    break;
                case 0b10_0011:// LW(bus);
                    if ((SR & 0x10000) == 0)
                        values = "R" + instr.rt + "[" + REG[instr.rt].ToString("x8") + "], " + instr.imm_s.ToString("x8") + "(" + REG[instr.rs].ToString("x8") + ")" + "[" + (instr.imm_s + REG[instr.rs]).ToString("x8") + "]";
                    else values = "R" + instr.rt + "[" + REG[instr.rt].ToString("x8") + "], " + instr.imm_s.ToString("x8") + "(" + REG[instr.rs].ToString("x8") + ")" + "[" + (instr.imm_s + REG[instr.rs]).ToString("x8") + "]" + " WARNING IGNORED LOAD";
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
                        values = "R" + instr.rt + "["+REG[instr.rt].ToString("x8") + "], " + instr.imm_s.ToString("x8") + "(" + REG[instr.rs].ToString("x8") +")" + "["+ (instr.imm_s + REG[instr.rs]).ToString("x8") + "]";
                    else values = "R" + instr.rt + "[" + REG[instr.rt].ToString("x8") + "], " + instr.imm_s.ToString("x8") + "(" + REG[instr.rs].ToString("x8") + ")" + "[" + (instr.imm_s + REG[instr.rs]).ToString("x8") + "]" + " WARNING IGNORED WRITE";
                    break;
                case 0b10_1110: output = "SW"; break;
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