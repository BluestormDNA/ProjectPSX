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
            public ushort X {
                get { return (ushort)x; }
                set { x = (short)value; }
            }
            public ushort Y {
                get { return (ushort)y; }
                set { y = (short)value; }
            }
            public ushort Z {
                get { return (ushort)z; }
                set { z = (short)value; }
            }

            public uint XY {
                get { return (uint)((ushort)x << 16 | (ushort)y); }
                set { x = (short)(value >> 16); y = (short)value; }
            }
            public uint OZ {
                get { return (uint)z; }
                set { z = (short)value; }
            }

            private short x;
            private short y;
            private short z;
        }

        struct Vector2 {
            public short x;
            public short y;

            public uint get() {
                return (uint)((ushort)x << 16 | (ushort)y);
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
                return (uint)(c << 24 | b << 16 | b << 8 | r);
            }
            public void set(uint value) {
                byte r = (byte)(value & 0x0000_00FF);
                byte g = (byte)((value & 0x0000_FF00) >> 8);
                byte b = (byte)((value & 0x00FF_0000) >> 16);
                byte c = (byte)((value & 0xFF00_0000) >> 24);
            }// more
        }

        //Data Registers
        Vector3[] V = new Vector3[3];             //R0-1 R2-3 R4-5 s16
        Color RGBC;                     //R6
        ushort OTZ;                     //R7
        short[] IR = new short[4];      //R8-11
        Vector2[] SXY = new Vector2[4]; //R12-15 FIFO
        Vector2[] SZ = new Vector2[4];  //R16-19 FIFO
        Color[] RGB = new Color[3];     //R20-22 FIFO
                                        //R23 prohibited
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

        internal void execute(uint imm) {
            Console.WriteLine("GTE EXECUTE" + (imm & 0x3F).ToString("x8"));

            decodeCommand(imm);
            FLAG = 0;

            switch (opcode) {
                case 0x06: NCLIP(); break;
                case 0x13: NCDS(); break;
                case 0x2D: AVSZ3(); break;
                case 0x30: RTPT(); break;
                default: Console.Write("UNIMPLEMENTED GTE COMMAND"); throw new NotImplementedException();
            }
        }

        private void decodeCommand(uint imm) {
            sf = (int)(imm & 0x1 << 19);
            MVMVA_M_Matrix = imm >> 17 & 0x3;
            MVMVA_M_Vector = imm >> 15 & 0x3;
            MVMVA_T_Vector = imm >> 13 & 0x3;
            lm = (imm >> 10 & 0x1) != 0;
            opcode = imm & 0x3F;
        }

        private void AVSZ3() {
            //MAC0 = ZSF3 * (SZ1 + SZ2 + SZ3); for AVSZ3
            //MAC0 = ZSF4 * (SZ0 + SZ1 + SZ2 + SZ3);for AVSZ4
            //OTZ = MAC0 / 1000h;for both(saturated to 0..FFFFh)
            MAC0 = ZSF3 * (int)(SZ[1].get() + SZ[2].get() + SZ[3].get());
            OTZ = (ushort)(MAC0 / 0x1000);
        }

        private void NCDS() { //Normal color depth cue (single vector)
            //In: V0 = Normal vector(for triple variants repeated with V1 and V2),
            //BK = Background color, RGBC = Primary color / code, LLM = Light matrix, LCM = Color matrix, IR0 = Interpolation value.
            // [IR1, IR2, IR3] = [MAC1, MAC2, MAC3] = (LLM * V0) SAR(sf * 12)
            // [IR1, IR2, IR3] = [MAC1, MAC2, MAC3] = (BK * 1000h + LCM * IR) SAR(sf * 12)
            // [MAC1, MAC2, MAC3] = [R * IR1, G * IR2, B * IR3] SHL 4;< --- for NCDx / NCCx
            // [MAC1, MAC2, MAC3] = MAC + (FC - MAC) * IR0;< --- for NCDx only
            // [MAC1, MAC2, MAC3] = [MAC1, MAC2, MAC3] SAR(sf * 12);< --- for NCDx / NCCx
            // Color FIFO = [MAC1 / 16, MAC2 / 16, MAC3 / 16, CODE], [IR1, IR2, IR3] = [MAC1, MAC2, MAC3]
            RGB[0].set(0x66666666);
            RGB[1].set(0x33005544);
            RGB[2].set(0x00117225);
        }

        private void NCLIP() { //Normal clipping
            // MAC0 =   SX0*SY1 + SX1*SY2 + SX2*SY0 - SX0*SY2 - SX1*SY0 - SX2*SY1
            MAC0 = SXY[0].x * SXY[1].y + SXY[1].x * SXY[2].y + SXY[2].x * SXY[0].y - SXY[0].x * SXY[2].y - SXY[1].x * SXY[0].y - SXY[2].x * SXY[1].y;
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
            SZ[3].set((uint)mac3);

            SXY[0] = SXY[1];
            SXY[1] = SXY[2];

            int result;
            int div = 0;
            if (SZ[3].get() == 0) {
                result = 0x1FFFF;
            } else {
                div = (int)(((H * 0x20000 / SZ[3].get()) + 1) / 2);

                if (div > 0x1FFFF) {
                    result = 0x1FFFF;
                    FLAG |= 0x1 << 17;
                } else {
                    result = div;
                }
            }

            MAC0 = (int)(result * IR[1] + OFX);
            SXY[2].x = (short)(MAC0 / 0x10000);
            MAC0 = (int)(result * IR[2] + OFY);
            SXY[2].y = (short)(MAC0 / 0x10000);
            MAC0 = (int)(result * DQA + OFX);
            IR[0] = (short)(MAC0 / 0x1000);
        }

        internal uint loadData(uint fs) {
            switch (fs) {
                case 00: return V[0].XY;
                case 01: return V[0].OZ;
                case 02: return V[1].XY;
                case 03: return V[1].OZ;
                case 04: return V[2].XY;
                case 05: return V[2].OZ;
                case 06: return RGBC.get();
                case 07: return OTZ;
                case 08: return (uint)IR[0];
                case 09: return (uint)IR[1];
                case 10: return (uint)IR[2];
                case 11: return (uint)IR[3];
                case 12: return SXY[0].get();
                case 13: return SXY[1].get();
                case 14: return SXY[2].get();
                case 15: return SXY[2].get(); //Mirror
                case 16: return SZ[0].get();
                case 17: return SZ[1].get();
                case 18: return SZ[2].get();
                case 19: return SZ[3].get();
                case 20: return RGB[0].get();
                case 21: return RGB[1].get();
                case 22: return RGB[2].get();
                case 23: return 0; //Prohibited Register
                case 24: return (uint)MAC0;
                case 25: return (uint)MAC1;
                case 26: return (uint)MAC2;
                case 27: return (uint)MAC3;
                case 28: return IRGB;
                case 29: return ORGB;
                case 30: return (uint)LZCS;
                case 31: return (uint)LZCR;
                default: return 0xFFFF_FFFF;
            }
        }

        internal void writeData(uint fs, uint v) {
            Console.WriteLine("GTE Write Data R" + fs + ": " + v.ToString("x8"));
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
                case 15: SXY[0] = SXY[1]; SXY[1] = SXY[2]; break; //On load mirrors 0x14 on write cycles the fifo
                case 16: SZ[0].set(v); break;
                case 17: SZ[1].set(v); break;
                case 18: SZ[2].set(v); break;
                case 19: SZ[3].set(v); break;
                case 20: RGB[0].set(v); break;
                case 21: RGB[1].set(v); break;
                case 22: RGB[2].set(v); break;
                case 23: break; //Prohibited Register
                case 24: MAC0 = (int)v; break;
                case 25: MAC1 = (int)v; break;
                case 26: MAC2 = (int)v; break;
                case 27: MAC3 = (int)v; break;
                case 28: IRGB = (ushort)v; break;
                case 29: ORGB = (ushort)v; break;
                case 30: LZCS = (int)v; break;
                case 31: LZCR = (int)v; break;
            }
        }

        internal uint loadControl(uint fs) {
            switch (fs) {
                case 00: return RT.v1.XY;
                case 01: return (uint)(RT.v1.Z | RT.v2.X << 16);
                case 02: return (uint)(RT.v2.Y | RT.v2.Z << 16);
                case 03: return RT.v3.XY;
                case 04: return RT.v3.OZ;
                case 05: return (uint)TRX;
                case 06: return (uint)TRY;
                case 07: return (uint)TRZ;
                case 08: return LM.v1.XY;
                case 09: return (uint)(LM.v1.Z | LM.v2.X << 16);
                case 10: return (uint)(LM.v2.Y | LM.v2.Z << 16);
                case 11: return LM.v3.XY;
                case 12: return LM.v3.OZ;
                case 13: return (uint)RBK;
                case 14: return (uint)GBK;
                case 15: return (uint)BBK;
                case 16: return LRGB.v1.XY;
                case 17: return (uint)(LRGB.v1.Z | LRGB.v2.X << 16);
                case 18: return (uint)(LRGB.v2.Y | LRGB.v2.Z << 16);
                case 19: return LRGB.v3.XY;
                case 20: return LRGB.v3.OZ;
                case 21: return (uint)RFC;
                case 22: return (uint)GFC;
                case 23: return (uint)BFC;
                case 24: return (uint)OFX;
                case 25: return (uint)OFY;
                case 26: return H;
                case 27: return (uint)(short)DQA;
                case 28: return (uint)DQB;
                case 29: return (uint)(short)ZSF3;
                case 30: return (uint)(short)ZSF4;
                case 31: return FLAG;
                default: return 0xFFFF_FFFF;
            }
        }

        internal void writeControl(uint fs, uint v) {
            switch (fs) {
                case 00: RT.v1.XY = v; break;
                case 01: RT.v1.Z = (ushort)v; RT.v2.X = (ushort)(v >> 16); break;
                case 02: RT.v2.Y = (ushort)v; RT.v2.Z = (ushort)(v >> 16); break;
                case 03: RT.v3.XY = v; break;
                case 04: RT.v3.OZ = v; break;
                case 05: TRX = (int)v; break;
                case 06: TRY = (int)v; break;
                case 07: TRZ = (int)v; break;
                case 08: LM.v1.XY = v; break;
                case 09: LM.v1.Z = (ushort)v; RT.v2.X = (ushort)(v >> 16); break;
                case 10: LM.v2.Y = (ushort)v; RT.v2.Z = (ushort)(v >> 16); break;
                case 11: LM.v3.XY = v; break;
                case 12: LM.v3.OZ = v; break;
                case 13: RBK = (int)v; break;
                case 14: GBK = (int)v; break;
                case 15: BBK = (int)v; break;
                case 16: LRGB.v1.XY = v; break;
                case 17: LRGB.v1.Z = (ushort)v; LRGB.v2.X = (ushort)(v >> 16); break;
                case 18: LRGB.v2.Y = (ushort)v; LRGB.v2.Z = (ushort)(v >> 16); break;
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
                case 31: FLAG = v; break;
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
