using ProjectPSX.Devices;
using System;

namespace ProjectPSX {
    class GTE : Device { //PSX MIPS Coprocessor 02 - Geometry Transformation Engine

        struct Matrix {
            public Vector3 v1;
            public Vector3 v2;
            public Vector3 v3;
        }

        struct Vector3 {
            public short X;
            public short Y;
            public short Z;

            public uint XY {
                get { return ((uint)(ushort)Y << 16) | (uint)(ushort)X; }
                set { Y = (short)(value >> 16); X = (short)value; }
            }
            public uint OZ {
                get { return (uint)Z; }
                set { Z = (short)value; }
            }
        }

        struct Vector2 {
            public short x;
            public short y;

            public uint get() {
                return ((uint)(ushort)y << 16 | (uint)(ushort)x);
            }
            public void set(uint value) {
                x = (short)(value & 0x0000_FFFF);
                y = (short)((value & 0xFFFF_0000) >> 16);
            }
        }

        struct Color {
            public byte r;
            public byte g;
            public byte b;
            public byte c;

            public uint get() {
                return (uint)(c << 24 | b << 16 | g << 8 | r);
            }
            public void set(uint value) {
                r = (byte)(value & 0x0000_00FF);
                g = (byte)((value & 0x0000_FF00) >> 8);
                b = (byte)((value & 0x00FF_0000) >> 16);
                c = (byte)((value & 0xFF00_0000) >> 24);
            }// more
        }

        //Data Registers
        Vector3[] V = new Vector3[3];   //R0-1 R2-3 R4-5 s16
        Color RGBC;                     //R6
        ushort OTZ;                     //R7
        short[] IR = new short[4];      //R8-11
        Vector2[] SXY = new Vector2[4]; //R12-15 FIFO
        ushort[] SZ = new ushort[4];    //R16-19 FIFO
        Color[] RGB = new Color[3];     //R20-22 FIFO
        uint RES1;                      //R23 prohibited
        int MAC0;                       //R24
        int MAC1, MAC2, MAC3;           //R25-27
        ushort IRGB, ORGB;              //R28-29
        int LZCS, LZCR;                 //R30-31

        //Control Registers
        Matrix RT, LM, LRGB;        //R32-36 R40-44 R48-52
        int TRX, TRY, TRZ;          //R37-39
        int RBK, GBK, BBK;          //R45-47
        int RFC, GFC, BFC;          //R53-55
        int OFX, OFY, DQB;          //R56 57 60
        ushort H;                   //R58
        short ZSF3, ZSF4, DQA;      //R61 62 59
        uint FLAG;                  //R63

        //Command decode
        int sf;                     //Shift fraction (0 or 12)
        uint MVMVA_M_Matrix;         //MVMVA Multiply Matrix    (0=Rotation. 1=Light, 2=Color, 3=Reserved)
        uint MVMVA_M_Vector;         //MVMVA Multiply Vector    (0=V0, 1=V1, 2=V2, 3=IR/long)
        uint MVMVA_T_Vector;         //MVMVA Translation Vector (0=TR, 1=BK, 2=FC/Bugged, 3=None)
        bool lm;                     //Saturate IR1,IR2,IR3 result (0=To -8000h..+7FFFh, 1=To 0..+7FFFh)
        uint opcode;                 //GTE opcode
        CPU cpu;
        public GTE(CPU cpu) {//debug purposes
            this.cpu = cpu;
        }
        internal void execute(uint imm) {
            //Console.WriteLine("GTE EXECUTE" + (imm & 0x3F).ToString("x8"));

            decodeCommand(imm);
            FLAG = 0;

            switch (opcode) {
                case 0x01: RTPS(0); break;
                case 0x06: NCLIP(); break;
                case 0x0C: OP(); break;
                case 0x13: NCDS(0); break;
                case 0x16: NCDT(); break;
                case 0x28: SQR(); break;
                case 0x2D: AVSZ3(); break;
                case 0x2E: AVSZ4(); break;
                case 0x30: RTPT(); break;
                default: Console.Write("UNIMPLEMENTED GTE COMMAND"); break;/* throw new NotImplementedException();*/
            }
        }

        private void NCDT() {
            NCDS(0);
            NCDS(1);
            NCDS(2);
        }

