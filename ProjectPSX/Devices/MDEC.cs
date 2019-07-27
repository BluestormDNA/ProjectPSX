using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectPSX.Devices {
    class MDEC {

        private uint macroBlockData;

        //Status Register
        private bool isDataOutFifoEmpty;
        private bool isDataInFifoFull;
        private bool isCommandBusy;
        private bool isDataInRequested;
        private bool isDataOutRequested;
        private uint dataOutputDepth;
        private bool isSigned;
        private bool isBit15;
        private uint currentBlock;
        private uint remainingDataWords;

        private bool isColored;

        private byte[] luminanceQuantTable = new byte[64];
        private byte[] colorQuantTable = new byte[64];
        private short[] scaleTable = new short[64];

        private Action command;
        private uint[] inBuffer = new uint[0xFFFF]; //this is badly wrong. should be 0x20 and handle dmas
        private uint[] outBuffer = new uint[0xFFFF];//accordlingly but each macrobock is processed as a whole
        private int ptr;

        public MDEC() {
            Array.Reverse(zagzig);
        }

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
                ptr = 0;
                command();
            }

        }

        private void decodeCommand(uint value) {
            uint rawCommand = value >> 29;
            dataOutputDepth = (value >> 27) & 0x3;
            isSigned = ((value >> 26) & 0x1) == 1;
            isBit15 = ((value >> 25) & 0x1) == 1;
            remainingDataWords = value & 0xFFFF;
            isColored = (value & 0x1) == 1; //only used on command2;

            switch (rawCommand) {
                case 1: command = decodeMacroBlocks; break;
                case 2: command = setQuantTable; remainingDataWords = 16 + (isColored ? 16u : 0); break;
                case 3: command = setScaleTable; remainingDataWords = 32; break;
                default: Console.WriteLine("[MDEC] Unhandled Command " + rawCommand); break;
            }
        }

        private void decodeMacroBlocks() {//in halfwords
            for (int i = 0; i < remainingDataWords; i++) {
                short block0 = (short)(inBuffer[i] & 0xFFFF);
                short block1 = (short)(inBuffer[i] >> 16);

                decodeMacroBlock(block0);
                decodeMacroBlock(block1);
            }
        }

        private void decodeMacroBlock(short block0) {
            //todo actual decode something
        }

        private void setQuantTable() {//64 unsigned parameter bytes for the Luminance Quant Table (used for Y1..Y4), and if Command.Bit0 was set, by another 64 unsigned parameter bytes for the Color Quant Table (used for Cb and Cr).
            for (int i = 0; i < 16; i++) { //16 words for each table
                luminanceQuantTable[i * 4] = (byte)(inBuffer[i] & 0xFF);
                luminanceQuantTable[i * 4 + 1] = (byte)(inBuffer[i] >> 8 & 0xFF);
                luminanceQuantTable[i * 4 + 2] = (byte)(inBuffer[i] >> 16 & 0xFF);
                luminanceQuantTable[i * 4 + 3] = (byte)(inBuffer[i] >> 24 & 0xFF);
            }

            if (!isColored) return;

            for (int i = 0; i < 16; i++) { //16 words continuation from buffer
                colorQuantTable[i * 4] = (byte)(inBuffer[i + 16] & 0xFF);
                colorQuantTable[i * 4 + 1] = (byte)(inBuffer[i + 16] >> 8 & 0xFF);
                colorQuantTable[i * 4 + 2] = (byte)(inBuffer[i + 16] >> 16 & 0xFF);
                colorQuantTable[i * 4 + 3] = (byte)(inBuffer[i + 16] >> 24 & 0xFF);
            }
        }

        private void setScaleTable() {//64 signed halfwords with 14bit fractional part
            for (int i = 0; i < 32; i++) { //writed as 32 words on buffer
                scaleTable[i * 2] = (short)(inBuffer[i] & 0xFFFF);
                scaleTable[i * 2 + 1] = (short)(inBuffer[i] >> 16);
            }
        }

        public void writeMDEC1_Control(uint value) { //1F801824h - MDEC1 - MDEC Control/Reset Register (W)
            uint resetMdec = (value >> 31) & 0x1; //todo actual abort commands and set status to 80040000h
            isDataInRequested = ((value >> 30) & 0x1) == 1; //todo enable dma
            isDataOutRequested = ((value >> 29) & 0x1) == 1;

            //Console.WriteLine("[MDEC] dataInRequest " + isDataInRequested + " dataOutRequested " + isDataOutRequested);
        }

        public uint readMDEC0_Data() { //1F801820h.Read - MDEC Data/Response Register (R)
            //return macroBlockData;
            //Console.WriteLine(ptr);
            return inBuffer[ptr++];
        }

        public uint readMDEC1_Status() {//1F801824h - MDEC1 - MDEC Status Register (R)
            uint status = 0;

            status |= (isDataOutFifoEmpty ? 1u : 0) << 31;
            status |= (isDataInFifoFull ? 1u : 0) << 30;
            status |= (isCommandBusy ? 1u : 0) << 29;
            status |= (isDataInRequested ? 1u : 0) << 28;
            status |= (isDataOutRequested ? 1u : 0) << 27;
            status |= dataOutputDepth << 25;
            status |= (isSigned ? 1u : 0) << 24;
            status |= (isBit15 ? 1u : 0) << 23;
            status |= currentBlock << 16;
            status |= remainingDataWords - 1 == 0xFFFF_FFFF ? 0 : remainingDataWords - 1;
            //Console.WriteLine("[MDEC] Load Status " + status.ToString("x8"));
            return status;
        }

        private static readonly byte[] zigzag = {
              0 ,1 ,5 ,6 ,14,15,27,28,
              2 ,4 ,7 ,13,16,26,29,42,
              3 ,8 ,12,17,25,30,41,43,
              9 ,11,18,24,31,40,44,53,
              10,19,23,32,39,45,52,54,
              20,22,33,38,46,51,55,60,
              21,34,37,47,50,56,59,61,
              35,36,48,49,57,58,62,63
        };

        private static readonly byte[] zagzig = ((byte[])zigzag.Clone());
    }
}
