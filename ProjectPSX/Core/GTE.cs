using System;
using System.Runtime.InteropServices;

namespace ProjectPSX {
    class GTE { //PSX MIPS Coprocessor 02 - Geometry Transformation Engine

        private struct Matrix {
            public Vector3 v1;
            public Vector3 v2;
            public Vector3 v3;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct Vector3 {
            [FieldOffset(0)] public uint XY;
            [FieldOffset(0)] public short x;
            [FieldOffset(2)] public short y;
            [FieldOffset(4)] public short z;
        }


        [StructLayout(LayoutKind.Explicit)]
        private struct Vector2 {
            [FieldOffset(0)] public uint val;
            [FieldOffset(0)] public short x;
            [FieldOffset(2)] public short y;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct Color {
            [FieldOffset(0)] public uint val;
            [FieldOffset(0)] public byte r;
            [FieldOffset(1)] public byte g;
            [FieldOffset(2)] public byte b;
            [FieldOffset(3)] public byte c;
        }

        //Data Registers
        private Vector3[] V = new Vector3[3];   //R0-1 R2-3 R4-5 s16
        private Color RGBC;                     //R6
        private ushort OTZ;                     //R7
        private short[] IR = new short[4];      //R8-11
        private Vector2[] SXY = new Vector2[4]; //R12-15 FIFO
        private ushort[] SZ = new ushort[4];    //R16-19 FIFO
        private Color[] RGB = new Color[3];     //R20-22 FIFO
        private uint RES1;                      //R23 prohibited
        private int MAC0;                       //R24
        private int MAC1, MAC2, MAC3;           //R25-27
        private ushort IRGB;//, ORGB;           //R28-29 Orgb is readonly and read by irgb
        private int LZCS, LZCR;                 //R30-31

        //Control Registers
        private Matrix RT, LM, LRGB;        //R32-36 R40-44 R48-52
        private int TRX, TRY, TRZ;          //R37-39
        private int RBK, GBK, BBK;          //R45-47
        private int RFC, GFC, BFC;          //R53-55
        private int OFX, OFY, DQB;          //R56 57 60
        private ushort H;                   //R58
        private short ZSF3, ZSF4, DQA;      //R61 62 59
        private uint FLAG;                  //R63

        //Command decode
        private int sf;                     //Shift fraction (0 or 12)
        private uint MVMVA_M_Matrix;         //MVMVA Multiply Matrix    (0=Rotation. 1=Light, 2=Color, 3=Reserved)
        private uint MVMVA_M_Vector;         //MVMVA Multiply Vector    (0=V0, 1=V1, 2=V2, 3=IR/long)
        private uint MVMVA_T_Vector;         //MVMVA Translation Vector (0=TR, 1=BK, 2=FC/Bugged, 3=None)
        private bool lm;                     //Saturate IR1,IR2,IR3 result (0=To -8000h..+7FFFh, 1=To 0..+7FFFh)
        private uint opcode;                 //GTE opcode
        private CPU cpu;
        public GTE(CPU cpu) {//this is only needed for temporary debug purposes till TGTE passes
            this.cpu = cpu;
        }

        private void decodeCommand(uint command) {
            sf = (int)(command & 0x80_000) >> 19;
            MVMVA_M_Matrix = (command >> 17) & 0x3;
            MVMVA_M_Vector = (command >> 15) & 0x3;
            MVMVA_T_Vector = (command >> 13) & 0x3;
            lm = ((command >> 10) & 0x1) != 0;
            opcode = command & 0x3F;
        }
        internal void execute(uint command) {
            //Console.WriteLine($"GTE EXECUTE {(command & 0x3F):x2}");

            decodeCommand(command);
            FLAG = 0;

            switch (opcode) {
                case 0x01: RTPS(0); break;
                case 0x06: NCLIP(); break;
                case 0x0C: OP(); break;
                case 0x10: DPCS(false); break;
                case 0x11: INTPL(); break;
                case 0x12: MVMVA(); break;
                case 0x13: NCDS(0); break;
                case 0x14: CDP(); break;
                case 0x16: NCDT(); break;
                case 0x1B: NCCS(0); break;
                case 0x1C: CC(); break;
                case 0x1E: NCS(0); break;
                case 0x20: NCT(); break;
                case 0x28: SQR(); break;
                case 0x29: DCPL(); break;
                case 0x2A: DCPT(); break;
                case 0x2D: AVSZ3(); break;
                case 0x2E: AVSZ4(); break;
                case 0x30: RTPT(); break;
                case 0x3D: GPF(); break;
                case 0x3E: GPL(); break;
                case 0x3F: NCCT(); break;
                default: Console.WriteLine($"UNIMPLEMENTED GTE COMMAND {opcode:x2}"); break;/* throw new NotImplementedException();*/
            }

            if ((FLAG & 0x7F87_E000) != 0) {
                FLAG |= 0x8000_0000;
            }
        }

        private void CDP() {
            // [IR1, IR2, IR3] = [MAC1, MAC2, MAC3] = (BK * 1000h + LCM * IR) SAR(sf * 12)
            // WARNING each multiplication can trigger mac flags so the check is needed on each op! Somehow this only affects the color matrix and not the light one
            MAC1 = (int)(setMAC(1, setMAC(1, setMAC(1, (long)RBK * 0x1000 + LRGB.v1.x * IR[1]) + (long)LRGB.v1.y * IR[2]) + (long)LRGB.v1.z * IR[3]) >> sf * 12);
            MAC2 = (int)(setMAC(2, setMAC(2, setMAC(2, (long)GBK * 0x1000 + LRGB.v2.x * IR[1]) + (long)LRGB.v2.y * IR[2]) + (long)LRGB.v2.z * IR[3]) >> sf * 12);
            MAC3 = (int)(setMAC(3, setMAC(3, setMAC(3, (long)BBK * 0x1000 + LRGB.v3.x * IR[1]) + (long)LRGB.v3.y * IR[2]) + (long)LRGB.v3.z * IR[3]) >> sf * 12);

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);