        private void OP() {
            //[MAC1, MAC2, MAC3] = [IR3*D2-IR2*D3, IR1*D3-IR3*D1, IR2*D1-IR1*D2] SAR(sf*12)
            //[IR1, IR2, IR3]    = [MAC1, MAC2, MAC3]                        ;copy result
            short d1 = RT.v1.X;
            short d2 = RT.v2.Y;
            short d3 = RT.v3.Z;

            MAC1 = ((IR[3] * d2) - (IR[2] * d3)) >> sf * 12;
            MAC2 = ((IR[1] * d3) - (IR[3] * d1)) >> sf * 12;
            MAC3 = ((IR[2] * d1) - (IR[1] * d2)) >> sf * 12;

            IR[1] = (short)MAC1;
            IR[2] = (short)MAC2;
            IR[3] = (short)MAC3;
        }

        private int sqr;
        private void SQR() {
            long mac1 = (long)(IR[1] * IR[1]) >> sf * 12;
            long mac2 = (long)(IR[2] * IR[2]) >> sf * 12;
            long mac3 = (long)(IR[3] * IR[3]) >> sf * 12;

            MAC1 = (int)mac1;
            MAC2 = (int)mac2;
            MAC3 = (int)mac3;


            IR[1] = (short)(MAC1 > 0x7FFF ? 0x7FFF : MAC1);
            IR[2] = (short)(MAC2 > 0x7FFF ? 0x7FFF : MAC2);
            IR[3] = (short)(MAC3 > 0x7FFF ? 0x7FFF : MAC3);

            if(MAC1 > 0x7FFF) {  //todo mac to array and loop here
                IR[1] = 0x7FFF;
                FLAG |= (1 << 24);
            }

            if (MAC2 > 0x7FFF) {
                IR[2] = 0x7FFF;
                FLAG |= (1 << 23);
            }
            if (MAC3 > 0x7FFF) {
                IR[3] = 0x7FFF;
                FLAG |= (1 << 22);
            }

            if (mac1 < -0x800_0000_0000) { //TODO FLAGS
                FLAG |= (1 << 27);
            }
            if (mac1 > 0x7FF_FFFF_FFFF) {
                FLAG |= (1 << 30);
            }

            if ((mac2 < -0x800_0000_0000)) {
                FLAG |= (1 << 26);
            }
            if (mac2 > 0x7FF_FFFF_FFFF) {
                FLAG |= (1 << 29);
            }
            
            if (mac3 < -0x800_0000_0000) {
                FLAG |= (1 << 25);
            }
            if (mac3 > 0x7FF_FFFF_FFFF) {
                FLAG |= (1 << 28);
            }
            //if(sqr > 4094) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(">SQR< " + sqr);
                //Console.ReadLine();
                Console.ResetColor();
            //}
            sqr++;

            //Console.WriteLine("FLAG " + FLAG.ToString("x8"));
            //Console.WriteLine(MAC1.ToString("x8") + " " + MAC2.ToString("x8") + " " + MAC3.ToString("x8"));
            //Console.WriteLine(IR[1].ToString("x8") + " " + IR[2].ToString("x8") + " " + IR[3].ToString("x8"));
            //if(sqr == 4094) {
            //cpu.disassemble();
            //cpu.PrintRegs();
            //cpu.output();
            //}
        }

        private void decodeCommand(uint imm) {
            sf = (int)(imm & 0x1 << 19);
            MVMVA_M_Matrix = imm >> 17 & 0x3;
            MVMVA_M_Vector = imm >> 15 & 0x3;
            MVMVA_T_Vector = imm >> 13 & 0x3;
            lm = (imm >> 10 & 0x1) != 0;
            opcode = imm & 0x3F;
        }
        int avsz3test;
        private void AVSZ3() {
            //MAC0 = ZSF3 * (SZ1 + SZ2 + SZ3); for AVSZ3
            //OTZ = MAC0 / 1000h;for both(saturated to 0..FFFFh)
            long avsz3 = (long)ZSF3 * (SZ[1] + SZ[2] + SZ[3]);
            MAC0 = (int)avsz3;

            long div = avsz3 / 0x1000;
            if (div < 0) {
                div = 0;
            } else if (div > 0xFFFF) {
                div = 0xFFFF;
            }
            OTZ = (ushort)div;

            Console.WriteLine("avsz3 " + avsz3test++ + " ZSF3: " + ZSF3.ToString("x4") + " MAC0: " + MAC0.ToString("x8") + " Mac0: " + MAC0 + " div: " + div + " divAnd: " + (div & 0x7FFF_FFFF) + " OTZ: " + OTZ.ToString("x4"));
            //Console.ReadLine();
        }

