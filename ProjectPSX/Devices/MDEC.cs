using System;
using System.Collections.Generic;

namespace ProjectPSX.Devices {
    public class MDEC {

        //Status Register
        //private bool isDataOutFifoEmpty;
        private bool isDataInFifoFull;
        private bool isCommandBusy;
        private bool isDataInRequested;
        private bool isDataOutRequested;
        private uint dataOutputDepth;
        private bool isSigned;
        private uint bit15;
        private uint currentBlock;
        private uint remainingDataWords;

        private bool isColored;

        private byte[] luminanceQuantTable = new byte[64];
        private byte[] colorQuantTable = new byte[64];
        private short[] scaleTable = new short[64];

        private Action command;

        private short[] Crblk = new short[64];
        private short[] Cbblk = new short[64];
        private short[][] Yblk = { new short[64], new short[64], new short[64], new short[64] };

        private short[] dst = new short[64];

        private ushort[] src = new ushort[0xFFFF]; //TEST TODO: REVISIT THIS "it works"...
        private uint[] inBuffer = new uint[0xFFFF]; //this is badly wrong. should be 0x20 and handle dmas
        private Queue<uint> outBuffer = new Queue<uint>();
        private int ptr;

        private uint[] output = new uint[256];


        public void writeMDEC0_Command(uint value) { //1F801820h - MDEC0 - MDEC Command/Parameter Register (W)
            //Console.WriteLine("[MDEC] Write " + value.ToString("x8"));
            if (remainingDataWords == 0) {
                //Console.WriteLine("decoding " + value.ToString("x8"));
                decodeCommand(value);
            } else {
                inBuffer[ptr++] = value;
                remainingDataWords--;
                //Console.WriteLine("[MDEC] remaining " + remainingDataWords);
            }

            if (remainingDataWords == 0) {
                isCommandBusy = true;
                outBuffer.Clear();
                command();
                ptr = 0;
                srcPointer = 0;
            }

        }

        private void decodeCommand(uint value) {
            uint rawCommand = value >> 29;
            dataOutputDepth = (value >> 27) & 0x3;
            isSigned = ((value >> 26) & 0x1) == 1;
            bit15 = (value >> 25) & 0x1;
            remainingDataWords = (value & 0xFFFF);
            isColored = (value & 0x1) == 1; //only used on command2;

            switch (rawCommand) {
                case 1: command = decodeMacroBlocks; break;
                case 2: command = setQuantTable; remainingDataWords = 16 + (isColored ? 16u : 0); break;
                case 3: command = setScaleTable; remainingDataWords = 32; break;
                default: Console.WriteLine("[MDEC] Unhandled Command " + rawCommand); break;
            }
        }

        private void decodeMacroBlocks() {
            Buffer.BlockCopy(inBuffer, 0, src, 0, inBuffer.Length);

            while (srcPointer < ptr * 2) {

                rl_decode_block(Crblk, src, colorQuantTable);
                rl_decode_block(Cbblk, src, colorQuantTable);

                clearOutput();

                rl_decode_block(Yblk[0], src, luminanceQuantTable); 
                rl_decode_block(Yblk[1], src, luminanceQuantTable); 
                rl_decode_block(Yblk[2], src, luminanceQuantTable); 
                rl_decode_block(Yblk[3], src, luminanceQuantTable); 

                yuv_to_rgb(Yblk[0], 0, 0, output);
                yuv_to_rgb(Yblk[2], 0, 8, output);
                yuv_to_rgb(Yblk[1], 8, 0, output);
                yuv_to_rgb(Yblk[3], 8, 8, output);

                writeOutputToOutQueue();

                //for (int i = 0; i < output.Length; i++) {
                //    Console.WriteLine(i + " " + output[i].ToString("x8"));
                //}
                //Console.WriteLine("MacroBlock decoded " + ++block + " srcPointer " + srcPointer + " bufferPtr " + ptr);
            }
            //Console.WriteLine("Finalized decode" + srcPointer + " " + ptr);
        }

        private void writeOutputToOutQueue() {
            for (int i = 0; i < output.Length; i++) {
                outBuffer.Enqueue(output[i]);
            }
        }

        private void clearOutput() {
            for (int i = 0; i < output.Length; i++) {
                output[i] = 0;
            }
        }

