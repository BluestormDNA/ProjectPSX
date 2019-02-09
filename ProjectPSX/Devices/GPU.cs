using System;
using System.Collections.Generic;

namespace ProjectPSX.Devices {

    public class GPU : Device {
        //private uint GP0;       //1F801810h-Write GP0    Send GP0 Commands/Packets(Rendering and VRAM Access)
        //private uint GP1;       //1F801814h-Write GP1    Send GP1 Commands(Display Control) (and DMA Control)
        //private uint GPUREAD;   //1F801810h-Read GPUREAD Receive responses to GP0(C0h) and GP1(10h) commands
        //private uint GPUSTAT/* = 0x1c00_0000*/;//temp value to force DMA   //1F801814h-Read GPUSTAT Receive GPU Status Register

        //private byte[] VRAM;    //todo

        private Command command;
        private int size;
        private Queue<uint> commandBuffer = new Queue<uint>();

        private enum Mode {
            COMMAND,
            VRAM
        }
        private Mode mode;

        //GP0
        private byte textureXBase;
        private byte textureYBase;
        private byte transparency;
        private byte textureDepth;
        private bool isDithered;
        private bool isDrawingToDisplayAllowed;
        private bool isMasked;
        private bool isMaskedPriority;
        private bool isInterlaceField;
        private bool isReverseFlag;
        private bool isTextureDisabled;
        private byte horizontalResolution;
        private bool isVerticalResolution480;
        private bool isPal;
        private bool is24BitDepth;
        private bool isVerticalInterlace;
        private bool isDisplayDisabled;
        private bool isInterruptRequested;
        private bool isDmaRequest;

        private bool isReadyToReceiveCommand;
        private bool isReadyToSendVRAMToCPU;
        private bool isReadyToReceiveDMABlock;

        private byte dmaDirection;
        private bool isOddLine;

        private bool isTexturedRectangleXFlipped;
        private bool isTexturedRectangleYFlipped;

        private byte textureWindowMaskX;
        private byte textureWindowMaskY;
        private byte textureWindowOffsetX;
        private byte textureWindowOffsetY;

        private ushort drawingAreaLeft;
        private ushort drawingAreaRight;
        private ushort drawingAreaTop;
        private ushort drawingAreaBottom;
        private ushort drawingXOffset;
        private ushort drawingYOffset;

        private ushort displayVRAMXStart;
        private ushort displayVRAMYStart;
        private ushort displayX1;
        private ushort displayX2;
        private ushort displayY1;
        private ushort displayY2;

        public GPU() {
            mem = new byte[1024 * 1024];
            mode = Mode.COMMAND;
            GP1_Reset();
        }

        public uint loadGPUSTAT() {
            uint GPUSTAT = 0;

            GPUSTAT |= textureXBase;
            GPUSTAT |= (uint)textureYBase << 4;
            GPUSTAT |= (uint)transparency << 5;
            GPUSTAT |= (uint)textureDepth << 7;
            GPUSTAT |= (uint)(isDithered ? 1 : 0) << 9;
            GPUSTAT |= (uint)(isDrawingToDisplayAllowed ? 1 : 0) << 10;
            GPUSTAT |= (uint)(isMasked ? 1 : 0) << 11;
            GPUSTAT |= (uint)(isMaskedPriority ? 1 : 0) << 12;
            GPUSTAT |= (uint)(isInterlaceField ? 1 : 0) << 13;
            GPUSTAT |= (uint)(isReverseFlag ? 1 : 0) << 14;
            GPUSTAT |= (uint)(isTextureDisabled ? 1 : 0) << 15;
            GPUSTAT |= (uint)horizontalResolution << 16;
            GPUSTAT |= (uint)/*(isVerticalResolution480 ? 1 : 0)*/0 << 19;
            GPUSTAT |= (uint)(isPal ? 1 : 0) << 20;
            GPUSTAT |= (uint)(is24BitDepth ? 1 : 0) << 21;
            GPUSTAT |= (uint)(isVerticalInterlace ? 1 : 0) << 22;
            GPUSTAT |= (uint)(isDisplayDisabled ? 1 : 0) << 23;
            GPUSTAT |= (uint)(isInterruptRequested ? 1 : 0) << 24;
            GPUSTAT |= (uint)(isDmaRequest ? 1 : 0) << 25;

            GPUSTAT |= (uint)/*(isReadyToReceiveCommand ? 1 : 0)*/1 << 26;
            GPUSTAT |= (uint)/*(isReadyToSendVRAMToCPU ? 1 : 0)*/1 << 27;
            GPUSTAT |= (uint)/*(isReadyToReceiveDMABlock ? 1 : 0)*/1 << 28;

            GPUSTAT |= (uint)dmaDirection << 29;
            GPUSTAT |= (uint)(isOddLine ? 1 : 0) << 31;

            Console.WriteLine("[GPU] LOAD GPUSTAT: {0}", GPUSTAT.ToString("x8"));
            return GPUSTAT;
        }