        private void AVSZ4() {
            //MAC0 = ZSF4 * (SZ0 + SZ1 + SZ2 + SZ3);for AVSZ4
            //OTZ = MAC0 / 1000h;for both(saturated to 0..FFFFh)
            long avsz4 = (long)ZSF4 * (SZ[0] + SZ[1] + SZ[2] + SZ[3]);
            MAC0 = (int)avsz4;

            long div = avsz4 / 0x1000;
            if (div < 0) {
                div = 0;
            } else if (div > 0xFFFF) {
                div = 0xFFFF;
            }
            OTZ = (ushort)div;
        }

        private void NCDS(int r) { //Normal color depth cue (single vector)
            //In: V0 = Normal vector(for triple variants repeated with V1 and V2),
            //BK = Background color, RGBC = Primary color / code, LLM = Light matrix, LCM = Color matrix, IR0 = Interpolation value.
            // [IR1, IR2, IR3] = [MAC1, MAC2, MAC3] = (LLM * V0) SAR(sf * 12)
            // [IR1, IR2, IR3] = [MAC1, MAC2, MAC3] = (BK * 1000h + LCM * IR) SAR(sf * 12)
            // [MAC1, MAC2, MAC3] = [R * IR1, G * IR2, B * IR3] SHL 4;< --- for NCDx / NCCx
            // [MAC1, MAC2, MAC3] = MAC + (FC - MAC) * IR0;< --- for NCDx only
            // [MAC1, MAC2, MAC3] = [MAC1, MAC2, MAC3] SAR(sf * 12);< --- for NCDx / NCCx
            // Color FIFO = [MAC1 / 16, MAC2 / 16, MAC3 / 16, CODE], [IR1, IR2, IR3] = [MAC1, MAC2, MAC3]
            MAC1 = (LM.v1.X * V[r].X + LM.v1.Y * V[r].Y + LM.v1.Z * V[r].Z) >> sf * 12;
            MAC2 = (LM.v2.X * V[r].X + LM.v2.Y * V[r].Y + LM.v2.Z * V[r].Z) >> sf * 12;
            MAC3 = (LM.v3.X * V[r].X + LM.v3.Y * V[r].Y + LM.v3.Z * V[r].Z) >> sf * 12;

            IR[1] = (short)MAC1;
            IR[2] = (short)MAC2;
            IR[3] = (short)MAC3;

            //MAC1 = RBK + LRGB.v1.X 



            RGB[0].set((uint)(MAC1 / 16));
            RGB[1].set((uint)(MAC2 / 16));
            RGB[2].set((uint)(MAC3 / 16));


        }

        int nclipTest;
        private void NCLIP() { //Normal clipping
            // MAC0 =   SX0*SY1 + SX1*SY2 + SX2*SY0 - SX0*SY2 - SX1*SY0 - SX2*SY1
            long nclip = (long)(SXY[0].x * SXY[1].y + SXY[1].x * SXY[2].y + SXY[2].x * SXY[0].y - SXY[0].x * SXY[2].y - SXY[1].x * SXY[0].y - SXY[2].x * SXY[1].y);
            MAC0 = (int)nclip;

            Console.WriteLine(nclipTest++ + " " + nclip.ToString("x16"));  //OJO VALIDO !
            if ((nclip & 0xFFFF_FFFF) <= 0x7FFF_FFFF && nclip < 0) {
                FLAG |= (1 << 15);
                //Console.WriteLine("Under VALUE");
                //Console.WriteLine(FLAG.ToString("x8"));
                //Console.ReadLine();
            } else if (nclip > 0x7FFF_FFFF && nclip < 0xFFFF_FFFF) {
                FLAG |= (1 << 16);
                //Console.WriteLine("Over VALUE");
                //Console.WriteLine(FLAG.ToString("x8"));
                //Console.ReadLine();
            }
        }

        private void RTPT() { //Perspective Transformation Triple
            RTPS(0);
            RTPS(1);
            RTPS(2);
            //debug();
        }