        private void yuv_to_rgb(short[] Yblk, int xx, int yy, uint[] output) {
            for (int y = 0; y < 8; y++) {
                for (int x = 0; x < 8; x++) {
                    int R = Crblk[((x + xx) / 2) + ((y + yy) / 2) * 8];
                    int B = Cbblk[((x + xx) / 2) + ((y + yy) / 2) * 8];
                    int G = (int)((-0.3437 * B) + (-0.7143 * R));

                    R = (int)(1.402 * R);
                    B = (int)(1.772 * B);
                    int Y = Yblk[x + y * 8];

                    R = Math.Min(Math.Max(Y + R, -128), 127);
                    G = Math.Min(Math.Max(Y + G, -128), 127);
                    B = Math.Min(Math.Max(Y + B, -128), 127);

                    R ^= 0x80;
                    G ^= 0x80;
                    B ^= 0x80;

                    uint val = (uint)((byte)B << 16 | (byte)G << 8 | (byte)R);

                    output[(x + xx) + ((y + yy) * 16)] = val;
                }
            }
        }

        private int srcPointer;
        public void rl_decode_block(short[] blk, ushort[] src, byte[] qt) {
            for (int i = 0; i < blk.Length; i++) {
                blk[i] = 0;
            }

            ushort n = src[srcPointer++];
            while(n == 0xFE00) {
                n = src[srcPointer++];
            }

            var q_scale = (n >> 10) & 0x3F;
            int val = signed10bit(n & 0x3FF) * qt[0];

            for (int i = 0; i < blk.Length;/*i advanced based on in loop values*/) {
                if (q_scale == 0) {
                    val = (ushort)(signed10bit((ushort)(n & 0x3FF)) * 2);
                    blk[i] = (short)val;
                }

                val = (ushort)Math.Min(Math.Max((int)(short)val, -0x400), 0x3FF);

                if (q_scale > 0) {
                    blk[zagzig[i]] = (short)val;
                }

                n = src[srcPointer++];

                i = i + ((n >> 10) & 0x3F) + 1;

                val = (signed10bit(n & 0x3FF) * qt[i & 0x3F] * q_scale + 4) / 8;
            }
            idct_core(blk);
        }

        private void idct_core(short[] src) {
            for (int i = 0; i < 2; i++) {
                for (int x = 0; x < 8; x++) {
                    for (int y = 0; y < 8; y++) {
                        int sum = 0;
                        for (int z = 0; z < 8; z++) {
                            sum = (sum + src[y + z * 8] * (scaleTable[x + z * 8] / 8));
                        }
                        dst[x + y * 8] = (short)((sum + 0xFFF) / 0x2000);
                    }
                }
                (dst, src) = (src, dst);
            }
        }

        private int signed10bit(int n) {
            return (n << 22) >> 22;
        }

        private void setQuantTable() {//64 unsigned parameter bytes for the Luminance Quant Table (used for Y1..Y4), and if Command.Bit0 was set, by another 64 unsigned parameter bytes for the Color Quant Table (used for Cb and Cr).
            for (int i = 0; i < 16; i++) { //16 words for each table
                luminanceQuantTable[i * 4] = (byte)(inBuffer[i] & 0xFF);
                luminanceQuantTable[i * 4 + 1] = (byte)(inBuffer[i] >> 8 & 0xFF);
                luminanceQuantTable[i * 4 + 2] = (byte)(inBuffer[i] >> 16 & 0xFF);
                luminanceQuantTable[i * 4 + 3] = (byte)(inBuffer[i] >> 24 & 0xFF);
            }

            //Console.WriteLine("setQuantTable: Luminance");

            if (!isColored) return;

            for (int i = 0; i < 16; i++) { //16 words continuation from buffer
                colorQuantTable[i * 4] = (byte)(inBuffer[i + 16] & 0xFF);
                colorQuantTable[i * 4 + 1] = (byte)(inBuffer[i + 16] >> 8 & 0xFF);
                colorQuantTable[i * 4 + 2] = (byte)(inBuffer[i + 16] >> 16 & 0xFF);
                colorQuantTable[i * 4 + 3] = (byte)(inBuffer[i + 16] >> 24 & 0xFF);
            }

            //Console.WriteLine("setQuantTable: color");
        }

        private void setScaleTable() {//64 signed halfwords with 14bit fractional part
            for (int i = 0; i < 32; i++) { //writed as 32 words on buffer
                scaleTable[i * 2] = (short)(inBuffer[i] & 0xFFFF);
                scaleTable[i * 2 + 1] = (short)(inBuffer[i] >> 16);
            }
        }