        public uint loadGPUREAD() {
            //TODO
            Console.WriteLine("[GPU] LOAD GPUREAD: {0}", 0.ToString("x8"));
            return 0;
        }

        public void writeGP0(uint value) {
            Console.WriteLine("[GPU] GP0 WRITE: {0}", value.ToString("x8"));
            //if (something about VRAM) else
            switch (mode) {
                case Mode.COMMAND: ExecuteGP0Command(value); break;
                case Mode.VRAM: WriteToVRAM(value); break;
                default: break;
            }
        }

        private void WriteToVRAM(uint value) {
            Console.WriteLine("Trying to Write to VRAM: Size " + size);
            // copy to VRAM lack position values x y w h to class?
            Console.WriteLine("[GPU] [VRAM] Write " + value.ToString("x8"));
            size--;
            if (size == 0) {
                mode = Mode.COMMAND;
            }
        }

        private void ExecuteGP0Command(uint value) {
            if (size == 0) {
                uint opcode = (value >> 24) & 0xFF;
                (size, command) = decode(opcode);
            }

            commandBuffer.Enqueue(value);

            if (commandBuffer.Count == size) {
                size = 0;
                command();
            }
        }

        private (int, Command) decode(uint opcode) {
            switch (opcode) {
                case 0x00: return (1, GP0_NOP);
                case 0x01: return (1, GP0_MemClearCache);
                case 0x2C: return (9, GP0_RenderTexturedQuadBlend);
                case 0xA0: return (3, GP0_MemCopyRectCPUtoVRAM);
                case 0xC0: return (3, GP0_MemCopyRectVRAMtoCPU);
                case 0xE1: return (1, GP0_SetDrawMode);
                case 0xE2: return (1, GP0_SetTextureWindow);
                case 0xE3: return (1, GP0_SetDrawingAreaTopLeft);
                case 0xE4: return (1, GP0_SetDrawingAreaBottomRight);
                case 0xE5: return (1, GP0_SetDrawingOffset);
                case 0xE6: return (1, GP0_SetMaskBit);
                case 0x28: return (5, GP0_RenderMonoQuadOpaque);
                case 0x30: return (6, GP0_RenderShadedTriOpaque);
                case 0x38: return (8, GP0_RenderShadedQuadOpaque);
                default: return (1, GP0_NOP);
            }
        }

        private void GP0_RenderTexturedQuadBlend() {
            uint val0 = commandBuffer.Dequeue();
            uint val1 = commandBuffer.Dequeue();
            uint val2 = commandBuffer.Dequeue();
            uint val3 = commandBuffer.Dequeue();
            uint val4 = commandBuffer.Dequeue();
            uint val5 = commandBuffer.Dequeue();
            uint val6 = commandBuffer.Dequeue();
            uint val7 = commandBuffer.Dequeue();
            uint val8 = commandBuffer.Dequeue();
        }

        private void GP0_RenderShadedTriOpaque() {
            uint val0 = commandBuffer.Dequeue();
            uint val1 = commandBuffer.Dequeue();
            uint val2 = commandBuffer.Dequeue();
            uint val3 = commandBuffer.Dequeue();
            uint val4 = commandBuffer.Dequeue();
            uint val5 = commandBuffer.Dequeue();
        }