        private void RTPS(int r) {
            //IR1 = MAC1 = (TRX*1000h + RT11*VX0 + RT12*VY0 + RT13*VZ0) SAR (sf*12)
            //IR2 = MAC2 = (TRY*1000h + RT21*VX0 + RT22*VY0 + RT23*VZ0) SAR (sf*12)
            //IR3 = MAC3 = (TRZ*1000h + RT31*VX0 + RT32*VY0 + RT33*VZ0) SAR (sf*12)
            //SZ3 = MAC3 SAR ((1-sf)*12)                           ;ScreenZ FIFO 0..+FFFFh
            //MAC0=(((H*20000h/SZ3)+1)/2)*IR1+OFX, SX2=MAC0/10000h ;ScrX FIFO -400h..+3FFh
            //MAC0=(((H*20000h/SZ3)+1)/2)*IR2+OFY, SY2=MAC0/10000h ;ScrY FIFO -400h..+3FFh
            //MAC0=(((H*20000h/SZ3)+1)/2)*DQA+DQB, IR0=MAC0/1000h  ;Depth cueing 0..+1000h

            MAC1 = (TRX * 0x1000 + RT.v1.X * V[r].X + RT.v1.Y * V[r].Y + RT.v1.Z * V[r].Z) >> (sf * 12);
            IR[1] = (short)MAC1;
            MAC2 = (TRY * 0x1000 + RT.v2.X * V[r].X + RT.v2.Y * V[r].Y + RT.v2.Z * V[r].Z) >> (sf * 12);
            IR[2] = (short)MAC2;
            MAC3 = (TRZ * 0x1000 + RT.v3.X * V[r].X + RT.v3.Y * V[r].Y + RT.v3.Z * V[r].Z) >> (sf * 12);
            IR[3] = (short)MAC3;

            SZ[0] = SZ[1];
            SZ[1] = SZ[2];
            SZ[2] = SZ[3];
            int mac3 = MAC3 >> (1 - (sf * 12));
            SZ[3] = (ushort)mac3;

            SXY[0] = SXY[1];
            SXY[1] = SXY[2];

            int result;
            int div = 0;
            if (SZ[3] == 0) {
                result = 0x1FFFF;
            } else {
                div = (int)(((H * 0x20000 / SZ[3]) + 1) / 2);

                if (div > 0x1FFFF) {
                    result = 0x1FFFF;
                    FLAG |= 0x1 << 17;
                } else {
                    result = div;
                }
            }

            MAC0 = 0; //(int)(result * IR[1] + OFX);
            SXY[2].x = 0;//(short)(MAC0 / 0x10000);
            MAC0 = 0;//(int)(result * IR[2] + OFY);
            SXY[2].y = 0;//(short)(MAC0 / 0x10000);
            MAC0 = 0;//(int)(result * DQA + OFX);
            IR[0] = 0;//(short)(MAC0 / 0x1000);
        }

        internal uint loadData(uint fs) {
            uint value;
            switch (fs) {
                case 00: value = V[0].XY; break;
                case 01: value = V[0].OZ; break;
                case 02: value = V[1].XY; break;
                case 03: value = V[1].OZ; break;
                case 04: value = V[2].XY; break;
                case 05: value = V[2].OZ; break;
                case 06: value = RGBC.get(); break;
                case 07: value = OTZ; break;
                case 08: value = (uint)IR[0]; break;
                case 09: value = (uint)IR[1]; break;
                case 10: value = (uint)IR[2]; break;
                case 11: value = (uint)IR[3]; break;
                case 12: value = SXY[0].get(); break;
                case 13: value = SXY[1].get(); break;
                case 14: //Mirror
                case 15: value = SXY[2].get(); break;
                case 16: value = SZ[0]; break;
                case 17: value = SZ[1]; break;
                case 18: value = SZ[2]; break;
                case 19: value = SZ[3]; break;
                case 20: value = RGB[0].get(); break;
                case 21: value = RGB[1].get(); break;
                case 22: value = RGB[2].get(); break;
                case 23: value = RES1; break; //Prohibited Register
                case 24: value = (uint)MAC0; break;
                case 25: value = (uint)MAC1; break;
                case 26: value = (uint)MAC2; break;
                case 27: value = (uint)MAC3; break;
                case 28:/* value = IRGB; break;*/
                case 29:/* value = ORGB; break;*/
                    IRGB = (ushort)(saturateRGB(IR[3] / 0x80) << 10 | saturateRGB(IR[2] / 0x80) << 5 | saturateRGB(IR[1] / 0x80));
                    value = IRGB;
                    break;
                case 30: value = (uint)LZCS; break;
                case 31: value = (uint)LZCR; break;
                default: value = 0xFFFF_FFFF; break;
            }
            //Console.WriteLine("GTE Load Data R" + fs + ": " + value.ToString("x8"));
            //Console.WriteLine(value.ToString("x8"));
            return value;
        }