            // [MAC1, MAC2, MAC3] = [R * IR1, G * IR2, B * IR3] SHL 4;
            MAC1 = (int)(setMAC(1, (long)RGBC.r * IR[1]) << 4);
            MAC2 = (int)(setMAC(2, (long)RGBC.g * IR[2]) << 4);
            MAC3 = (int)(setMAC(3, (long)RGBC.b * IR[3]) << 4);

            interpolateColor(MAC1, MAC2, MAC3);

            // Color FIFO = [MAC1 / 16, MAC2 / 16, MAC3 / 16, CODE]
            RGB[0] = RGB[1];
            RGB[1] = RGB[2];

            RGB[2].r = setRGB(1, MAC1 >> 4);
            RGB[2].g = setRGB(2, MAC2 >> 4);
            RGB[2].b = setRGB(3, MAC3 >> 4);
            RGB[2].c = RGBC.c;
        }

        private void CC() {
            // [IR1, IR2, IR3] = [MAC1, MAC2, MAC3] = (BK * 1000h + LCM * IR) SAR(sf * 12)
            // WARNING each multiplication can trigger mac flags so the check is needed on each op! Somehow this only affects the color matrix and not the light one
            MAC1 = (int)(setMAC(1, setMAC(1, setMAC(1, (long)RBK * 0x1000 + LRGB.v1.x * IR[1]) + (long)LRGB.v1.y * IR[2]) + (long)LRGB.v1.z * IR[3]) >> sf * 12);
            MAC2 = (int)(setMAC(2, setMAC(2, setMAC(2, (long)GBK * 0x1000 + LRGB.v2.x * IR[1]) + (long)LRGB.v2.y * IR[2]) + (long)LRGB.v2.z * IR[3]) >> sf * 12);
            MAC3 = (int)(setMAC(3, setMAC(3, setMAC(3, (long)BBK * 0x1000 + LRGB.v3.x * IR[1]) + (long)LRGB.v3.y * IR[2]) + (long)LRGB.v3.z * IR[3]) >> sf * 12);

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);

            // [MAC1, MAC2, MAC3] = [R * IR1, G * IR2, B * IR3] SHL 4;
            MAC1 = (int)(setMAC(1, (long)RGBC.r * IR[1]) << 4);
            MAC2 = (int)(setMAC(2, (long)RGBC.g * IR[2]) << 4);
            MAC3 = (int)(setMAC(3, (long)RGBC.b * IR[3]) << 4);

            // [MAC1, MAC2, MAC3] = [MAC1, MAC2, MAC3] SAR(sf * 12);< --- for NCDx / NCCx
            MAC1 = (int)(setMAC(1, MAC1) >> sf * 12);
            MAC2 = (int)(setMAC(2, MAC2) >> sf * 12);
            MAC3 = (int)(setMAC(3, MAC3) >> sf * 12);

            // Color FIFO = [MAC1 / 16, MAC2 / 16, MAC3 / 16, CODE], [IR1, IR2, IR3] = [MAC1, MAC2, MAC3]
            RGB[0] = RGB[1];
            RGB[1] = RGB[2];

            RGB[2].r = setRGB(1, MAC1 >> 4);
            RGB[2].g = setRGB(2, MAC2 >> 4);
            RGB[2].b = setRGB(3, MAC3 >> 4);
            RGB[2].c = RGBC.c;

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);
        }

        private void DCPT() {
            DPCS(true);
            DPCS(true);
            DPCS(true);
        }

        private void DCPL() {
            //[MAC1, MAC2, MAC3] = [R*IR1, G*IR2, B*IR3] SHL 4          ;<--- for DCPL only
            MAC1 = (int)(setMAC(1, RGBC.r * IR[1]) << 4);
            MAC2 = (int)(setMAC(2, RGBC.g * IR[2]) << 4);
            MAC3 = (int)(setMAC(3, RGBC.b * IR[3]) << 4);

            interpolateColor(MAC1, MAC2, MAC3);

            // Color FIFO = [MAC1 / 16, MAC2 / 16, MAC3 / 16, CODE]
            RGB[0] = RGB[1];
            RGB[1] = RGB[2];

            RGB[2].r = setRGB(1, MAC1 >> 4);
            RGB[2].g = setRGB(2, MAC2 >> 4);
            RGB[2].b = setRGB(3, MAC3 >> 4);
            RGB[2].c = RGBC.c;
        }

        private void NCCS(int r) {
            // [IR1, IR2, IR3] = [MAC1, MAC2, MAC3] = (LLM * V0) SAR(sf * 12)
            MAC1 = (int)(setMAC(1, (long)LM.v1.x * V[r].x + LM.v1.y * V[r].y + LM.v1.z * V[r].z) >> sf * 12);
            MAC2 = (int)(setMAC(2, (long)LM.v2.x * V[r].x + LM.v2.y * V[r].y + LM.v2.z * V[r].z) >> sf * 12);
            MAC3 = (int)(setMAC(3, (long)LM.v3.x * V[r].x + LM.v3.y * V[r].y + LM.v3.z * V[r].z) >> sf * 12);

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);