        private void GP0_RenderShadedQuadOpaque() {
            uint val0 = commandBuffer.Dequeue();
            uint val1 = commandBuffer.Dequeue();
            uint val2 = commandBuffer.Dequeue();
            uint val3 = commandBuffer.Dequeue();
            uint val4 = commandBuffer.Dequeue();
            uint val5 = commandBuffer.Dequeue();
            uint val6 = commandBuffer.Dequeue();
            uint val7 = commandBuffer.Dequeue();
        }

        private void GP0_MemCopyRectVRAMtoCPU() {
            uint command = commandBuffer.Dequeue();
            uint yx = commandBuffer.Dequeue();
            uint wh = commandBuffer.Dequeue();

            ushort x = (ushort)(yx & 0xFFFF);
            ushort y = (ushort)(yx >> 16);

            ushort h = (ushort)(wh & 0xFFFF);
            ushort w = (ushort)(wh >> 16);
            //todo
        }

        public delegate void Command();

        private void GP0_MemCopyRectCPUtoVRAM() {
            uint command = commandBuffer.Dequeue();
            uint yx = commandBuffer.Dequeue();
            uint wh = commandBuffer.Dequeue();

            ushort x = (ushort)(yx & 0xFFFF);
            ushort y = (ushort)(yx >> 16);

            ushort h = (ushort)(wh & 0xFFFF);
            ushort w = (ushort)(wh >> 16);

            size = (ushort)(((w * h) + 1) >> 1);

            mode = Mode.VRAM;
            Console.WriteLine("Image Load x y = " + x + " " + y);
            Console.WriteLine("Image Load Size = " + w * h);
            Console.WriteLine("ImageSize Normalized = " + size);
            Console.ReadLine();

            //throw new NotImplementedException();
        }

        private void GP0_MemClearCache() {
            uint val0 = commandBuffer.Dequeue();
            //throw new NotImplementedException();
        }

        private void GP0_RenderMonoQuadOpaque() {
            uint val0 = commandBuffer.Dequeue();
            uint val1 = commandBuffer.Dequeue();
            uint val2 = commandBuffer.Dequeue();
            uint val3 = commandBuffer.Dequeue();
            uint val4 = commandBuffer.Dequeue();
            Console.WriteLine("[GPU] [DRAW] MonoQuadOpaque");
            Console.ReadLine();
        }

        private void GP0_SetTextureWindow() {
            uint val = commandBuffer.Dequeue();

            textureWindowMaskX = (byte)(val & 0x1F);
            textureWindowMaskY = (byte)((val >> 5) & 0x1F);
            textureWindowOffsetX = (byte)((val >> 10) & 0x1F);
            textureWindowOffsetY = (byte)((val >> 15) & 0x1F);
        }

        private void GP0_SetMaskBit() {
            uint val = commandBuffer.Dequeue();

            isMasked = (val & 1) != 0; ;
            isMaskedPriority = (val & 2) != 0;
        }

        private void GP0_SetDrawingOffset() {
            uint val = commandBuffer.Dequeue();

            drawingXOffset = (ushort)(short)(val & 0x7FF);
            drawingYOffset = (ushort)(short)((val >> 11) & 0x7FF);
        }

        private void GP0_NOP() {
            //maybe something about timings?
            commandBuffer.Dequeue();
            Console.WriteLine("[GPU] [GP0] NOP");
            Console.ReadLine();
        }

        private void GP0_SetDrawMode() {
            uint val = commandBuffer.Dequeue();

            textureXBase = (byte)(val & 0xF);
            textureYBase = (byte)((val >> 4) & 0x1);
            transparency = (byte)((val >> 5) & 0x3);
            textureDepth = (byte)((val >> 7) & 0x3);
            isDithered = ((val >> 9) & 1) != 0;
            isDrawingToDisplayAllowed = ((val >> 10) & 1) != 0;
            isTextureDisabled = ((val >> 11) & 1) != 0;
            isTexturedRectangleXFlipped = ((val >> 12) & 1) != 0;
            isTexturedRectangleYFlipped = ((val >> 13) & 1) != 0;

            Console.WriteLine("[GPU] [GP0] DrawMode");
        }