        private short saturateRGB(int v) {
            short saturate = (short)v;
            if (saturate < 0x00) return 0x00;
            else if (saturate > 0x1F) return 0x1F;
            else return saturate;
        }

        private int test;
        internal void writeData(uint fs, uint v) {
            //Console.WriteLine("GTE Write Data R" + fs + ": " + v.ToString("x8"));
            //Console.WriteLine(v.ToString("x8"));
            //Console.ReadLine();
            switch (fs) {
                case 00: V[0].XY = v; break;
                case 01: V[0].OZ = v; break;
                case 02: V[1].XY = v; break;
                case 03: V[1].OZ = v; break;
                case 04: V[2].XY = v; break;
                case 05: V[2].OZ = v; break;
                case 06: RGBC.set(v); break;
                case 07: OTZ = (ushort)v; break;
                case 08: IR[0] = (short)v; break;
                case 09: IR[1] = (short)v; break;
                case 10: IR[2] = (short)v; break;
                case 11: IR[3] = (short)v; break;
                case 12: SXY[0].set(v); break;
                case 13: SXY[1].set(v); break;
                case 14: SXY[2].set(v); break;
                case 15: SXY[0] = SXY[1]; SXY[1] = SXY[2]; SXY[2].set(v); break; //On load mirrors 0x14 on write cycles the fifo
                case 16: SZ[0] = (ushort)v; break;
                case 17: SZ[1] = (ushort)v; break;
                case 18: SZ[2] = (ushort)v; break;
                case 19: SZ[3] = (ushort)v; break;
                case 20: RGB[0].set(v); break;
                case 21: RGB[1].set(v); break;
                case 22: RGB[2].set(v); break;
                case 23: RES1 = v; break;
                case 24: MAC0 = (int)v; break;
                case 25: MAC1 = (int)v; break;
                case 26: MAC2 = (int)v; break;
                case 27: MAC3 = (int)v; break;
                case 28:
                    IRGB = (ushort)(v & 0x7FFF);
                    IR[1] = (short)((v & 0x1F) * 0x80);
                    IR[2] = (short)(((v >> 5) & 0x1F) * 0x80);
                    IR[3] = (short)(((v >> 10) & 0x1F) * 0x80);
                    break;
                case 29: /*ORGB = (ushort)v;*/ break; //Only Read its set by IRGB
                case 30: LZCS = (int)v; LZCR = leadingCount(v); break;
                case 31: /*LZCR = (int)v;*/ break; //Only Read its set by LZCS
            }
        }

        private int leadingCount(uint v) {
            int sign = (int)((v & 0x80000000) >> 31);
            int leadingCount = 0;
            int n = (int)v;
            for (int i = 0; i < 32; i++) {
                if ((n & 0x80000000) >> 31 != sign) break;
                leadingCount++;
                n <<= 1;
            }
            return leadingCount;
        }

        internal uint loadControl(uint fs) {
            uint value;
            switch (fs) {
                case 00: value = RT.v1.XY; break;
                case 01: value = (uint)(ushort)RT.v1.Z | (uint)(RT.v2.X << 16); break;
                case 02: value = (uint)(ushort)RT.v2.Y | (uint)(RT.v2.Z << 16); break;
                case 03: value = RT.v3.XY; break;
                case 04: value = RT.v3.OZ; break;
                case 05: value = (uint)TRX; break;
                case 06: value = (uint)TRY; break;
                case 07: value = (uint)TRZ; break;
                case 08: value = LM.v1.XY; break;
                case 09: value = (uint)(ushort)LM.v1.Z | (uint)(LM.v2.X << 16); break;
                case 10: value = (uint)(ushort)LM.v2.Y | (uint)(LM.v2.Z << 16); break;
                case 11: value = LM.v3.XY; break;
                case 12: value = LM.v3.OZ; break;
                case 13: value = (uint)RBK; break;
                case 14: value = (uint)GBK; break;
                case 15: value = (uint)BBK; break;
                case 16: value = LRGB.v1.XY; break;
                case 17: value = (uint)(ushort)LRGB.v1.Z | (uint)(LRGB.v2.X << 16); break;
                case 18: value = (uint)(ushort)LRGB.v2.Y | (uint)(LRGB.v2.Z << 16); break;
                case 19: value = LRGB.v3.XY; break;
                case 20: value = LRGB.v3.OZ; break;
                case 21: value = (uint)RFC; break;
                case 22: value = (uint)GFC; break;
                case 23: value = (uint)BFC; break;
                case 24: value = (uint)OFX; break;
                case 25: value = (uint)OFY; break;
                case 26: value = (uint)(short)H; break; //sign extend
                case 27: value = (uint)DQA; break;
                case 28: value = (uint)DQB; break;
                case 29: value = (uint)ZSF3; break; //sign extend
                case 30: value = (uint)ZSF4; break; //sign extend
                case 31: value = FLAG; break;
                default: value = 0xFFFF_FFFF; break;
            }
            //Console.WriteLine("GTE Load Cont R" + fs + ": " + value.ToString("x8"));
            //Console.WriteLine(value.ToString("x8"));
            return value;
        }