        public void writeMDEC1_Control(uint value) { //1F801824h - MDEC1 - MDEC Control/Reset Register (W)
            bool isDataOutFifoEmpty = ((value >> 31) & 0x1) == 1; //todo actual abort commands and set status to 80040000h
            if (isDataOutFifoEmpty) outBuffer.Clear();
            isDataInRequested = ((value >> 30) & 0x1) == 1; //todo enable dma
            isDataOutRequested = ((value >> 29) & 0x1) == 1;

            //Console.WriteLine("[MDEC] dataInRequest " + isDataInRequested + " dataOutRequested " + isDataOutRequested);
        }

        //int decodeTest = 0;
        int bgrOrder;
        uint bgrBuffer;
        public uint readMDEC0_Data() { //1F801820h.Read - MDEC Data/Response Register (R)
            //return inBuffer[ptr++];
            //Console.WriteLine(dataOutputDepth + " " + decodeTest++);

            if (dataOutputDepth == 2 && outBuffer.Count > 0) {
                switch (bgrOrder) { // 24bpp data is compacted on the DMA word queue as: ...[(BGR)(B] [GR)(BG] [R)(BGR)] but the queue is uint as 0BGR
                    case 0:
                        uint _BGR = outBuffer.Dequeue() & 0x00FF_FFFF;
                        bgrBuffer = outBuffer.Dequeue();
                        uint R___ = (bgrBuffer & 0x0000_00FF) << 24;

                        bgrOrder++;

                        return R___ | _BGR;

                    case 1:
                        uint __BG = (bgrBuffer & 0x00FF_FF00) >> 8;
                        bgrBuffer = outBuffer.Dequeue();
                        uint GR__ = (bgrBuffer & 0x0000_FFFF) << 16;

                        bgrOrder++;

                        return GR__ | __BG;

                    case 2:
                        uint ___B = (bgrBuffer & 0x00FF_0000) >> 16;
                        uint BGR_ = (outBuffer.Dequeue() & 0x00FF_FFFF) << 8;

                        bgrOrder = 0;

                        return BGR_ | ___B;
                }

            } else if (dataOutputDepth == 3 && outBuffer.Count > 0) { //3 15b, 2 24b the count is for testing
                ushort u1 = (ushort)(bit15 << 15 | convert24to15bpp(outBuffer.Dequeue()));
                ushort u2 = (ushort)(bit15 << 15 | convert24to15bpp(outBuffer.Dequeue()));
                //Console.WriteLine("decodeTest? " + decodeTest++ + "Depth " + dataOutputDepth + " outBufferCount" + outBuffer.Count());
                return (uint)(u2 << 16 | u1);
            }

            return 0x00FF_00FF;
        }

        public ushort convert24to15bpp(uint value) {
            //byte m = (byte)(val >> 15);
            byte r = (byte)((value & 0xFF) >> 3);
            byte g = (byte)(((value >> 8) & 0xFF) >> 3);
            byte b = (byte)(((value >> 16) & 0xFF) >> 3);

            return (ushort)(b << 10 | g << 5 | r);
        }

        public uint readMDEC1_Status() {//1F801824h - MDEC1 - MDEC Status Register (R)
            uint status = 0;

            status |= (isDataOutFifoEmpty() ? 1u : 0) << 31;
            status |= (isDataInFifoFull ? 1u : 0) << 30;
            status |= (isCommandBusy ? 1u : 0) << 29;
            status |= (isDataInRequested ? 1u : 0) << 28;
            status |= (isDataOutRequested ? 1u : 0) << 27;
            status |= dataOutputDepth << 25;
            status |= (isSigned ? 1u : 0) << 24;
            status |= bit15 << 23;
            status |= 1 << 18; // weird status return for 0x8004_0000;
            status |= currentBlock << 16;
            status |= (ushort)(remainingDataWords - 1);
            //Console.WriteLine("[MDEC] Load Status " + status.ToString("x8"));
            //Console.ReadLine();

            isCommandBusy = false;

            return status;
        }

        private bool isDataOutFifoEmpty() {
            return outBuffer.Count == 0;
        }

        private static ReadOnlySpan<byte> zagzig => new byte[] {
             0,  1,  8, 16,  9,  2,  3, 10,
            17, 24, 32, 25, 18, 11,  4,  5,
            12, 19, 26, 33, 40, 48, 41, 34,
            27, 20, 13,  6,  7, 14, 21, 28,
            35, 42, 49, 56, 57, 50, 43, 36,
            29, 22, 15, 23, 30, 37, 44, 51,
            58, 59, 52, 45, 38, 31, 39, 46,
            53, 60, 61, 54, 47, 55, 62, 63
        };

    }
}
