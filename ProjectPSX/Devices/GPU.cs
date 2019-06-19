using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProjectPSX.Devices {

    public class GPU {

        private uint GPUREAD;     //1F801810h-Read GPUREAD Receive responses to GP0(C0h) and GP1(10h) commands

        private int area;

        private uint command;
        private int commandSize;
        //private Queue<uint> commandBuffer = new Queue<uint>(16);
        private uint[] commandBuffer = new uint[32];
        private uint[] emptyBuffer = new uint[32]; //fallback to rewrite
        private int pointer;

        private const int CyclesPerFrame = 564480;

        private Window window;
        private DirectBitmap VRAM = new DirectBitmap();

        private int[] clut = new int[256];

        public void setWindow(Window window) {
            this.window = window;
        }
        private enum Mode {
            COMMAND,
            VRAM
        }
        private Mode mode;

        private enum Type {
            opaque,
            shaded,
            textured
        }
        private Type type;

        private struct VRAM_Coord {
            public int x, y;
            public int origin_x;
            public ushort w, h;
            public int size;
        }
        private VRAM_Coord vram_coord;


        [StructLayout(LayoutKind.Explicit)]
        private struct Point2D {
            [FieldOffset(0)] public uint val;
            [FieldOffset(0)] public short x;
            [FieldOffset(2)] public short y;
        }

        private Point2D[] v = new Point2D[4];
        Point2D min = new Point2D();
        Point2D max = new Point2D();

        private struct TextureData {
            public int x, y;

            public TextureData(uint val) {
                x = (int)(val & 0xFF);
                y = (int)((val >> 8) & 0xFF);
            }

            public void setData(uint val) {
                x = (int)(val & 0xFF);
                y = (int)((val >> 8) & 0xFF);
            }

            public TextureData(int x, int y) {
                this.x = x;
                this.y = y;
            }
        }
        private TextureData[] t = new TextureData[4];

        [StructLayout(LayoutKind.Explicit)]
        private struct Color {
            [FieldOffset(0)] public uint val;
            [FieldOffset(0)] public byte r;
            [FieldOffset(1)] public byte g;
            [FieldOffset(2)] public byte b;
            [FieldOffset(3)] public byte m;
        }

        private Color c0;
        private Color c1;
        private Color c2;
        private Color c3;

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
        private short drawingXOffset;
        private short drawingYOffset;

        private ushort displayVRAMXStart;
        private ushort displayVRAMYStart;
        private ushort displayX1;
        private ushort displayX2;
        private ushort displayY1;
        private ushort displayY2;

        private int timer;

        public GPU() {
            mode = Mode.COMMAND;
            GP1_ResetGPU();
        }

        public bool tick(int cycles) {
            timer += cycles;
            if (timer >= CyclesPerFrame) { 
                //Console.WriteLine("[GPU] Request Interrupt 0x1 VBLANK");
                timer -= CyclesPerFrame; //1128960 ff7
                window.update(VRAM.Bits);
                return true;
            }
            return false;
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

            //Console.WriteLine("[GPU] LOAD GPUSTAT: {0}", GPUSTAT.ToString("x8"));
            return GPUSTAT;
        }

        public uint loadGPUREAD() {
            //TODO check if correct and refact
            uint value;
            if (vram_coord.size > 0) {
                value = readFromVRAM();
            } else {
                value = GPUREAD;
            }
            //Console.WriteLine("[GPU] LOAD GPUREAD: {0}", value.ToString("x8"));
            return value;
        }
        //All this should be cleaner if delegate function calls werent so slow :\
        public void writeGP0(uint value) {
            //Console.WriteLine("Direct " + value.ToString("x8"));
            //Console.WriteLine(mode);
            switch (mode) {
                case Mode.COMMAND: DecodeGP0Command(value); break;
                case Mode.VRAM: WriteToVRAM(value); break;
            }
        }

        internal void writeGP0(uint[] buffer) {
            //Console.WriteLine("buffer");
            //Console.WriteLine(mode);
            switch (mode) {
                case Mode.COMMAND: DecodeGP0Command(buffer); break;
                case Mode.VRAM:
                    for (int i = 0; i < buffer.Length; i++) {
                        //Console.WriteLine(i + " " + buffer[i].ToString("x8"));
                        WriteToVRAM(buffer[i]);
                    }
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteToVRAM(uint value) { //todo rewrite this mess
            ushort pixel1 = (ushort)(value >> 16);
            ushort pixel0 = (ushort)(value & 0xFFFF);

            drawVRAMPixel(pixel0);
            drawVRAMPixel(pixel1);

            vram_coord.size--;
            if (vram_coord.size == 0) {
                mode = Mode.COMMAND;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint readFromVRAM() {
            ushort pixel0 = VRAM.GetPixel16(vram_coord.x++ & 0x3FF, vram_coord.y & 0x1FF);
            ushort pixel1 = VRAM.GetPixel16(vram_coord.x++ & 0x3FF, vram_coord.y & 0x1FF);
            if (vram_coord.x == vram_coord.origin_x + vram_coord.w) {
                vram_coord.x -= vram_coord.w;
                vram_coord.y++;
            }
            vram_coord.size -= 2;
            return (uint)(pixel1 << 16 | pixel0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void drawVRAMPixel(ushort val) {
            VRAM.SetPixel(vram_coord.x++ & 0x3FF, vram_coord.y & 0x1FF, get555Color(val));
            if (vram_coord.x == vram_coord.origin_x + vram_coord.w) {
                vram_coord.x -= vram_coord.w;
                vram_coord.y++;
            }
        }

        //This needs to go away once a BGR bitmap is achieved
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int get555Color(ushort val) {
            byte m = (byte)(val >> 15);
            byte r = (byte)((val & 0x1F) << 3);
            byte g = (byte)(((val >> 5) & 0x1F) << 3);
            byte b = (byte)(((val >> 10) & 0x1F) << 3);

            return (m << 24 | r << 16 | g << 8 | b);
        }

        private void DecodeGP0Command(uint value) {
            if (pointer == 0) {
                command = value >> 24;
                commandSize = CommandSize[command];
                //Console.WriteLine("[GPU] Direct GP0 COMMAND: {0} size: {1}", value.ToString("x8"), commandSize);
            }

            commandBuffer[pointer++] = value;
            //Console.WriteLine("[GPU] Direct GP0: {0} buffer: {1}", value.ToString("x8"), pointer);

            if (pointer == commandSize || commandSize == 32 && value == 0x5555_5555) {
                pointer = 0;
                //Console.WriteLine("EXECUTING");
                ExecuteGP0(command);
                pointer = 0;
            }
        }
        private void DecodeGP0Command(uint[] buffer) {
            commandBuffer = buffer;

            //Console.WriteLine(commandBuffer.Length);

            while (pointer < buffer.Length) {
                command = commandBuffer[pointer] >> 24;
                //Console.WriteLine("Buffer Executing " + command.ToString("x2") + " pointer " + pointer);
                ExecuteGP0(command);
            }
            pointer = 0;

            commandBuffer = emptyBuffer;
            //Console.WriteLine("fin");
        }


        private void ExecuteGP0(uint opcode) {
            //Console.WriteLine("GP0 Command: " + opcode.ToString("x2"));
            switch (opcode) {
                case 0x00: GP0_NOP(); break;
                case 0x01: GP0_MemClearCache(); break;
                case 0x02: GP0_FillRectVRAM(); break;
                case 0x1F: GP0_InterruptRequest(); break;

                case 0xE1: GP0_SetDrawMode(); break;
                case 0xE2: GP0_SetTextureWindow(); break;
                case 0xE3: GP0_SetDrawingAreaTopLeft(); break;
                case 0xE4: GP0_SetDrawingAreaBottomRight(); break;
                case 0xE5: GP0_SetDrawingOffset(); break;
                case 0xE6: GP0_SetMaskBit(); break;

                case uint polygon when opcode >= 0x20 && opcode <= 0x3F:
                    GP0_RenderPolygon(); break;
                case uint line when opcode >= 0x40 && opcode <= 0x5F:
                    GP0_RenderLine(); break;
                case uint rect when opcode >= 0x60 && opcode <= 0x7F:
                    GP0_RenderRectangle(); break;
                case uint vramToVram when opcode >= 0x80 && opcode <= 0x9F:
                    GP0_MemCopyRectVRAMtoVRAM(); break;
                case uint cpuToVram when opcode >= 0xA0 && opcode <= 0xBF:
                    GP0_MemCopyRectCPUtoVRAM(); break;
                case uint vramToCpu when opcode >= 0xC0 && opcode <= 0xDF:
                    GP0_MemCopyRectVRAMtoCPU(); break;

                case uint nop when (opcode >= 0x3 && opcode <= 0x1E) || opcode == 0xE0 || opcode >= 0xE7 && opcode <= 0xEF:
                    GP0_NOP(); break;

                default: Console.WriteLine("[GPU] Unsupported GP0 Command " + opcode.ToString("x8")); Console.ReadLine(); GP0_NOP(); break;// throw new NotImplementedException();
            }
        }

        private void GP0_InterruptRequest() {
            pointer++;
            isInterruptRequested = true;
        }

        private void GP0_RenderLine() {
            //Console.WriteLine("size " + commandBuffer.Count);
            uint command = commandBuffer[pointer++];
            uint color1 = command & 0xFFFFFF;
            uint color2 = color1;

            bool isPoly = (command & (1 << 27)) != 0;
            bool isShaded = (command & (1 << 28)) != 0;
            bool isTransparent = (command & (1 << 25)) != 0;

            uint v1 = commandBuffer[pointer++];

            if (isShaded) color2 = commandBuffer[pointer++];
            uint v2 = commandBuffer[pointer++];

            rasterizeLine(v1, v2, color1, color2);

            if (!isPoly) return;

            while (commandBuffer[pointer] != 0x5555_5555) {
                //Console.WriteLine("DOING ANOTHER LINE");
                color1 = color2;
                if (isShaded) color2 = commandBuffer[pointer++];
                v1 = v2;
                v2 = commandBuffer[pointer++];
                rasterizeLine(v1, v2, color1, color2);
            }

            pointer++; // discard 5555_5555 termination (need to rewrite all this from the GP0...)
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void rasterizeLine(uint v1, uint v2, uint color1, uint color2) {
            short x = (short)((v1 & 0xFFFF) & 0x3FF);
            short y = (short)((v1 >> 16) & 0x1FF);

            short x2 = (short)((v2 & 0xFFFF) & 0x3FF);
            short y2 = (short)((v2 >> 16) & 0x1FF);

            x += drawingXOffset;
            y += drawingYOffset;

            x2 += drawingXOffset;
            y2 += drawingYOffset;

            //Console.WriteLine("x y : " + x + " " + y);
            //Console.WriteLine("x2 y2 : " + x2 + " " + y2);

            int w = x2 - x;
            int h = y2 - y;
            int dx1 = 0, dy1 = 0, dx2 = 0, dy2 = 0;

            if (w < 0) dx1 = -1; else if (w > 0) dx1 = 1;
            if (h < 0) dy1 = -1; else if (h > 0) dy1 = 1;
            if (w < 0) dx2 = -1; else if (w > 0) dx2 = 1;

            int longest = Math.Abs(w);
            int shortest = Math.Abs(h);

            if (!(longest > shortest)) {
                longest = Math.Abs(h);
                shortest = Math.Abs(w);
                if (h < 0) dy2 = -1; else if (h > 0) dy2 = 1;
                dx2 = 0;
            }

            int numerator = longest >> 1;

            for (int i = 0; i <= longest; i++) {
                float ratio = (float)i / longest;
                int color = interpolate(color1, color2, ratio);

                if (x >= drawingAreaLeft && x < drawingAreaRight && y >= drawingAreaTop && y < drawingAreaBottom) //why boundingbox dosnt work???
                    VRAM.SetPixel(x, y, color);

                numerator += shortest;
                if (!(numerator < longest)) {
                    numerator -= longest;
                    x += (short)dx1;
                    y += (short)dy1;
                } else {
                    x += (short)dx2;
                    y += (short)dy2;
                }
            }
            //Console.ReadLine();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int interpolate(uint color1, uint color2, float ratio) {
            c1.val = color1;
            c2.val = color2;

            byte r = (byte)(c2.r * ratio + c1.r * (1 - ratio));
            byte g = (byte)(c2.g * ratio + c1.g * (1 - ratio));
            byte b = (byte)(c2.b * ratio + c1.b * (1 - ratio));

            return (r << 16 | g << 8 | b);
        }

        public void GP0_RenderPolygon() {
            uint command = commandBuffer[pointer];
            //Console.WriteLine(command.ToString("x8") +  " "  + commandBuffer.Length + " " + pointer);

            bool isQuad = (command & (1 << 27)) != 0;
            bool isShaded = (command & (1 << 28)) != 0;
            bool isTextured = (command & (1 << 26)) != 0;
            bool isTransparent = (command & (1 << 25)) != 0; //todo unhandled still!

            type = isShaded ? Type.shaded : Type.opaque;
            if (isTextured) type = Type.textured;

            int vertexN = isQuad ? 4 : 3;

            //Point2D[] v = new Point2D[vertexN];
            //TextureData[] t = new TextureData[vertexN];
            uint[] c = new uint[vertexN];

            if (!isShaded) {
                uint color = commandBuffer[pointer++];
                c[0] = color; //triangle 1 opaque color
                c[1] = color; //triangle 2 opaque color
            }

            uint palette = 0;
            uint texpage = 0;

            for (int i = 0; i < vertexN; i++) {
                if (isShaded) c[i] = commandBuffer[pointer++];

                v[i].val = commandBuffer[pointer++];
                v[i].x += drawingXOffset;
                v[i].y += drawingYOffset;

                if (isTextured) {
                    uint textureData = commandBuffer[pointer++];
                    t[i].setData(textureData);
                    if (i == 0) {
                        palette = textureData >> 16 & 0xFFFF;
                    } else if (i == 1) {
                        texpage = textureData >> 16 & 0xFFFF;
                    }
                }
            }

            rasterizeTri(v[0], v[1], v[2], t[0], t[1], t[2], c[0], c[1], c[2], palette, texpage, type);
            if (isQuad) rasterizeTri(v[1], v[2], v[3], t[1], t[2], t[3], c[1], c[2], c[3], palette, texpage, type);
        }

        //Mother of parameters. this should be better when c#8 ranges come into play. I could declare 2 new arrays segments but i dont like them
        //or 4 new arrays  but i think that tests implied slower performance
        //or not pass asnything and use fields but then how to handle quad...
        //Atm passing structs as parameters is pretty lol..
        //well anyway not an "issue" right now. (priority low)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void rasterizeTri(Point2D v0, Point2D v1, Point2D v2, TextureData t0, TextureData t1, TextureData t2, uint c0, uint c1, uint c2, uint palette, uint texpage, Type type) {

            area = orient2d(v0, v1, v2);

            if (area == 0) {
                return;
            }

            if (area < 0) {
                Point2D vertexAux = v1;
                v1 = v2;
                v2 = vertexAux;
                TextureData textureAux = t1;
                t1 = t2;
                t2 = textureAux;
                uint colorAux = c1;
                c1 = c2;
                c2 = colorAux;
            }

            /*(Point2D min, Point2D max) = */boundingBox(v0, v1, v2);

            int A01 = v0.y - v1.y, B01 = v1.x - v0.x;
            int A12 = v1.y - v2.y, B12 = v2.x - v1.x;
            int A20 = v2.y - v0.y, B20 = v0.x - v2.x;

            int w0_row = orient2d(v1, v2, min);
            int w1_row = orient2d(v2, v0, min);
            int w2_row = orient2d(v0, v1, min);

            //TEST
            area = w0_row + w1_row + w2_row;
            int depth = (int)(texpage >> 7) & 0x3;
            int clutX = (int)(palette & 0x3f) << 4;
            int clutY = (int)(palette >> 6) & 0x1FF;

            int XBase = (int)(texpage & 0xF) << 6;
            int YBase = (int)((texpage >> 4) & 0x1) << 8;

            int col = GetRgbColor(c0);
            loadClut(clutX, clutY, depth);
            //TESTING END


            // Rasterize
            for (int y = min.y; y < max.y; y++) {
                // Barycentric coordinates at start of row
                int w0 = w0_row;
                int w1 = w1_row;
                int w2 = w2_row;

                for (int x = min.x; x < max.x; x++) {
                    // If p is on or inside all edges, render pixel.
                    if ((w0 | w1 | w2) >= 0) {

                        switch (type) {
                            case Type.opaque:
                                //col = GetRgbColor(c0); //this is overkill here as its the same but putting it outside slows the important ones
                                VRAM.SetPixel((x & 0x3FF), (y & 0x1FF), col);
                                break;
                            case Type.shaded:
                                col = getShadedColor(w0, w1, w2, c0, c1, c2);
                                VRAM.SetPixel((x & 0x3FF), (y & 0x1FF), col);
                                break;
                            case Type.textured:
                                col = getTextureColor(w0, w1, w2, t0, t1, t2, clutX, clutY, XBase, YBase, depth, clut);
                                if (col != 0)
                                    VRAM.SetPixel((x & 0x3FF), (y & 0x1FF), col);
                                break;
                        }
                    }
                    // One step to the right
                    w0 += A12;
                    w1 += A20;
                    w2 += A01;
                }
                // One row step
                w0_row += B12;
                w1_row += B20;
                w2_row += B01;
            }
        }

        private void loadClut(int clutX, int clutY, int depth) {
            if (depth == 0) {
                for (int i = 0; i < clut.Length; i++) {
                    clut[i] = VRAM.GetPixel(clutX+i, clutY);
                }
            } else if (depth == 1) {
                for (int i = 0; i < clut.Length; i++) {
                    clut[i] = VRAM.GetPixel(clutX + i, clutY);
                }
            }
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetRgbColor(uint value) {
            c0.val = value;
            return (c0.m << 24 | c0.r << 16 | c0.g << 8 | c0.b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GP0_FillRectVRAM() {
            c0.val = commandBuffer[pointer++];
            v[0].val = commandBuffer[pointer++];
            v[1].val = commandBuffer[pointer++];

            int color = (c0.r << 16 | c0.g << 8 | c0.b);

            for (int yPos = v[0].y; yPos < v[1].y + v[0].y; yPos++) {
                for (int xPos = v[0].x; xPos < v[1].x + v[0].x; xPos++) {
                    VRAM.SetPixel(xPos & 0x3FF, yPos & 0x1FF, color);
                }
            }
        }

        private void GP0_RenderRectangle() {
            //1st Color+Command(CcBbGgRrh)
            //2nd Vertex(YyyyXxxxh)
            //3rd Texcoord+Palette(ClutYyXxh)(for 4bpp Textures Xxh must be even!) //Only textured
            //4rd (3rd non textured) Width + Height(YsizXsizh)(variable opcode only)(max 1023x511)
            uint command = commandBuffer[pointer++];
            uint color = command & 0xFFFFFF;
            uint opcode = (command >> 24) & 0xFF;

            bool isShaded = (command & (1 << 28)) != 0;
            bool isTextured = (command & (1 << 26)) != 0;
            bool isTransparent = (command & (1 << 25)) != 0; //todo unhandled still!

            type = Type.opaque;
            if (isTextured) type = Type.textured;

            uint vertex = commandBuffer[pointer++];
            short xo = (short)(vertex & 0xFFFF);
            short yo = (short)((vertex >> 16) & 0xFFFF);

            uint[] c = new uint[4];
            c[0] = color;
            c[1] = color;

            ushort palette = 0;
            short textureX = 0;
            short textureY = 0;
            if (isTextured) {
                uint texture = commandBuffer[pointer++];
                palette = (ushort)((texture >> 16) & 0xFFFF);
                textureX = (short)(texture & 0xFF);
                textureY = (short)((texture >> 8) & 0xFF);
            }

            short width = 0;
            short heigth = 0;

            switch ((opcode & 0x18) >> 3) {
                case 0x0:
                    uint hw = commandBuffer[pointer++];
                    width = (short)(hw & 0xFFFF);
                    heigth = (short)((hw >> 16) & 0xFFFF);
                    break;
                case 0x1:
                    width = 1; heigth = 1;
                    break;
                case 0x2:
                    width = 8; heigth = 8;
                    break;
                case 0x3:
                    width = 16; heigth = 16;
                    break;
                default:
                    Console.WriteLine("INCORRECT LENGTH");
                    break;
            }

            int y = yo + drawingYOffset;
            int x = xo + drawingXOffset;

            v[0].x = (short)x;
            v[0].y = (short)y;

            v[1].x = (short)(x + width);
            v[1].y = (short)y;

            v[2].x = (short)x;
            v[2].y = (short)(y + heigth);

            v[3].x = (short)(x + width);
            v[3].y = (short)(y + heigth);


            t[0].x = textureX; t[0].y = textureY;
            t[1].x = textureX + width; t[1].y = textureY;
            t[2].x = textureX; t[2].y = textureY + heigth;
            t[3].x = textureX + width; t[3].y = textureY + heigth;

            uint texpage = getTexpageFromGPU();

            rasterizeTri(v[0], v[1], v[2], t[0], t[1], t[2], c[0], c[1], c[2], palette, texpage, type);
            rasterizeTri(v[1], v[2], v[3], t[1], t[2], t[3], c[1], c[2], c[3], palette, texpage, type);

            //Console.WriteLine("after offset x" + x + " y" + y);
            //Console.ReadLine();
        }

        private void GP0_MemCopyRectVRAMtoCPU() {
            pointer++; //Command/Color parameter unused
            uint yx = commandBuffer[pointer++];
            uint wh = commandBuffer[pointer++];

            ushort x = (ushort)(yx & 0xFFFF);
            ushort y = (ushort)(yx >> 16);

            ushort w = (ushort)(wh & 0xFFFF);
            ushort h = (ushort)(wh >> 16);

            vram_coord.x = x;
            vram_coord.origin_x = x;
            vram_coord.y = y;
            vram_coord.w = w;
            vram_coord.h = h;
            vram_coord.size = h * w;
        }

        public delegate void Command();

        private void GP0_MemCopyRectCPUtoVRAM() { //todo rewrite VRAM coord struct mess
            pointer++; //Command/Color parameter unused
            uint yx = commandBuffer[pointer++];
            uint wh = commandBuffer[pointer++];

            ushort x = (ushort)(yx & 0xFFFF);
            ushort y = (ushort)(yx >> 16);

            ushort w = (ushort)(wh & 0xFFFF);
            ushort h = (ushort)(wh >> 16);

            vram_coord.x = x;
            vram_coord.origin_x = x;
            vram_coord.y = y;
            vram_coord.w = w;
            vram_coord.h = h;
            vram_coord.size = ((h * w) + 1) >> 1;

            mode = Mode.VRAM;
        }

        private void GP0_MemCopyRectVRAMtoVRAM() {
            pointer++; //Command/Color parameter unused
            uint sourceXY = commandBuffer[pointer++];
            uint destinationXY = commandBuffer[pointer++];
            uint wh = commandBuffer[pointer++];

            short sx = (short)(sourceXY & 0xFFFF);
            short sy = (short)(sourceXY >> 16);

            short dx = (short)(destinationXY & 0xFFFF);
            short dy = (short)(destinationXY >> 16);

            short w = (short)(wh & 0xFFFF);
            short h = (short)(wh >> 16);

            for (int yPos = 0; yPos < h; yPos++) {
                for (int xPos = 0; xPos < w; xPos++) {
                    int color = VRAM.GetPixel((sx + xPos) & 0x3FF, (sy + yPos) & 0x1FF);
                    VRAM.SetPixel((dx + xPos) & 0x3FF, (dy + yPos) & 0x1FF, color);
                }
            }
        }


        private void GP0_MemClearCache() {
            pointer = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int getShadedColor(int w0, int w1, int w2, uint color0, uint color1, uint color2) {
            //https://codeplea.com/triangular-interpolation
            //int w = w0 + w1 + w2;
            c0.val = color0;
            c1.val = color1;
            c2.val = color2;

            int r = (c0.r * w0 + c1.r * w1 + c2.r * w2) / area;
            int g = (c0.g * w0 + c1.g * w1 + c2.g * w2) / area;
            int b = (c0.b * w0 + c1.b * w1 + c2.b * w2) / area;

            return (r << 16 | g << 8 | b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int getTextureColor(int w0, int w1, int w2, TextureData t0, TextureData t1, TextureData t2, int clutX, int clutY, int XBase, int YBase, int depth, int[] clut) {
            switch (depth) {
                case 0: return get4bppTexel(w0, w1, w2, t0, t1, t2, clutX, clutY, XBase, YBase, clut);
                case 1: return get8bppTexel(w0, w1, w2, t0, t1, t2, clutX, clutY, XBase, YBase);
                case 2: return get16bppTexel(w0, w1, w2, t0, t1, t2, clutX, clutY, XBase, YBase);
                default: Console.WriteLine("CLUT ERROR WAS " + textureDepth); Console.ReadLine(); return 0x00FF00FF;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int get8bppTexel(int w0, int w1, int w2, TextureData t0, TextureData t1, TextureData t2, int clutX, int clutY, int XBase, int YBase) {
            //https://codeplea.com/triangular-interpolation
            //int w = w0 + w1 + w2;
            int x = (t0.x * w0 + t1.x * w1 + t2.x * w2) / area;
            int y = (t0.y * w0 + t1.y * w1 + t2.y * w2) / area;

            x &= 255;
            y &= 255;

            // Texture masking
            // texel = (texel AND(NOT(Mask * 8))) OR((Offset AND Mask) * 8)
            x = (x & ~(textureWindowMaskX * 8)) | ((textureWindowOffsetX & textureWindowMaskX) * 8);
            y = (y & ~(textureWindowMaskY * 8)) | ((textureWindowOffsetY & textureWindowMaskY) * 8);

            //window.VRAM.SetPixel(x / 2 + XBase, y + YBase, 0x000000FF);
            ushort index = VRAM.GetPixel16(x / 2 + XBase, y + YBase);

            int p = 0;
            switch (x & 1) { //way faster than x % 2
                case 0: p = index & 0xFF; break;
                case 1: p = index >> 8; break;
            }

            return VRAM.GetPixel(clutX + p, clutY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int get16bppTexel(int w0, int w1, int w2, TextureData t0, TextureData t1, TextureData t2, int clutX, int clutY, int XBase, int YBase) {
            //int w = w0 + w1 + w2;
            int x = (t0.x * w0 + t1.x * w1 + t2.x * w2) / area;
            int y = (t0.y * w0 + t1.y * w1 + t2.y * w2) / area;

            return VRAM.GetPixel(x + XBase, y + YBase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int get4bppTexel(int w0, int w1, int w2, TextureData t0, TextureData t1, TextureData t2, int clutX, int clutY, int XBase, int YBase, int[] clut) {
            //int w = w0 + w1 + w2;
            int x = (t0.x * w0 + t1.x * w1 + t2.x * w2) / area;
            int y = (t0.y * w0 + t1.y * w1 + t2.y * w2) / area;
            //Console.WriteLine(x + " " + y);

            x &= 255;
            y &= 255;

            // Texture masking
            // texel = (texel AND(NOT(Mask * 8))) OR((Offset AND Mask) * 8)
            x = (x & ~(textureWindowMaskX * 8)) | ((textureWindowOffsetX & textureWindowMaskX) * 8);
            y = (y & ~(textureWindowMaskY * 8)) | ((textureWindowOffsetY & textureWindowMaskY) * 8);

            //window.VRAM.SetPixel(x / 4 + XBase, y + YBase, 0x000000FF);
            ushort index = VRAM.GetPixel16(x / 4 + XBase, y + YBase);

            int p = 0;
            switch (x & 3) { //way faster than x % 4
                case 0: p = index & 0xF; break;
                case 2: p = index >> 8 & 0xF; break;
                case 1: p = index >> 4 & 0xF; break;
                case 3: p = index >> 12; break;
            }

            return clut[p]; //VRAM.GetPixel(clutX + p, clutY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int orient2d(Point2D a, Point2D b, Point2D c) {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        private /*(Point2D min, Point2D max)*/ void boundingBox(Point2D p0, Point2D p1, Point2D p2) {



            int minX = Math.Min(p0.x, Math.Min(p1.x, p2.x));
            int minY = Math.Min(p0.y, Math.Min(p1.y, p2.y));
            int maxX = Math.Max(p0.x, Math.Max(p1.x, p2.x));
            int maxY = Math.Max(p0.y, Math.Max(p1.y, p2.y));

            min.x = (short)Math.Max(minX, drawingAreaLeft);
            min.y = (short)Math.Max(minY, drawingAreaTop);
            max.x = (short)Math.Min(maxX, drawingAreaRight);
            max.y = (short)Math.Min(maxY, drawingAreaBottom);

            //return (min, max);
        }

        private void GP0_SetTextureWindow() {
            uint val = commandBuffer[pointer++];

            textureWindowMaskX = (byte)(val & 0x1F);
            textureWindowMaskY = (byte)((val >> 5) & 0x1F);
            textureWindowOffsetX = (byte)((val >> 10) & 0x1F);
            textureWindowOffsetY = (byte)((val >> 15) & 0x1F);
        }

        private void GP0_SetMaskBit() {
            uint val = commandBuffer[pointer++];

            isMasked = (val & 1) != 0; ;
            isMaskedPriority = (val & 2) != 0;
        }

        private void GP0_SetDrawingOffset() {
            uint val = commandBuffer[pointer++];

            drawingXOffset = (short)(val & 0x7FF);
            drawingYOffset = (short)((val >> 11) & 0x7FF);
            //drawingXOffset = 256;
            //drawingYOffset = 128;
            //drawingXOffset = 0;
            //drawingYOffset = 0;
        }

        private void GP0_NOP() {
            pointer++;
        }

        private void GP0_SetDrawMode() {
            uint val = commandBuffer[pointer++];

            textureXBase = (byte)(val & 0xF);
            textureYBase = (byte)((val >> 4) & 0x1);
            transparency = (byte)((val >> 5) & 0x3);
            textureDepth = (byte)((val >> 7) & 0x3);
            isDithered = ((val >> 9) & 0x1) != 0;
            isDrawingToDisplayAllowed = ((val >> 10) & 0x1) != 0;
            isTextureDisabled = ((val >> 11) & 0x1) != 0;
            isTexturedRectangleXFlipped = ((val >> 12) & 0x1) != 0;
            isTexturedRectangleYFlipped = ((val >> 13) & 0x1) != 0;

            //Console.WriteLine("[GPU] [GP0] DrawMode ");
        }

        private void GP0_SetDrawingAreaTopLeft() {
            uint val = commandBuffer[pointer++];

            drawingAreaTop = (ushort)((val >> 10) & 0x1FF);
            drawingAreaLeft = (ushort)(val & 0x3FF);
        }

        private void GP0_SetDrawingAreaBottomRight() {
            uint val = commandBuffer[pointer++];

            drawingAreaBottom = (ushort)((val >> 10) & 0x1FF);
            drawingAreaRight = (ushort)(val & 0x3FF);
        }

        public void writeGP1(uint value) {
            //Console.WriteLine("[GPU] GP1 Write Value: {0}", value.ToString("x8"));
            ////Console.ReadLine();
            ExecuteGP1Command(value);
        }

        private void ExecuteGP1Command(uint value) {
            uint opcode = value >> 24;
            switch (opcode) {
                case 0x00: GP1_ResetGPU(); break;
                case 0x01: GP1_ResetCommandBuffer(); break;
                case 0x02: GP1_AckGPUInterrupt(); break;
                case 0x03: GP1_DisplayEnable(value); break;
                case 0x04: GP1_DMADirection(value); break;
                case 0x05: GP1_DisplayVRAMStart(value); break;
                case 0x06: GP1_DisplayHorizontalRange(value); break;
                case 0x07: GP1_DisplayVerticalRange(value); break;
                case 0x08: GP1_DisplayMode(value); break;
                case uint _ when opcode >= 0x10 && opcode <= 0x1F:
                    GP1_GPUInfo(value); break;
                default: Console.WriteLine("[GPU] Unsupported GP1 Command " + opcode.ToString("x8")); Console.ReadLine(); break;
            }
        }

        private void GP1_GPUInfo(uint value) {
            uint info = value & 0xFFFFFF;
            switch (info) {
                case 0x2: GPUREAD = (uint)(textureWindowOffsetY << 15 | textureWindowOffsetX << 10 | textureWindowMaskY << 5 | textureWindowMaskX); break;
                case 0x3: GPUREAD = (uint)(drawingAreaTop << 10 | drawingAreaLeft); break;
                case 0x4: GPUREAD = (uint)(drawingAreaBottom << 10 | drawingAreaRight); break;
                case 0x5: GPUREAD = (uint)(drawingYOffset << 11 | drawingXOffset); break;
                default: GPUREAD = 0; Console.WriteLine("[GPU] GP1 Unhandled GetInfo: " + info.ToString("x8")); break;
            }
        }

        private void GP1_ResetCommandBuffer() {
            pointer = 0;
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

        private void GP1_ResetGPU() {
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

        private uint getTexpageFromGPU() {
            uint texpage = 0;

            texpage |= (isTexturedRectangleYFlipped ? 1u : 0u) << 13;
            texpage |= (isTexturedRectangleXFlipped ? 1u : 0u) << 12;
            texpage |= (isTextureDisabled ? 1u : 0u) << 11;
            texpage |= (isDrawingToDisplayAllowed ? 1u : 0u) << 10;
            texpage |= (isDithered ? 1u : 0u) << 9;
            texpage |= (uint)(textureDepth << 7);
            texpage |= (uint)(transparency << 5);
            texpage |= (uint)(textureYBase << 4);
            texpage |= textureXBase;

            return texpage;
        }

        //This is only needed for the Direct GP0 commands as the command number needs to be
        //known ahead of the first command on queue.
        //GP0 DMA buffer write already doesn't use this.
        //TODO: Rework the direct GP0 Write as if they were GP0 DMA and execute the buffer.
        //Maybe fake a 16/32 buffer and draw then. Maybe is faster this?
        private readonly int[] CommandSize = {
        //0  1   2   3   4   5   6   7   8   9   A   B   C   D   E   F
         1,  1,  3,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, //0
         1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, //1
         4,  4,  4,  4,  7,  7,  7,  7,  5,  5,  5,  5,  9,  9,  9,  9, //2
         6,  6,  6,  6,  9,  9,  9,  9,  8,  8,  8,  8, 12, 12, 12, 12, //3
         3,  3,  3,  3,  3,  3,  3,  3, 32, 32, 32, 32, 32, 32, 32, 32, //4
         4,  4,  4,  4,  4,  4,  4,  4, 32, 32, 32, 32, 32, 32, 32, 32, //5
         3,  3,  3,  1,  4,  4,  4,  4,  2,  1,  2,  1,  3,  3,  3,  3, //6
         2,  1,  2,  1,  3,  3,  3,  3,  2,  1,  2,  2,  3,  3,  3,  3, //7
         4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4, //8
         4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4, //9
         3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //A
         3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //B
         3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //C
         3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //D
         1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, //E
         1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1 //F
    };
    }
}