        internal void writeControl(uint fs, uint v) {
            //Console.ForegroundColor = ConsoleColor.Red;
            //Console.WriteLine(" >>>>>>>>>>>>>>>>>>>>       [TEST]" + ++test + "           <<<<<<<<<<<<<<<<<<<<<<<");
            //Console.ResetColor();
            //Console.WriteLine("GTE Write Control R" + fs + ": " + v.ToString("x8"));
            //Console.WriteLine(v.ToString("x8"));
            switch (fs) {
                case 00: RT.v1.XY = v; break;
                case 01: RT.v1.Z = (short)v; RT.v2.X = (short)(v >> 16); break;
                case 02: RT.v2.Y = (short)v; RT.v2.Z = (short)(v >> 16); break;
                case 03: RT.v3.XY = v; break;
                case 04: RT.v3.OZ = v; break;
                case 05: TRX = (int)v; break;
                case 06: TRY = (int)v; break;
                case 07: TRZ = (int)v; break;
                case 08: LM.v1.XY = v; break;
                case 09: LM.v1.Z = (short)v; LM.v2.X = (short)(v >> 16); break;
                case 10: LM.v2.Y = (short)v; LM.v2.Z = (short)(v >> 16); break;
                case 11: LM.v3.XY = v; break;
                case 12: LM.v3.OZ = v; break;
                case 13: RBK = (int)v; break;
                case 14: GBK = (int)v; break;
                case 15: BBK = (int)v; break;
                case 16: LRGB.v1.XY = v; break;
                case 17: LRGB.v1.Z = (short)v; LRGB.v2.X = (short)(v >> 16); break;
                case 18: LRGB.v2.Y = (short)v; LRGB.v2.Z = (short)(v >> 16); break;
                case 19: LRGB.v3.XY = v; break;
                case 20: LRGB.v3.OZ = v; break;
                case 21: RFC = (int)v; break;
                case 22: GFC = (int)v; break;
                case 23: BFC = (int)v; break;
                case 24: OFX = (int)v; break;
                case 25: OFY = (int)v; break;
                case 26: H = (ushort)v; break;
                case 27: DQA = (short)v; break;
                case 28: DQB = (int)v; break;
                case 29: ZSF3 = (short)v; break;
                case 30: ZSF4 = (short)v; break;
                case 31: //flag is u20 with 31 Error Flag (Bit30..23, and 18..13 ORed together)
                    FLAG = v & 0x7FFF_F000;
                    if ((FLAG & 0x7F87_E000) != 0) {
                        FLAG |= 0x8000_0000;
                    }
                    break;
            }
        }

        private void debug() {
            string gteDebug = $"GTE CONTROL\n";
            for (uint i = 0; i < 32; i++) {
                string register = i <= 9 ? "0" + i : i.ToString();
                gteDebug += " " + register + ": " + loadControl(i).ToString("x8");
                if ((i + 1) % 4 == 0) gteDebug += "\n";
            }

            gteDebug += $"GTE DATA\n";
            for (uint i = 0; i < 32; i++) {
                string register = i <= 9 ? "0" + i : i.ToString();
                gteDebug += " " + register + ": " + loadData(i).ToString("x8");
                if ((i + 1) % 4 == 0) gteDebug += "\n";
            }

            Console.WriteLine(gteDebug);
            Console.ReadLine();
        }
    }
}