            // [IR1, IR2, IR3] = [MAC1, MAC2, MAC3] = (BK * 1000h + LCM * IR) SAR(sf * 12)
            // WARNING each multiplication can trigger mac flags so the check is needed on each op! Somehow this only affects the color matrix and not the light one
            MAC1 = (int)(setMAC(1, setMAC(1, setMAC(1, (long)RBK * 0x1000 + LRGB.v1.x * IR[1]) + (long)LRGB.v1.y * IR[2]) + (long)LRGB.v1.z * IR[3]) >> sf * 12);
            MAC2 = (int)(setMAC(2, setMAC(2, setMAC(2, (long)GBK * 0x1000 + LRGB.v2.x * IR[1]) + (long)LRGB.v2.y * IR[2]) + (long)LRGB.v2.z * IR[3]) >> sf * 12);
            MAC3 = (int)(setMAC(3, setMAC(3, setMAC(3, (long)BBK * 0x1000 + LRGB.v3.x * IR[1]) + (long)LRGB.v3.y * IR[2]) + (long)LRGB.v3.z * IR[3]) >> sf * 12);

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);

            // [MAC1, MAC2, MAC3] = [R * IR1, G * IR2, B * IR3] SHL 4;< --- for NCDx / NCCx
            MAC1 = (int)setMAC(1, (RGBC.r * IR[1]) << 4);
            MAC2 = (int)setMAC(2, (RGBC.g * IR[2]) << 4);
            MAC3 = (int)setMAC(3, (RGBC.b * IR[3]) << 4);

            // [MAC1, MAC2, MAC3] = [MAC1, MAC2, MAC3] SAR(sf * 12);< --- for NCDx / NCCx
            MAC1 = (int)setMAC(1, MAC1 >> sf * 12);
            MAC2 = (int)setMAC(2, MAC2 >> sf * 12);
            MAC3 = (int)setMAC(3, MAC3 >> sf * 12);

            // Color FIFO = [MAC1 / 16, MAC2 / 16, MAC3 / 16, CODE], [IR1, IR2, IR3] = [MAC1, MAC2, MAC3]
            RGB[0] = RGB[1];
            RGB[1] = RGB[2];

            RGB[2].r = setRGB(1, MAC1 >> 4);
            RGB[2].g = setRGB(2, MAC2 >> 4);
            RGB[2].b = setRGB(3, MAC3 >> 4);
            RGB[2].c = RGBC.c;

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);
        }

        private void NCCT() {
            NCCS(0);
            NCCS(1);
            NCCS(2);
        }

        private void DPCS(bool dpct) {
            byte r = RGBC.r;
            byte g = RGBC.g;
            byte b = RGBC.b;

            // WHEN DCPT it uses RGB FIFO instead RGBC
            if (dpct) {
                r = RGB[0].r;
                g = RGB[0].g;
                b = RGB[0].b;
            }
            //[MAC1, MAC2, MAC3] = [R, G, B] SHL 16                     ;<--- for DPCS/DPCT
            MAC1 = (int)(setMAC(1, r) << 16);
            MAC2 = (int)(setMAC(2, g) << 16);
            MAC3 = (int)(setMAC(3, b) << 16);

            interpolateColor(MAC1, MAC2, MAC3);

            // Color FIFO = [MAC1 / 16, MAC2 / 16, MAC3 / 16, CODE]
            RGB[0] = RGB[1];
            RGB[1] = RGB[2];

            RGB[2].r = setRGB(1, MAC1 >> 4);
            RGB[2].g = setRGB(2, MAC2 >> 4);
            RGB[2].b = setRGB(3, MAC3 >> 4);
            RGB[2].c = RGBC.c;
        }

        private void INTPL() {
            // [MAC1, MAC2, MAC3] = [IR1, IR2, IR3] SHL 12               ;<--- for INTPL only
            MAC1 = (int)setMAC(1, (long)IR[1] << 12);
            MAC2 = (int)setMAC(2, (long)IR[2] << 12);
            MAC3 = (int)setMAC(3, (long)IR[3] << 12);

            interpolateColor(MAC1, MAC2, MAC3);

            // Color FIFO = [MAC1 / 16, MAC2 / 16, MAC3 / 16, CODE]
            RGB[0] = RGB[1];
            RGB[1] = RGB[2];

            RGB[2].r = setRGB(1, MAC1 >> 4);
            RGB[2].g = setRGB(2, MAC2 >> 4);
            RGB[2].b = setRGB(3, MAC3 >> 4);
            RGB[2].c = RGBC.c;
        }

        private void NCT() {
            NCS(0);
            NCS(1);
            NCS(2);
        }

        private void NCS(int r) {
            //In: V0 = Normal vector(for triple variants repeated with V1 and V2),
            //BK = Background color, RGBC = Primary color / code, LLM = Light matrix, LCM = Color matrix, IR0 = Interpolation value.

            // [IR1, IR2, IR3] = [MAC1, MAC2, MAC3] = (LLM * V0) SAR(sf * 12)
            MAC1 = (int)(setMAC(1, (long)LM.v1.x * V[r].x + LM.v1.y * V[r].y + LM.v1.z * V[r].z) >> sf * 12);
            MAC2 = (int)(setMAC(2, (long)LM.v2.x * V[r].x + LM.v2.y * V[r].y + LM.v2.z * V[r].z) >> sf * 12);
            MAC3 = (int)(setMAC(3, (long)LM.v3.x * V[r].x + LM.v3.y * V[r].y + LM.v3.z * V[r].z) >> sf * 12);

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);

            // [IR1, IR2, IR3] = [MAC1, MAC2, MAC3] = (BK * 1000h + LCM * IR) SAR(sf * 12)
            // WARNING each multiplication can trigger mac flags so the check is needed on each op! Somehow this only affects the color matrix and not the light one
            MAC1 = (int)(setMAC(1, setMAC(1, setMAC(1, (long)RBK * 0x1000 + LRGB.v1.x * IR[1]) + (long)LRGB.v1.y * IR[2]) + (long)LRGB.v1.z * IR[3]) >> sf * 12);
            MAC2 = (int)(setMAC(2, setMAC(2, setMAC(2, (long)GBK * 0x1000 + LRGB.v2.x * IR[1]) + (long)LRGB.v2.y * IR[2]) + (long)LRGB.v2.z * IR[3]) >> sf * 12);
            MAC3 = (int)(setMAC(3, setMAC(3, setMAC(3, (long)BBK * 0x1000 + LRGB.v3.x * IR[1]) + (long)LRGB.v3.y * IR[2]) + (long)LRGB.v3.z * IR[3]) >> sf * 12);

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);

            // Color FIFO = [MAC1 / 16, MAC2 / 16, MAC3 / 16, CODE], [IR1, IR2, IR3] = [MAC1, MAC2, MAC3]
            RGB[0] = RGB[1];
            RGB[1] = RGB[2];

            RGB[2].r = setRGB(1, MAC1 >> 4);
            RGB[2].g = setRGB(2, MAC2 >> 4);
            RGB[2].b = setRGB(3, MAC3 >> 4);
            RGB[2].c = RGBC.c;

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);
        }

        //int matrix;
        private void MVMVA() { //WIP
            //Console.WriteLine("[GTE] MVMVA " + ++matrix);
            //Mx = matrix specified by mx; RT / LLM / LCM - Rotation, light or color matrix
            //Vx = vector specified by v; V0, V1, V2, or[IR1, IR2, IR3]
            //Tx = translation vector specified by cv; TR or BK or Bugged / FC, or None

            Matrix mx = getMVMVA_Matrix();
            Vector3 vx = getMVMVA_Vector();
            (long tx, long ty, long tz) = getMVMVA_Translation();

            //MAC1 = (Tx1 * 1000h + Mx11 * Vx1 + Mx12 * Vx2 + Mx13 * Vx3) SAR(sf * 12)
            //MAC2 = (Tx2 * 1000h + Mx21 * Vx1 + Mx22 * Vx2 + Mx23 * Vx3) SAR(sf * 12)
            //MAC3 = (Tx3 * 1000h + Mx31 * Vx1 + Mx32 * Vx2 + Mx33 * Vx3) SAR(sf * 12)
            //[IR1, IR2, IR3] = [MAC1, MAC2, MAC3]

            MAC1 = (int)setMAC(1, (long)(tx * 0x1000 + mx.v1.x * vx.x + mx.v1.y * vx.y + mx.v1.z * vx.z) >> sf * 12);
            MAC2 = (int)setMAC(2, (long)(ty * 0x1000 + mx.v2.x * vx.x + mx.v2.y * vx.y + mx.v2.z * vx.z) >> sf * 12);
            MAC3 = (int)setMAC(3, (long)(tz * 0x1000 + mx.v3.x * vx.x + mx.v3.y * vx.y + mx.v3.z * vx.z) >> sf * 12);

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);
        }

        private (int trX, int trY, int trZ) getMVMVA_Translation() {
            switch (MVMVA_T_Vector) {
                case 0: return (TRX, TRY, TRZ);
                case 1: return (RBK, GBK, BBK);
                case 2: return (RFC, GFC, BFC);
                case 3: return (0, 0, 0);
                default: Console.WriteLine("[GTE] Unhandled MVMVA Translation Vector " + MVMVA_T_Vector); return (0, 0, 0);
            }
        }

        private Vector3 getMVMVA_Vector() {
            switch (MVMVA_M_Vector) {
                case 0: return V[0];
                case 1: return V[1];
                case 2: return V[2];
                case 3: return new Vector3() { x = IR[1], y = IR[2], z = IR[3] };
                default: Console.WriteLine("[GTE] Unhandled M Vector " + MVMVA_M_Matrix); return new Vector3();
            }
        }

        private Matrix getMVMVA_Matrix() {
            switch (MVMVA_M_Matrix) {
                case 0: return RT;
                case 1: return LM;
                case 2: return LRGB;
                default: Console.WriteLine("[GTE] Unhandled MVMVA Matrix " + MVMVA_M_Matrix); return new Matrix();
            }
        }

        private void GPL() {
            //[MAC1, MAC2, MAC3] = [MAC1, MAC2, MAC3] SHL(sf*12);<--- for GPL only
            //[MAC1, MAC2, MAC3] = (([IR1, IR2, IR3] * IR0) + [MAC1, MAC2, MAC3]) SAR(sf*12)
            // Color FIFO = [MAC1 / 16, MAC2 / 16, MAC3 / 16, CODE], [IR1, IR2, IR3] = [MAC1, MAC2, MAC3]
            //Note: Although the SHL in GPL is theoretically undone by the SAR, 44bit overflows can occur internally when sf=1.

            long mac1 = (long)MAC1 << sf * 12;
            long mac2 = (long)MAC2 << sf * 12;
            long mac3 = (long)MAC3 << sf * 12;

            MAC1 = (int)(setMAC(1, IR[1] * IR[0] + mac1) >> sf * 12); //this is a good example of why setMac cant return int directly
            MAC2 = (int)(setMAC(2, IR[2] * IR[0] + mac2) >> sf * 12); //as you cant >> before cause it dosnt triggers the flags and if
            MAC3 = (int)(setMAC(3, IR[3] * IR[0] + mac3) >> sf * 12); //you do it after you get wrong values

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);

            RGB[0] = RGB[1];
            RGB[1] = RGB[2];

            RGB[2].r = setRGB(1, MAC1 >> 4);
            RGB[2].g = setRGB(2, MAC2 >> 4);
            RGB[2].b = setRGB(3, MAC3 >> 4);
            RGB[2].c = RGBC.c;
        }

        private void GPF() {
            //[MAC1, MAC2, MAC3] = [0,0,0]                            ;<--- for GPF only
            //[MAC1, MAC2, MAC3] = (([IR1, IR2, IR3] * IR0) + [MAC1, MAC2, MAC3]) SAR(sf*12)
            // Color FIFO = [MAC1 / 16, MAC2 / 16, MAC3 / 16, CODE], [IR1, IR2, IR3] = [MAC1, MAC2, MAC3]

            MAC1 = (int)setMAC(1, IR[1] * IR[0]) >> sf * 12;
            MAC2 = (int)setMAC(2, IR[2] * IR[0]) >> sf * 12;
            MAC3 = (int)setMAC(3, IR[3] * IR[0]) >> sf * 12;

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);

            RGB[0] = RGB[1];
            RGB[1] = RGB[2];

            RGB[2].r = setRGB(1, MAC1 >> 4);
            RGB[2].g = setRGB(2, MAC2 >> 4);
            RGB[2].b = setRGB(3, MAC3 >> 4);
            RGB[2].c = RGBC.c;
        }

        private void NCDT() {
            NCDS(0);
            NCDS(1);
            NCDS(2);
        }

        private void OP() {
            //[MAC1, MAC2, MAC3] = [IR3*D2-IR2*D3, IR1*D3-IR3*D1, IR2*D1-IR1*D2] SAR(sf*12)
            //[IR1, IR2, IR3]    = [MAC1, MAC2, MAC3]                        ;copy result
            //Calculates the outer product of two signed 16bit vectors.
            //Note: D1,D2,D3 are meant to be the RT11,RT22,RT33 elements of the RT matrix "misused" as vector. lm should be usually zero.

            short d1 = RT.v1.x;
            short d2 = RT.v2.y;
            short d3 = RT.v3.z;

            MAC1 = (int)setMAC(1, ((IR[3] * d2) - (IR[2] * d3)) >> sf * 12);
            MAC2 = (int)setMAC(2, ((IR[1] * d3) - (IR[3] * d1)) >> sf * 12);
            MAC3 = (int)setMAC(3, ((IR[2] * d1) - (IR[1] * d2)) >> sf * 12);

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);
        }

        private void SQR() {
            MAC1 = (int)setMAC(1, (IR[1] * IR[1]) >> sf * 12);
            MAC2 = (int)setMAC(2, (IR[2] * IR[2]) >> sf * 12);
            MAC3 = (int)setMAC(3, (IR[3] * IR[3]) >> sf * 12);


            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);
        }

        private void AVSZ3() {
            //MAC0 = ZSF3 * (SZ1 + SZ2 + SZ3); for AVSZ3
            //OTZ = MAC0 / 1000h;for both(saturated to 0..FFFFh)
            long avsz3 = (long)ZSF3 * (SZ[1] + SZ[2] + SZ[3]);
            MAC0 = setMAC0(avsz3);
            OTZ = setSZ3(avsz3 >> 12);
        }

        private void AVSZ4() {
            //MAC0 = ZSF4 * (SZ0 + SZ1 + SZ2 + SZ3);for AVSZ4
            //OTZ = MAC0 / 1000h;for both(saturated to 0..FFFFh)
            long avsz4 = (long)ZSF4 * (SZ[0] + SZ[1] + SZ[2] + SZ[3]);
            MAC0 = setMAC0(avsz4);
            OTZ = setSZ3(avsz4 >> 12);
        }

        private void NCDS(int r) {
            //Normal color depth cue (single vector) //329048 WIP FLAGS
            //In: V0 = Normal vector(for triple variants repeated with V1 and V2),
            //BK = Background color, RGBC = Primary color / code, LLM = Light matrix, LCM = Color matrix, IR0 = Interpolation value.

            // [IR1, IR2, IR3] = [MAC1, MAC2, MAC3] = (LLM * V0) SAR(sf * 12)
            MAC1 = (int)(setMAC(1, (long)LM.v1.x * V[r].x + LM.v1.y * V[r].y + LM.v1.z * V[r].z) >> sf * 12);
            MAC2 = (int)(setMAC(2, (long)LM.v2.x * V[r].x + LM.v2.y * V[r].y + LM.v2.z * V[r].z) >> sf * 12);
            MAC3 = (int)(setMAC(3, (long)LM.v3.x * V[r].x + LM.v3.y * V[r].y + LM.v3.z * V[r].z) >> sf * 12);

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);

            //Console.WriteLine("NCDS " + ncdsTest + " " + MAC1.ToString("x8") + " " + MAC2.ToString("x8") + " " + MAC3.ToString("x8") + " " + (sf * 12).ToString("x1")
            //             + " " + IR[0].ToString("x4") + " " + IR[1].ToString("x4") + " " + IR[2].ToString("x4") + " " + IR[3].ToString("x4") + " " + FLAG.ToString("x8"));

            // [IR1, IR2, IR3] = [MAC1, MAC2, MAC3] = (BK * 1000h + LCM * IR) SAR(sf * 12)
            // WARNING each multiplication can trigger mac flags so the check is needed on each op! Somehow this only affects the color matrix and not the light one
            MAC1 = (int)(setMAC(1, setMAC(1, setMAC(1, (long)RBK * 0x1000 + LRGB.v1.x * IR[1]) + (long)LRGB.v1.y * IR[2]) + (long)LRGB.v1.z * IR[3]) >> sf * 12);
            MAC2 = (int)(setMAC(2, setMAC(2, setMAC(2, (long)GBK * 0x1000 + LRGB.v2.x * IR[1]) + (long)LRGB.v2.y * IR[2]) + (long)LRGB.v2.z * IR[3]) >> sf * 12);
            MAC3 = (int)(setMAC(3, setMAC(3, setMAC(3, (long)BBK * 0x1000 + LRGB.v3.x * IR[1]) + (long)LRGB.v3.y * IR[2]) + (long)LRGB.v3.z * IR[3]) >> sf * 12);

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);

            // [MAC1, MAC2, MAC3] = [R * IR1, G * IR2, B * IR3] SHL 4;< --- for NCDx / NCCx
            MAC1 = (int)setMAC(1, ((long)RGBC.r * IR[1]) << 4);
            MAC2 = (int)setMAC(2, ((long)RGBC.g * IR[2]) << 4);
            MAC3 = (int)setMAC(3, ((long)RGBC.b * IR[3]) << 4);

            interpolateColor(MAC1, MAC2, MAC3);

            // Color FIFO = [MAC1 / 16, MAC2 / 16, MAC3 / 16, CODE]
            RGB[0] = RGB[1];
            RGB[1] = RGB[2];

            RGB[2].r = setRGB(1, MAC1 >> 4);
            RGB[2].g = setRGB(2, MAC2 >> 4);
            RGB[2].b = setRGB(3, MAC3 >> 4);
            RGB[2].c = RGBC.c;
        }

        private void interpolateColor(int mac1, int mac2, int mac3) {
            // PSX SPX is very convoluted about this and it lacks some info
            // [MAC1, MAC2, MAC3] = MAC + (FC - MAC) * IR0;< --- for NCDx only
            // Note: Above "[IR1,IR2,IR3]=(FC-MAC)" is saturated to - 8000h..+7FFFh(ie. as if lm = 0)
            // Details on "MAC+(FC-MAC)*IR0":
            // [IR1, IR2, IR3] = (([RFC, GFC, BFC] SHL 12) - [MAC1, MAC2, MAC3]) SAR(sf * 12)
            // [MAC1, MAC2, MAC3] = (([IR1, IR2, IR3] * IR0) + [MAC1, MAC2, MAC3])
            // [MAC1, MAC2, MAC3] = [MAC1, MAC2, MAC3] SAR(sf * 12);< --- for NCDx / NCCx
            // [IR1, IR2, IR3] = [MAC1, MAC2, MAC3]

            MAC1 = (int)(setMAC(1, ((long)RFC << 12) - mac1) >> sf * 12);
            MAC2 = (int)(setMAC(2, ((long)GFC << 12) - mac2) >> sf * 12);
            MAC3 = (int)(setMAC(3, ((long)BFC << 12) - mac3) >> sf * 12);

            IR[1] = setIR(1, MAC1, false);
            IR[2] = setIR(2, MAC2, false);
            IR[3] = setIR(3, MAC3, false);

            MAC1 = (int)(setMAC(1, ((long)IR[1] * IR[0]) + mac1) >> sf * 12);
            MAC2 = (int)(setMAC(2, ((long)IR[2] * IR[0]) + mac2) >> sf * 12);
            MAC3 = (int)(setMAC(3, ((long)IR[3] * IR[0]) + mac3) >> sf * 12);

            IR[1] = setIR(1, MAC1, lm);
            IR[2] = setIR(2, MAC2, lm);
            IR[3] = setIR(3, MAC3, lm);
        }

        private void NCLIP() { //Normal clipping
            // MAC0 =   SX0*SY1 + SX1*SY2 + SX2*SY0 - SX0*SY2 - SX1*SY0 - SX2*SY1
            MAC0 = setMAC0((long)SXY[0].x * SXY[1].y + SXY[1].x * SXY[2].y + SXY[2].x * SXY[0].y - SXY[0].x * SXY[2].y - SXY[1].x * SXY[0].y - SXY[2].x * SXY[1].y);
        }
        private int setMAC0(long value) {
            if (value < -0x8000_0000) {
                FLAG |= 0x8000;
            } else if (value > 0x7FFF_FFFF) {
                FLAG |= 0x1_0000;
            }
            return (int)value;
        }

        private void RTPT() { //Perspective Transformation Triple
            RTPS(0);
            RTPS(1);
            RTPS(2);
        }

        private void RTPS(int r) {
            //IR1 = MAC1 = (TRX*1000h + RT11*VX0 + RT12*VY0 + RT13*VZ0) SAR (sf*12)
            //IR2 = MAC2 = (TRY*1000h + RT21*VX0 + RT22*VY0 + RT23*VZ0) SAR (sf*12)
            //IR3 = MAC3 = (TRZ*1000h + RT31*VX0 + RT32*VY0 + RT33*VZ0) SAR (sf*12)

            MAC1 = (int)setMAC(1, (TRX * 0x1000 + RT.v1.x * V[r].x + RT.v1.y * V[r].y + RT.v1.z * V[r].z) >> (sf * 12));
            MAC2 = (int)setMAC(2, (TRY * 0x1000 + RT.v2.x * V[r].x + RT.v2.y * V[r].y + RT.v2.z * V[r].z) >> (sf * 12));
            MAC3 = (int)setMAC(3, (TRZ * 0x1000 + RT.v3.x * V[r].x + RT.v3.y * V[r].y + RT.v3.z * V[r].z) >> (sf * 12));

            IR[1] = setIR(1, MAC1, false);
            IR[2] = setIR(2, MAC2, false);
            IR[3] = setIR(3, MAC3, false);

            //SZ3 = MAC3 SAR ((1-sf)*12)                           ;ScreenZ FIFO 0..+FFFFh

            SZ[0] = SZ[1];
            SZ[1] = SZ[2];
            SZ[2] = SZ[3];
            SZ[3] = setSZ3(MAC3 >> ((1 - sf) * 12));

            //MAC0=(((H*20000h/SZ3)+1)/2)*IR1+OFX, SX2=MAC0/10000h ;ScrX FIFO -400h..+3FFh
            //MAC0=(((H*20000h/SZ3)+1)/2)*IR2+OFY, SY2=MAC0/10000h ;ScrY FIFO -400h..+3FFh
            //MAC0=(((H*20000h/SZ3)+1)/2)*DQA+DQB, IR0=MAC0/1000h  ;Depth cueing 0..+1000h

            SXY[0] = SXY[1];
            SXY[1] = SXY[2];

            long result;
            long div = 0;
            if (SZ[3] == 0) {
                result = 0x1FFFF;
            } else {
                div = (((long)H * 0x20000 / SZ[3]) + 1) / 2;

                if (div > 0x1FFFF) {
                    result = 0x1FFFF;
                    FLAG |= 0x1 << 17;
                } else {
                    result = div;
                }
            }

            MAC0 = (int)(result * IR[1] + OFX);
            SXY[2].x = setSXY(2, MAC0 / 0x10000);
            MAC0 = (int)(result * IR[2] + OFY);
            SXY[2].y = setSXY(2, MAC0 / 0x10000);
            MAC0 = (int)(result * DQA + DQB);
            IR[0] = setIR0(MAC0 / 0x1000);


            // Console.WriteLine("RTPS " + ++rtpsTest + " FLAG " + FLAG.ToString("x8") + " SXY2 " + SXY[2].get().ToString("x8") + " SZ3 " + SZ[3].ToString("x8"));
            // Console.WriteLine(((uint)MAC0).ToString("x8") + " " + ((uint)MAC1).ToString("x8") + " " + ((uint)MAC2).ToString("x8") + " " + ((uint)MAC3).ToString("x8"));
            // Console.WriteLine(((uint)IR[0]).ToString("x8") + " " + ((uint)IR[1]).ToString("x8") + " " + ((uint)IR[2]).ToString("x8") + " " + ((uint)IR[3]).ToString("x8"));
            //Console.ReadLine();
        }
        private short setIR0(long value) {
            if (value < 0) {
                FLAG |= 0x1000;
                return 0;
            }

            if (value > 0x1000) {
                FLAG |= 0x1000;
                return 0x1000;
            }

            return (short)value;
        }
        private short setSXY(int i, int value) {
            if (value < -0x400) {
                FLAG |= (uint)(0x4000 >> (i - 1));
                return -0x400;
            }

            if (value > 0x3FF) {
                FLAG |= (uint)(0x4000 >> (i - 1));
                return 0x3FF;
            }

            return (short)value;
        }
        private ushort setSZ3(long value) {
            if (value < 0) {
                FLAG |= 0x4_0000;
                return 0;
            }

            if (value > 0xFFFF) {
                FLAG |= 0x4_0000;
                return 0xFFFF;
            }

            return (ushort)value;
        }

        private byte setRGB(int i, int value) {
            if (value < 0) {
                FLAG |= (uint)0x20_0000 >> (i - 1);
                return 0;
            }

            if (value > 0xFF) {
                FLAG |= (uint)0x20_0000 >> (i - 1);
                return 0xFF;
            }

            return (byte)value;
        }

        private short setIR(int i, int value, bool lm) {
            if (lm && value < 0) {
                FLAG |= (uint)(0x100_0000 >> (i - 1));
                return 0;
            }

            if (!lm && (value < -0x8000)) {
                FLAG |= (uint)(0x100_0000 >> (i - 1));
                return -0x8000;
            }

            if (value > 0x7FFF) {
                FLAG |= (uint)(0x100_0000 >> (i - 1));
                return 0x7FFF;
            }

            return (short)value;
        }

        private long setMAC(int i, long value) {
            //Console.WriteLine((i) + " " + value.ToString("x16"));
            if (value < -0x800_0000_0000) {
                //Console.WriteLine("under");
                FLAG |= (uint)(0x800_0000 >> (i - 1));
            }

            if (value > 0x7FF_FFFF_FFFF) {
                //Console.WriteLine("over");
                FLAG |= (uint)(0x4000_0000 >> (i - 1));
            }

            return (value << 20) >> 20;
        }

        private static short saturateRGB(int v) {
            short saturate = (short)v;
            if (saturate < 0x00) return 0x00;
            else if (saturate > 0x1F) return 0x1F;
            else return saturate;
        }

        private static int leadingCount(uint v) {
            uint sign = (v >> 31);
            int leadingCount = 0;
            for (int i = 0; i < 32; i++) {
                if (v >> 31 != sign) break;
                leadingCount++;
                v <<= 1;
            }
            return leadingCount;
        }

        internal uint loadData(uint fs) {
            uint value;
            switch (fs) {
                case 00: value = V[0].XY; break;
                case 01: value = (uint)V[0].z; break;
                case 02: value = V[1].XY; break;
                case 03: value = (uint)V[1].z; break;
                case 04: value = V[2].XY; break;
                case 05: value = (uint)V[2].z; break;
                case 06: value = RGBC.val; break;
                case 07: value = OTZ; break;
                case 08: value = (uint)IR[0]; break;
                case 09: value = (uint)IR[1]; break;
                case 10: value = (uint)IR[2]; break;
                case 11: value = (uint)IR[3]; break;
                case 12: value = SXY[0].val; break;
                case 13: value = SXY[1].val; break;
                case 14: //Mirror
                case 15: value = SXY[2].val; break;
                case 16: value = SZ[0]; break;
                case 17: value = SZ[1]; break;
                case 18: value = SZ[2]; break;
                case 19: value = SZ[3]; break;
                case 20: value = RGB[0].val; break;
                case 21: value = RGB[1].val; break;
                case 22: value = RGB[2].val; break;
                case 23: value = RES1; break; //Prohibited Register
                case 24: value = (uint)MAC0; break;
                case 25: value = (uint)MAC1; break;
                case 26: value = (uint)MAC2; break;
                case 27: value = (uint)MAC3; break;
                case 28:/* value = IRGB; break;*/
                case 29:/* value = ORGB; break;*/
                    IRGB = (ushort)(saturateRGB(IR[3] / 0x80) << 10 | saturateRGB(IR[2] / 0x80) << 5 | (ushort)saturateRGB(IR[1] / 0x80));
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

        private int test;
        internal void writeData(uint fs, uint v) {
            //Console.WriteLine("GTE Write Data R" + fs + ": " + v.ToString("x8"));
            //Console.WriteLine(v.ToString("x8"));
            //Console.ReadLine();
            switch (fs) {
                case 00: V[0].XY = v; break;
                case 01: V[0].z = (short)v; break;
                case 02: V[1].XY = v; break;
                case 03: V[1].z = (short)v; break;
                case 04: V[2].XY = v; break;
                case 05: V[2].z = (short)v; break;
                case 06: RGBC.val = v; break;
                case 07: OTZ = (ushort)v; break;
                case 08: IR[0] = (short)v; break;
                case 09: IR[1] = (short)v; break;
                case 10: IR[2] = (short)v; break;
                case 11: IR[3] = (short)v; break;
                case 12: SXY[0].val = v; break;
                case 13: SXY[1].val = v; break;
                case 14: SXY[2].val = v; break;
                case 15: SXY[0] = SXY[1]; SXY[1] = SXY[2]; SXY[2].val = v; break; //On load mirrors 0x14 on write cycles the fifo
                case 16: SZ[0] = (ushort)v; break;
                case 17: SZ[1] = (ushort)v; break;
                case 18: SZ[2] = (ushort)v; break;
                case 19: SZ[3] = (ushort)v; break;
                case 20: RGB[0].val = v; break;
                case 21: RGB[1].val = v; break;
                case 22: RGB[2].val = v; break;
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

        internal uint loadControl(uint fs) {
            uint value;
            switch (fs) {
                case 00: value = RT.v1.XY; break;
                case 01: value = (ushort)RT.v1.z | (uint)(RT.v2.x << 16); break;
                case 02: value = (ushort)RT.v2.y | (uint)(RT.v2.z << 16); break;
                case 03: value = RT.v3.XY; break;
                case 04: value = (uint)RT.v3.z; break;
                case 05: value = (uint)TRX; break;
                case 06: value = (uint)TRY; break;
                case 07: value = (uint)TRZ; break;
                case 08: value = LM.v1.XY; break;
                case 09: value = (ushort)LM.v1.z | (uint)(LM.v2.x << 16); break;
                case 10: value = (ushort)LM.v2.y | (uint)(LM.v2.z << 16); break;
                case 11: value = LM.v3.XY; break;
                case 12: value = (uint)LM.v3.z; break;
                case 13: value = (uint)RBK; break;
                case 14: value = (uint)GBK; break;
                case 15: value = (uint)BBK; break;
                case 16: value = LRGB.v1.XY; break;
                case 17: value = (ushort)LRGB.v1.z | (uint)(LRGB.v2.x << 16); break;
                case 18: value = (ushort)LRGB.v2.y | (uint)(LRGB.v2.z << 16); break;
                case 19: value = LRGB.v3.XY; break;
                case 20: value = (uint)LRGB.v3.z; break;
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
                case 01: RT.v1.z = (short)v; RT.v2.x = (short)(v >> 16); break;
                case 02: RT.v2.y = (short)v; RT.v2.z = (short)(v >> 16); break;
                case 03: RT.v3.XY = v; break;
                case 04: RT.v3.z = (short)v; break;
                case 05: TRX = (int)v; break;
                case 06: TRY = (int)v; break;
                case 07: TRZ = (int)v; break;
                case 08: LM.v1.XY = v; break;
                case 09: LM.v1.z = (short)v; LM.v2.x = (short)(v >> 16); break;
                case 10: LM.v2.y = (short)v; LM.v2.z = (short)(v >> 16); break;
                case 11: LM.v3.XY = v; break;
                case 12: LM.v3.z = (short)v; break;
                case 13: RBK = (int)v; break;
                case 14: GBK = (int)v; break;
                case 15: BBK = (int)v; break;
                case 16: LRGB.v1.XY = v; break;
                case 17: LRGB.v1.z = (short)v; LRGB.v2.x = (short)(v >> 16); break;
                case 18: LRGB.v2.y = (short)v; LRGB.v2.z = (short)(v >> 16); break;
                case 19: LRGB.v3.XY = v; break;
                case 20: LRGB.v3.z = (short)v; break;
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
            string gteDebug = "GTE CONTROL\n";
            for (uint i = 0; i < 32; i++) {
                gteDebug += $" {i:00}: {loadControl(i):x8}";
                if ((i + 1) % 4 == 0) gteDebug += "\n";
            }

            gteDebug += "GTE DATA\n";
            for (uint i = 0; i < 32; i++) {
                gteDebug += $" {i:00}: {loadData(i):x8}";
                if ((i + 1) % 4 == 0) gteDebug += "\n";
            }

            Console.WriteLine(gteDebug);
            Console.ReadLine();
        }
    }
}