        private void GP0_SetDrawingAreaTopLeft() {
            uint val = commandBuffer.Dequeue();

            drawingAreaTop = (ushort)((val >> 10) & 0x3FF);
            drawingAreaLeft = (ushort)(val & 0x1FF); //todo 0x3FF???
        }

        private void GP0_SetDrawingAreaBottomRight() {
            uint val = commandBuffer.Dequeue();

            drawingAreaBottom = (ushort)((val >> 10) & 0x3FF);
            drawingAreaRight = (ushort)(val & 0x1FF);//todo 0x3FF???
        }

        public void writeGP1(uint value) {
            //TODO
            Console.WriteLine("[GPU] GP1 Write Value: {0}", value.ToString("x8"));
            //Console.ReadLine();
            ExecuteGP1Command(value);
        }

        private void ExecuteGP1Command(uint value) {
            uint opcode = (value >> 24) & 0xFF;
            switch (opcode) {
                case 0x00: GP1_Reset(); break;
                case 0x02: GP1_AckGPUInterrupt(); break;
                case 0x03: GP1_DisplayEnable(value); break;
                case 0x04: GP1_DMADirection(value); break;
                case 0x05: GP1_DisplayVRAMStart(value); break;
                case 0x06: GP1_DisplayHorizontalRange(value); break;
                case 0x07: GP1_DisplayVerticalRange(value); break;
                case 0x08: GP1_DisplayMode(value); break;
            }
        }

        private void GP1_AckGPUInterrupt() {
            isInterruptRequested = false;
        }

        private void GP1_DisplayEnable(uint value) {
            isDisplayDisabled = (value & 1) != 0;
        }

        private void GP1_DisplayVerticalRange(uint value) {
            displayY1 = (ushort)(value & 0x3FF);
            displayY2 = (ushort)((value >> 10) & 0x3FF);
        }

        private void GP1_DisplayHorizontalRange(uint value) {
            displayX1 = (ushort)(value & 0xFFF);
            displayX2 = (ushort)((value >> 12) & 0xFFF);
        }

        private void GP1_DisplayVRAMStart(uint value) {
            displayVRAMXStart = (ushort)(value & 0x3FE);
            displayVRAMYStart = (ushort)((value >> 10) & 0x1FE);
        }

        private void GP1_DMADirection(uint value) {
            dmaDirection = (byte)(value & 0x3);
        }

        private void GP1_DisplayMode(uint value) {
            uint horizontalRes1 = value & 0x3;
            uint horizontalRes2 = value & 0x40;

            horizontalResolution = (byte)(horizontalRes2 << 2 | horizontalRes1);
            isVerticalResolution480 = (value & 0x4) != 0;
            isPal = (value & 0x8) != 0;
            is24BitDepth = (value & 0x10) != 0;
            isVerticalInterlace = (value & 0x20) != 0;
            isReverseFlag = (value & 0x80) != 0;
        }

        private void GP1_Reset() {
            textureXBase = 0;
            textureYBase = 0;
            transparency = 0;
            textureDepth = 0;

            isDithered = false;
            isDrawingToDisplayAllowed = false;
            isTextureDisabled = false;

            isTexturedRectangleXFlipped = false;
            isTexturedRectangleYFlipped = false;

            isMasked = false;
            isMaskedPriority = false;
            dmaDirection = 0;
            isDisplayDisabled = true;
            horizontalResolution = 0;
            isVerticalResolution480 = false;
            isPal = false;
            isVerticalInterlace = false; //?
            is24BitDepth = false;
            isInterruptRequested = false;
            isInterlaceField = true;

            textureWindowMaskX = 0;
            textureWindowMaskY = 0;
            textureWindowOffsetX = 0;
            textureWindowOffsetY = 0;

            drawingAreaLeft = 0;
            drawingAreaRight = 0;
            drawingAreaTop = 0;
            drawingAreaBottom = 0;
            drawingXOffset = 0;
            drawingYOffset = 0;

            displayVRAMXStart = 0;
            displayVRAMYStart = 0;
            displayX1 = 0x200;
            displayX2 = 0xc00;
            displayY1 = 0x10;
            displayY2 = 0x100;
        }
    }
}