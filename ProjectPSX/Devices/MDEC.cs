using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProjectPSX.Devices {
    public class MDEC {

        private const int NUM_BLOCKS = 6;
        //For some reason even tho it iterates all blocks it starts at 4
        //going 4 (Cr), 5 (Cb), 0, 1, 2, 3 (Y) 
        private const int INITIAL_BLOCK = 4;
        private const int MACRO_BLOCK_DECODED_BYTES = 256 * 3;

        //Status Register
        //private bool isDataOutFifoEmpty;
        private bool isDataInFifoFull;
        private bool isCommandBusy;
        private bool isDataInRequested;
        private bool isDataOutRequested;
        private uint dataOutputDepth;
        private bool isSigned;
        private uint bit15;
        private uint currentBlock = INITIAL_BLOCK;
        private uint remainingDataWords;

        private bool isColored;

        private byte[] luminanceQuantTable = new byte[64];
        private byte[] colorQuantTable = new byte[64];
        private short[] scaleTable = new short[64];

        private Action command;

        private short[][] block = { new short[64], new short[64], new short[64], new short[64], new short[64], new short[64] };

        private short[] dst = new short[64];

        private Queue<ushort> inBuffer = new Queue<ushort>(1024);

        private IMemoryOwner<byte> outBuffer = MemoryPool<byte>.Shared.Rent(0x30000); //wild guess while resumable dmas come...
        private int outBufferPos = 0;

        public void writeMDEC0_Command(uint value) { //1F801820h - MDEC0 - MDEC Command/Parameter Register (W)
            //Console.WriteLine("[MDEC] Write " + value.ToString("x8"));
            if (remainingDataWords == 0) {
                //Console.WriteLine("decoding " + value.ToString("x8"));
                decodeCommand(value);
            } else {
                inBuffer.Enqueue((ushort)value);
                inBuffer.Enqueue((ushort)(value >> 16));

                remainingDataWords--;
                //Console.WriteLine("[MDEC] remaining " + remainingDataWords);
            }

            if (remainingDataWords == 0) {
                isCommandBusy = true;
                yuvToRgbBlockPos = 0;
                outBufferPos = 0;
                command();
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

            while (inBuffer.Count != 0) {

                for (int i = 0; i < NUM_BLOCKS; i++) { //Try to decode a macro block (6 blocks)
                    //But actually iterate from the current block
                    byte[] qt = currentBlock >= 4 ? colorQuantTable : luminanceQuantTable;
                    //todo check if was success and idct and currentblock++ there so we can return here
                    //and recover state when handling resumable macroblock decoding...
                    rl_decode_block(block[currentBlock], qt);
                    idct_core(block[currentBlock]);

                    currentBlock++;
                    currentBlock %= NUM_BLOCKS;
                }

                yuv_to_rgb(block[0], 0, 0);
                yuv_to_rgb(block[1], 8, 0);
                yuv_to_rgb(block[2], 0, 8);
                yuv_to_rgb(block[3], 8, 8);

                yuvToRgbBlockPos += MACRO_BLOCK_DECODED_BYTES;

                //for (int i = 0; i < output.Length; i++) {
                //    Console.WriteLine(i + " " + output[i].ToString("x8"));
                //}
                //Console.WriteLine("MacroBlock decoded " + ++block + " srcPointer " + srcPointer + " bufferPtr " + ptr);
            }
            //Console.WriteLine("Finalized decode" + srcPointer + " " + ptr);
        }

        int yuvToRgbBlockPos = 0;
        private void yuv_to_rgb(short[] Yblk, int xx, int yy) {
            for (int y = 0; y < 8; y++) {
                for (int x = 0; x < 8; x++) {
                    int R = block[4][((x + xx) / 2) + ((y + yy) / 2) * 8]; //CR Block
                    int B = block[5][((x + xx) / 2) + ((y + yy) / 2) * 8]; //CB Block
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

                    int position = ((x + xx + ((y + yy) * 16)) * 3) + yuvToRgbBlockPos;
                    Span<byte> rgb = stackalloc byte[3] { (byte)R, (byte)G, (byte)B };
                    var dest = outBuffer.Memory.Span.Slice(position, 3);
                    rgb.CopyTo(dest);
                }
            }
        }

        private int blockPointer;
        private int q_scale;
        private int val;
        private ushort n;
        public void rl_decode_block(short[] blk, byte[] qt) {
            if (blockPointer >= 64) { //Start of new block
                for (int i = 0; i < blk.Length; i++) {
                    blk[i] = 0;
                }

                if (inBuffer.Count == 0) return;
                n = inBuffer.Dequeue();
                while (n == 0xFE00) {
                    if (inBuffer.Count == 0) return;
                    n = inBuffer.Dequeue();
                }

                q_scale = (n >> 10) & 0x3F;
                val = signed10bit(n & 0x3FF) * qt[0];

                blockPointer = 0;
            }


            while (blockPointer < blk.Length) {
                if (q_scale == 0) {
                    val = signed10bit(n & 0x3FF) * 2;
                }

                val = Math.Min(Math.Max(val, -0x400), 0x3FF);

                if (q_scale > 0) {
                    blk[zagzig[blockPointer]] = (short)val;
                }

                if (q_scale == 0) {
                    blk[blockPointer] = (short)val;
                }

                if (inBuffer.Count == 0) return;
                n = inBuffer.Dequeue();

                blockPointer += ((n >> 10) & 0x3F) + 1;

                val = (signed10bit(n & 0x3FF) * qt[blockPointer & 0x3F] * q_scale + 4) / 8;
            }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int signed10bit(int n) => (n << 22) >> 22;

        private void setQuantTable() {//64 unsigned parameter bytes for the Luminance Quant Table (used for Y1..Y4)
            for (int i = 0; i < 32; i++) { //16 words for each table
                ushort value = inBuffer.Dequeue();
                luminanceQuantTable[i * 2 + 0] = (byte)value;
                luminanceQuantTable[i * 2 + 1] = (byte)(value >> 8);
            }

            //Console.WriteLine("setQuantTable: Luminance");

            if (!isColored) return; //and if Command.Bit0 was set, by another 64 unsigned parameter bytes for the Color Quant Table(used for Cb and Cr).

            for (int i = 0; i < 32; i++) { //16 words continuation from buffer
                ushort value = inBuffer.Dequeue();
                colorQuantTable[i * 2 + 0] = (byte)value;
                colorQuantTable[i * 2 + 1] = (byte)(value >> 8);
            }

            //Console.WriteLine("setQuantTable: color");
        }

        private void setScaleTable() {//64 signed halfwords with 14bit fractional part
            for (int i = 0; i < 64; i++) { //writed as 32 words on buffer
                scaleTable[i] = (short)inBuffer.Dequeue();
            }
        }

        public void writeMDEC1_Control(uint value) { //1F801824h - MDEC1 - MDEC Control/Reset Register (W)
            bool abortCommand = ((value >> 31) & 0x1) == 1;
            if (abortCommand) { //Set status to 80040000h
                outBufferPos = 0;
                currentBlock = 4;
                remainingDataWords = 0;
                command = null;
            }

            isDataInRequested = ((value >> 30) & 0x1) == 1; //todo enable dma
            isDataOutRequested = ((value >> 29) & 0x1) == 1;

            //Console.WriteLine("[MDEC] dataInRequest " + isDataInRequested + " dataOutRequested " + isDataOutRequested);
        }


        public uint readMDEC0_Data() { //1F801820h.Read - MDEC Data/Response Register (R)
            if (dataOutputDepth == 2) { //2 24b
                var span = outBuffer.Memory.Span.Slice(outBufferPos++ * 4, 4);

                return Unsafe.ReadUnaligned<uint>(ref MemoryMarshal.GetReference(span));
            } else if (dataOutputDepth == 3) { //3 15b
                var span = outBuffer.Memory.Span.Slice(outBufferPos++ * 6, 6);

                ushort lo = (ushort)(bit15 << 15 | convert24to15bpp(span[0], span[1], span[2]));
                ushort hi = (ushort)(bit15 << 15 | convert24to15bpp(span[3], span[4], span[5]));

                return (uint)(hi << 16 | lo);
            } else {
                return 0x00FF_00FF;
            }

        }

        public Span<uint> processDmaLoad(int size) {
            if (dataOutputDepth == 2) { //2 24b
                var byteSpan = outBuffer.Memory.Span.Slice(outBufferPos++ * 4 * size, 4 * size); //4 = RGBR, GBRG, BRGB...

                return MemoryMarshal.Cast<byte, uint>(byteSpan);
            } else if (dataOutputDepth == 3) { //3 15b
                var byteSpan = outBuffer.Memory.Span.Slice(outBufferPos++ * 6 * size, 6 * size); //6 = RGB * 2 => b15|b15

                for (int b24 = 0, b15 = 0; b24 < byteSpan.Length; b24 += 3, b15 += 2) {
                    var r = byteSpan[b24 + 0] >> 3;
                    var g = byteSpan[b24 + 1] >> 3;
                    var b = byteSpan[b24 + 2] >> 3;

                    var rgb15 = (ushort)(b << 10 | g << 5 | r);
                    var lo = (byte)rgb15;
                    var hi = (byte)(rgb15 >> 8);

                    byteSpan[b15 + 0] = lo;
                    byteSpan[b15 + 1] = hi;
                }

                var dma = MemoryMarshal.Cast<byte, uint>(byteSpan.Slice(0, 4 * size));

                return dma;
            } else { //unsupported mode allocate and return garbage so can be seen
                uint[] garbage = new uint[size];
                var dma = new Span<uint>(garbage);
                dma.Fill(0xFF00FF00);
                return dma;
            }
        }

        public ushort convert24to15bpp(byte sr, byte sg, byte sb) {
            byte r = (byte)(sr >> 3);
            byte g = (byte)(sg >> 3);
            byte b = (byte)(sb >> 3);

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
            status |= currentBlock << 16;
            status |= (ushort)(remainingDataWords - 1);
            //Console.WriteLine("[MDEC] Load Status " + status.ToString("x8"));
            //Console.ReadLine();

            isCommandBusy = false;

            return status;
        }

        private bool isDataOutFifoEmpty() => outBufferPos == 0; //Todo compare to inbufferpos when we handle full dma in

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
