using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProjectPSX.Devices {

    public class GPU {

        private uint GPUREAD;     //1F801810h-Read GPUREAD Receive responses to GP0(C0h) and GP1(10h) commands

        private uint command;
        private int commandSize;
        //private Queue<uint> commandBuffer = new Queue<uint>(16);
        private uint[] commandBuffer = new uint[16];
        private uint[] emptyBuffer = new uint[16]; //fallback to rewrite
        private int pointer;

        private int scanLine = 0;

        private static readonly int[] resolutions = { 256, 320, 512, 640, 368 };//gpustat res index
        private static readonly int[] dotClockDiv = { 10, 8, 5, 4, 7 };

        private Window window;
        // private DirectBitmap VRAM = new DirectBitmap();
        private Display VRAM = new Display(1024, 512);

        private delegate void Command();

        public bool debug;

        public void setWindow(Window window) {
            this.window = window;
            GP1_ResetGPU();
        }
        private enum Mode {
            COMMAND,
            VRAM
        }
        private Mode mode;

        private ref struct Primitive {
            public bool isShaded;
            public bool isTextured;
            public bool isSemiTransparent;
            public bool isRawTextured;//if not: blended
        }

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
        private Point2D min = new Point2D();
        private Point2D max = new Point2D();

        [StructLayout(LayoutKind.Explicit)]
        private struct TextureData {
            [FieldOffset(0)] public ushort val;
            [FieldOffset(0)] public byte x;
            [FieldOffset(1)] public byte y;
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

        [StructLayout(LayoutKind.Explicit)]
        private ref struct ColorRef {
            [FieldOffset(0)] public uint val;
            [FieldOffset(0)] public byte r;
            [FieldOffset(1)] public byte g;
            [FieldOffset(2)] public byte b;
            [FieldOffset(3)] public byte m;
        }

        private Color color0;
        private Color color1;
        private Color color2;

        private bool isTextureDisabledAllowed;

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
        private byte horizontalResolution2;
        private byte horizontalResolution1;
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

        private int videoCycles;
        private int horizontalTiming = 3413;
        private int verticalTiming = 263;

        public GPU() {
            mode = Mode.COMMAND;
        }

        public bool tick(int cycles) {
            //Video clock is the cpu clock multiplied by 11/7.
            videoCycles += cycles * 11 / 7;


            if (videoCycles >= horizontalTiming) {
                videoCycles -= horizontalTiming;
                scanLine++;

                if (!isVerticalResolution480) {
                    isOddLine = (scanLine & 0x1) != 0;
                }

                if (scanLine >= verticalTiming) {
                    scanLine = 0;

                    if (isVerticalInterlace && isVerticalResolution480) {
                        isOddLine = !isOddLine;
                    }

                    window.update(VRAM.Bits);
                    return true;
                }
            }
            return false;
        }

        public (int dot, bool hblank, bool bBlank) getBlanksAndDot() { //test
            int dot = dotClockDiv[horizontalResolution2 << 2 | horizontalResolution1];
            bool hBlank = videoCycles < displayX1 || videoCycles > displayX2;
            bool vBlank = scanLine < displayY1 || scanLine > displayY2;

            return (dot, hBlank, vBlank);
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
            GPUSTAT |= (uint)horizontalResolution2 << 16;
            GPUSTAT |= (uint)horizontalResolution1 << 17;
            GPUSTAT |= (uint)(isVerticalResolution480 ? 1 : 0);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void writeGP0(uint value) {
            //Console.WriteLine("Direct " + value.ToString("x8"));
            //Console.WriteLine(mode);
            if (mode == Mode.COMMAND) {
                DecodeGP0Command(value);
            } else {
                WriteToVRAM(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void writeGP0(uint[] buffer) {
            //Console.WriteLine("buffer");
            //Console.WriteLine(mode);
            if (mode == Mode.COMMAND) {
                DecodeGP0Command(buffer);
            } else {
                for (int i = 0; i < buffer.Length; i++) {
                    //Console.WriteLine(i + " " + buffer[i].ToString("x8"));
                    WriteToVRAM(buffer[i]);
                }
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
            ushort pixel0 = VRAM.GetPixelBGR555(vram_coord.x++ & 0x3FF, vram_coord.y & 0x1FF);
            ushort pixel1 = VRAM.GetPixelBGR555(vram_coord.x++ & 0x3FF, vram_coord.y & 0x1FF);
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
        private static int get555Color(ushort val) {
            byte m = (byte)(val >> 15);
            byte r = (byte)((val & 0x1F) << 3);
            byte g = (byte)(((val >> 5) & 0x1F) << 3);
            byte b = (byte)(((val >> 10) & 0x1F) << 3);

            return (m << 24 | r << 16 | g << 8 | b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeGP0Command(uint value) {
            if (pointer == 0) {
                command = value >> 24;
                commandSize = CommandSize[command];
                //Console.WriteLine("[GPU] Direct GP0 COMMAND: {0} size: {1}", value.ToString("x8"), commandSize);
            }

            commandBuffer[pointer++] = value;
            //Console.WriteLine("[GPU] Direct GP0: {0} buffer: {1}", value.ToString("x8"), pointer);

            if (pointer == commandSize || commandSize == 16 && (value & 0xF000_F000) == 0x5000_5000) {
                pointer = 0;
                //Console.WriteLine("EXECUTING");
                ExecuteGP0(command);
                pointer = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeGP0Command(uint[] buffer) {
            commandBuffer = buffer;

            //Console.WriteLine(commandBuffer.Length);

            while (pointer < buffer.Length) {
                if (mode == Mode.COMMAND) {
                    command = commandBuffer[pointer] >> 24;
                    //if (debug) Console.WriteLine("Buffer Executing " + command.ToString("x2") + " pointer " + pointer);
                    ExecuteGP0(command);
                } else {
                    WriteToVRAM(commandBuffer[pointer++]);
                }
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

                case uint _ when opcode >= 0x20 && opcode <= 0x3F:
                    GP0_RenderPolygon(); break;
                case uint _ when opcode >= 0x40 && opcode <= 0x5F:
                    GP0_RenderLine(); break;
                case uint _ when opcode >= 0x60 && opcode <= 0x7F:
                    GP0_RenderRectangle(); break;
                case uint _ when opcode >= 0x80 && opcode <= 0x9F:
                    GP0_MemCopyRectVRAMtoVRAM(); break;
                case uint _ when opcode >= 0xA0 && opcode <= 0xBF:
                    GP0_MemCopyRectCPUtoVRAM(); break;
                case uint _ when opcode >= 0xC0 && opcode <= 0xDF:
                    GP0_MemCopyRectVRAMtoCPU(); break;
                case uint _ when (opcode >= 0x3 && opcode <= 0x1E) || opcode == 0xE0 || opcode >= 0xE7 && opcode <= 0xEF:
                    GP0_NOP(); break;

                default: Console.WriteLine("[GPU] Unsupported GP0 Command " + opcode.ToString("x8")); /*Console.ReadLine();*/ GP0_NOP(); break;
            }
        }

        private void GP0_InterruptRequest() {
            pointer++;
            isInterruptRequested = true;
        }

        //int renderline;
        //int rasterizeline;
        private void GP0_RenderLine() {
            //Console.WriteLine("size " + commandBuffer.Count);
            //int arguments = 0;
            uint command = commandBuffer[pointer++];
            //arguments++;

            uint color1 = command & 0xFFFFFF;
            uint color2 = color1;

            bool isPoly = (command & (1 << 27)) != 0;
            bool isShaded = (command & (1 << 28)) != 0;
            bool isTransparent = (command & (1 << 25)) != 0;

            //if (isTextureMapped /*isRaw*/) return;

            uint v1 = commandBuffer[pointer++];
            //arguments++;

            if (isShaded) {
                color2 = commandBuffer[pointer++];
                //arguments++;
            }
            uint v2 = commandBuffer[pointer++];
            //arguments++;

            rasterizeLine(v1, v2, color1, color2, isTransparent);

            if (!isPoly) return;
            //renderline = 0;
            while (/*arguments < 0xF &&*/ (commandBuffer[pointer] & 0xF000_F000) != 0x5000_5000) {
                //Console.WriteLine("DOING ANOTHER LINE " + ++renderline);
                //arguments++;
                color1 = color2;
                if (isShaded) {
                    color2 = commandBuffer[pointer++];
                    //arguments++;
                }
                v1 = v2;
                v2 = commandBuffer[pointer++];
                rasterizeLine(v1, v2, color1, color2, isTransparent);
                //Console.WriteLine("RASTERIZE " + ++rasterizeline);
                //window.update(VRAM.Bits);
                //Console.ReadLine();
            }

            /*if (arguments != 0xF) */
            pointer++; // discard 5555_5555 termination (need to rewrite all this from the GP0...)
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short signed11bit(uint n) {
            return (short)(((int)n << 21) >> 21);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void rasterizeLine(uint v1, uint v2, uint color1, uint color2, bool isTransparent) {
            short x = signed11bit(v1 & 0xFFFF);
            short y = signed11bit(v1 >> 16);

            short x2 = signed11bit(v2 & 0xFFFF);
            short y2 = signed11bit(v2 >> 16);

            if (Math.Abs(x - x2) > 0x3FF || Math.Abs(y - y2) > 0x1FF) return;

            x += drawingXOffset;
            y += drawingYOffset;

            x2 += drawingXOffset;
            y2 += drawingYOffset;

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

                //x = (short)Math.Min(Math.Max(x, drawingAreaLeft), drawingAreaRight); //this generates glitches on RR4
                //y = (short)Math.Min(Math.Max(y, drawingAreaTop), drawingAreaBottom);

                if (x >= drawingAreaLeft && x < drawingAreaRight && y >= drawingAreaTop && y < drawingAreaBottom) {
                    //if (primitive.isSemiTransparent && (!primitive.isTextured || (color & 0xFF00_0000) != 0)) {
                    if (isTransparent) {
                        color = handleSemiTransp(x, y, color, transparency);
                    }
                    VRAM.SetPixel(x, y, color);
                }

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
        private int interpolate(uint c1, uint c2, float ratio) {
            color1.val = c1;
            color2.val = c2;

            byte r = (byte)(color2.r * ratio + color1.r * (1 - ratio));
            byte g = (byte)(color2.g * ratio + color1.g * (1 - ratio));
            byte b = (byte)(color2.b * ratio + color1.b * (1 - ratio));

            return (r << 16 | g << 8 | b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GP0_RenderPolygon() {
            uint command = commandBuffer[pointer];
            //Console.WriteLine(command.ToString("x8") +  " "  + commandBuffer.Length + " " + pointer);

            bool isQuad = (command & (1 << 27)) != 0;

            bool isShaded = (command & (1 << 28)) != 0;
            bool isTextured = (command & (1 << 26)) != 0;
            bool isSemiTransparent = (command & (1 << 25)) != 0;
            bool isRawTextured = (command & (1 << 24)) != 0;

            Primitive primitive = new Primitive();
            primitive.isShaded = isShaded;
            primitive.isTextured = isTextured;
            primitive.isSemiTransparent = isSemiTransparent;
            primitive.isRawTextured = isRawTextured;

            int vertexN = isQuad ? 4 : 3;

            //Point2D[] v = new Point2D[vertexN];
            //TextureData[] t = new TextureData[vertexN];
            Span<uint> c = stackalloc uint[vertexN];

            if (!isShaded) {
                uint color = commandBuffer[pointer++];
                c[0] = color; //triangle 1 opaque color
                c[1] = color; //triangle 2 opaque color
            }

            uint palette = 0;
            uint texpage = (uint)transparency << 5;

            for (int i = 0; i < vertexN; i++) {
                if (isShaded) c[i] = commandBuffer[pointer++];

                v[i].val = commandBuffer[pointer++];
                v[i].x = (short)(signed11bit((uint)v[i].x) + drawingXOffset);
                v[i].y = (short)(signed11bit((uint)v[i].y) + drawingYOffset);

                if (isTextured) {
                    uint textureData = commandBuffer[pointer++];
                    t[i].val = (ushort)textureData;
                    if (i == 0) {
                        palette = textureData >> 16;
                    } else if (i == 1) {
                        texpage = textureData >> 16;
                    }
                }
            }

            rasterizeTri(v[0], v[1], v[2], t[0], t[1], t[2], c[0], c[1], c[2], palette, texpage, primitive);
            if (isQuad) rasterizeTri(v[1], v[2], v[3], t[1], t[2], t[3], c[1], c[2], c[3], palette, texpage, primitive);
        }

        //Mother of parameters. this should be better when c#8 ranges come into play. I could declare 2 new arrays segments but i dont like them
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void rasterizeTri(Point2D v0, Point2D v1, Point2D v2, TextureData t0, TextureData t1, TextureData t2, uint c0, uint c1, uint c2, uint palette, uint texpage, Primitive primitive) {

            int area = orient2d(v0, v1, v2);

            if (area == 0) {
                return;
            }

            if (area < 0) {
                (v1, v2) = (v2, v1);
                (t1, t2) = (t2, t1);
                (c1, c2) = (c2, c1);
                area *= -1;
            }

            /*boundingBox*/
            int minX = Math.Min(v0.x, Math.Min(v1.x, v2.x));
            int minY = Math.Min(v0.y, Math.Min(v1.y, v2.y));
            int maxX = Math.Max(v0.x, Math.Max(v1.x, v2.x));
            int maxY = Math.Max(v0.y, Math.Max(v1.y, v2.y));

            if ((maxX - minX) > 1024 || (maxY - minY) > 512) return;

            /*clip*/
            min.x = (short)Math.Max(minX, drawingAreaLeft);
            min.y = (short)Math.Max(minY, drawingAreaTop);
            max.x = (short)Math.Min(maxX, drawingAreaRight);
            max.y = (short)Math.Min(maxY, drawingAreaBottom);

            int A01 = v0.y - v1.y, B01 = v1.x - v0.x;
            int A12 = v1.y - v2.y, B12 = v2.x - v1.x;
            int A20 = v2.y - v0.y, B20 = v0.x - v2.x;

            int bias0 = isTopLeft(v1, v2) ? 0 : -1;
            int bias1 = isTopLeft(v2, v0) ? 0 : -1;
            int bias2 = isTopLeft(v0, v1) ? 0 : -1;

            int w0_row = orient2d(v1, v2, min);
            int w1_row = orient2d(v2, v0, min);
            int w2_row = orient2d(v0, v1, min);

            int depth = 0;
            int semiTransp = (int)((texpage >> 5) & 0x3);
            Point2D clut = new Point2D();
            Point2D textureBase = new Point2D();

            if (primitive.isTextured) {
                depth = (int)(texpage >> 7) & 0x3;

                clut.x = (short)((palette & 0x3f) << 4);
                clut.y = (short)((palette >> 6) & 0x1FF);

                textureBase.x = (short)((texpage & 0xF) << 6);
                textureBase.y = (short)(((texpage >> 4) & 0x1) << 8);

                forceSetE1(texpage);
            }

            int baseColor = GetRgbColor(c0);
            //TESTING END

            // Rasterize
            for (int y = min.y; y <= max.y; y++) {
                // Barycentric coordinates at start of row
                int w0 = w0_row;
                int w1 = w1_row;
                int w2 = w2_row;

                for (int x = min.x; x <= max.x; x++) {
                    // If p is on or inside all edges, render pixel.
                    if ((w0 + bias0 | w1 + bias1 | w2 + bias2) >= 0) {
                        //Adjustements per triangle instead of per pixel can be done at area level
                        //but it still does some little by 1 error apreciable on some textured quads
                        //I assume it could be handled recalculating AXX and BXX offsets but those maths are beyond my scope

                        // reset default color of the triangle calculated outside the for as it gets overwriten as follows...
                        int color = baseColor;

                        if (primitive.isShaded) color = getShadedColor(w0, w1, w2, c0, c1, c2, area);

                        if (primitive.isTextured) {
                            int texelX = interpolateCoords(w0, w1, w2, t0.x, t1.x, t2.x, area);
                            int texelY = interpolateCoords(w0, w1, w2, t0.y, t1.y, t2.y, area);
                            int texel = getTexel(texelX, texelY, clut, textureBase, depth);
                            if (texel == 0) {
                                w0 += A12;
                                w1 += A20;
                                w2 += A01;
                                continue;
                            }

                            if (!primitive.isRawTextured) {
                                color0.val = (uint)color;
                                color1.val = (uint)texel;
                                color1.r = clampToFF(color0.r * color1.r >> 7);
                                color1.g = clampToFF(color0.g * color1.g >> 7);
                                color1.b = clampToFF(color0.b * color1.b >> 7);

                                texel = (int)color1.val;
                            }

                            color = texel;
                        }

                        if (primitive.isSemiTransparent && (!primitive.isTextured || (color & 0xFF00_0000) != 0)) {
                            color = handleSemiTransp(x, y, color, semiTransp);
                        }

                        VRAM.SetPixel((x & 0x3FF), (y & 0x1FF), color);
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
            //if (debug) {
            //    //window.update(VRAM.Bits);
            //    Console.ReadLine();
            //}
        }

        private static bool isTopLeft(Point2D a, Point2D b) {
            return a.y == b.y && b.x > a.x || b.y < a.y;
        }

        private int handleSemiTransp(int x, int y, int color, int semiTranspMode) {
            color0.val = (uint)VRAM.GetPixelRGB888(x & 0x3FF, y & 0x1FF); //back
            color1.val = (uint)color; //front
            switch (semiTranspMode) {
                case 0: //0.5 x B + 0.5 x F    ;aka B/2+F/2
                    color2.r = (byte)((color0.r + color1.r) >> 1);
                    color2.g = (byte)((color0.g + color1.g) >> 1);
                    color2.b = (byte)((color0.b + color1.b) >> 1);
                    break;
                case 1://1.0 x B + 1.0 x F    ;aka B+F
                    color2.r = clampToFF(color0.r + color1.r);
                    color2.g = clampToFF(color0.g + color1.g);
                    color2.b = clampToFF(color0.b + color1.b);
                    break;
                case 2: //1.0 x B - 1.0 x F    ;aka B-F
                    color2.r = clampToZero(color0.r - color1.r);
                    color2.g = clampToZero(color0.g - color1.g);
                    color2.b = clampToZero(color0.b - color1.b);
                    break;
                case 3: //1.0 x B +0.25 x F    ;aka B+F/4
                    color2.r = clampToFF(color0.r + (color1.r >> 2));
                    color2.g = clampToFF(color0.g + (color1.g >> 2));
                    color2.b = clampToFF(color0.b + (color1.b >> 2));
                    break;
            }//actually doing RGB calcs on BGR struct...
            return (int)color2.val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte clampToZero(int v) {
            if (v < 0) return 0;
            else return (byte)v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte clampToFF(int v) {
            if (v > 0xFF) return 0xFF;
            else return (byte)v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetRgbColor(uint value) {
            color0.val = value;
            return (color0.m << 24 | color0.r << 16 | color0.g << 8 | color0.b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GP0_FillRectVRAM() {
            color0.val = commandBuffer[pointer++];
            uint yx = commandBuffer[pointer++];
            uint hw = commandBuffer[pointer++];

            ushort x = (ushort)(yx & 0x3F0);
            ushort y = (ushort)((yx >> 16) & 0x1FF);

            ushort w = (ushort)(((hw & 0x3FF) + 0xF) & ~0xF);
            ushort h = (ushort)((hw >> 16) & 0x1FF);

            int color = (color0.r << 16 | color0.g << 8 | color0.b);

           for (int yPos = y; yPos < h + y; yPos++) {
               for (int xPos = x; xPos < w + x; xPos++) {
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
            uint opcode = command >> 24;

            bool isTextured = (command & (1 << 26)) != 0;
            bool isSemiTransparent = (command & (1 << 25)) != 0;
            bool isRawTextured = (command & (1 << 24)) != 0;

            Primitive primitive = new Primitive();
            primitive.isTextured = isTextured;
            primitive.isSemiTransparent = isSemiTransparent;
            primitive.isRawTextured = isRawTextured;

            uint vertex = commandBuffer[pointer++];
            short xo = signed11bit(vertex & 0xFFFF);
            short yo = signed11bit(vertex >> 16);

            ushort palette = 0;
            byte textureX = 0;
            byte textureY = 0;
            if (isTextured) {
                uint texture = commandBuffer[pointer++];
                palette = (ushort)((texture >> 16) & 0xFFFF);
                textureX = (byte)(texture & 0xFF);
                textureY = (byte)((texture >> 8) & 0xFF);
            }

            short width = 0;
            short heigth = 0;

            switch ((opcode & 0x18) >> 3) {
                case 0x0:
                    uint hw = commandBuffer[pointer++];
                    width = (short)(hw & 0xFFFF);
                    heigth = (short)(hw >> 16);
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
            }

            int y = yo + drawingYOffset;
            int x = xo + drawingXOffset;

            v[0].x = (short)x;
            v[0].y = (short)y;

            v[3].x = (short)(x + width);
            v[3].y = (short)(y + heigth);

            t[0].x = textureX;
            t[0].y = textureY;

            uint texpage = getTexpageFromGPU();

            rasterizeRect(v, t[0], color, palette, texpage, primitive);
        }

        private void rasterizeRect(Point2D[] vec, TextureData t, uint c, ushort palette, uint texpage, Primitive primitive) {
            int xOrigin = Math.Max(vec[0].x, drawingAreaLeft);
            int yOrigin = Math.Max(vec[0].y, drawingAreaTop);
            int width = Math.Min(vec[3].x, drawingAreaRight);
            int height = Math.Min(vec[3].y, drawingAreaBottom);

            int depth = (int)(texpage >> 7) & 0x3;
            int semiTransp = (int)((texpage >> 5) & 0x3);

            Point2D clut = new Point2D();
            clut.x = (short)((palette & 0x3f) << 4);
            clut.y = (short)((palette >> 6) & 0x1FF);

            Point2D textureBase = new Point2D();
            textureBase.x = (short)((texpage & 0xF) << 6);
            textureBase.y = (short)(((texpage >> 4) & 0x1) << 8);

            int uOrigin = t.x + (xOrigin - vec[0].x);
            int vOrigin = t.y + (yOrigin - vec[0].y);

            int baseColor = GetRgbColor(c);

            for (int y = yOrigin, v = vOrigin; y < height; y++, v++) {
                for (int x = xOrigin, u = uOrigin; x < width; x++, u++) {
                    int color = baseColor;

                    if (primitive.isTextured) {
                        int texel = getTexel(u, v, clut, textureBase, depth);
                        if (texel == 0) {
                            continue;
                        }

                        if (!primitive.isRawTextured) {
                            color0.val = (uint)color;
                            color1.val = (uint)texel;
                            color1.r = clampToFF(color0.r * color1.r >> 7);
                            color1.g = clampToFF(color0.g * color1.g >> 7);
                            color1.b = clampToFF(color0.b * color1.b >> 7);

                            texel = (int)color1.val;
                        }

                        color = texel;
                    }

                    if (primitive.isSemiTransparent && (!primitive.isTextured || (color & 0xFF00_0000) != 0)) {
                        color = handleSemiTransp(x, y, color, semiTransp);
                    }

                    VRAM.SetPixel(x, y, color);
                }

            }
        }

        private void GP0_MemCopyRectVRAMtoCPU() {
            pointer++; //Command/Color parameter unused
            uint yx = commandBuffer[pointer++];
            uint wh = commandBuffer[pointer++];

            ushort x = (ushort)(yx & 0x3FF);
            ushort y = (ushort)((yx >> 16) & 0x1FF);

            ushort w = (ushort)((((wh & 0xFFFF) - 1) & 0x3FF) + 1);
            ushort h = (ushort)((((wh >> 16) - 1) & 0x1FF) + 1);

            vram_coord.x = x;
            vram_coord.origin_x = x;
            vram_coord.y = y;
            vram_coord.w = w;
            vram_coord.h = h;
            vram_coord.size = h * w;
        }

        private void GP0_MemCopyRectCPUtoVRAM() { //todo rewrite VRAM coord struct mess
            pointer++; //Command/Color parameter unused
            uint yx = commandBuffer[pointer++];
            uint wh = commandBuffer[pointer++];

            ushort x = (ushort)(yx & 0x3FF);
            ushort y = (ushort)((yx >> 16) & 0x1FF);

            ushort w = (ushort)((((wh & 0xFFFF) - 1) & 0x3FF) + 1);
            ushort h = (ushort)((((wh >> 16) - 1) & 0x1FF) + 1);

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

            ushort sx = (ushort)(sourceXY & 0x3FF);
            ushort sy = (ushort)((sourceXY >> 16) & 0x1FF);

            ushort dx = (ushort)(destinationXY & 0x3FF);
            ushort dy = (ushort)((destinationXY >> 16) & 0x1FF);

            ushort w = (ushort)((((wh & 0xFFFF) - 1) & 0x3FF) + 1);
            ushort h = (ushort)((((wh >> 16) - 1) & 0x1FF) + 1);

            for (int yPos = 0; yPos < h; yPos++) {
                for (int xPos = 0; xPos < w; xPos++) {
                    int color = VRAM.GetPixelRGB888((sx + xPos) & 0x3FF, (sy + yPos) & 0x1FF);
                    VRAM.SetPixel((dx + xPos) & 0x3FF, (dy + yPos) & 0x1FF, color);
                }
            }
        }

        private void GP0_MemClearCache() {
            pointer++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int getShadedColor(int w0, int w1, int w2, uint c0, uint c1, uint c2, int area) {
            ColorRef color0 = new ColorRef();
            color0.val = c0;
            ColorRef color1 = new ColorRef();
            color1.val = c1;
            ColorRef color2 = new ColorRef();
            color2.val = c2;

            int r = (color0.r * w0 + color1.r * w1 + color2.r * w2) / area;
            int g = (color0.g * w0 + color1.g * w1 + color2.g * w2) / area;
            int b = (color0.b * w0 + color1.b * w1 + color2.b * w2) / area;

            return (r << 16 | g << 8 | b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int interpolateCoords(int w0, int w1, int w2, int t0, int t1, int t2, int area) {
            //https://codeplea.com/triangular-interpolation
            return (t0 * w0 + t1 * w1 + t2 * w2) / area;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int getTexel(int x, int y, Point2D clut, Point2D textureBase, int depth) {
            x &= 255;
            y &= 255;

            // Texture masking: texel = (texel AND(NOT(Mask * 8))) OR((Offset AND Mask) * 8)
            x = (x & ~(textureWindowMaskX * 8)) | ((textureWindowOffsetX & textureWindowMaskX) * 8);
            y = (y & ~(textureWindowMaskY * 8)) | ((textureWindowOffsetY & textureWindowMaskY) * 8);

            if (depth == 0) {
                return get4bppTexel(x, y, clut, textureBase);
            } else if (depth == 1) {
                return get8bppTexel(x, y, clut, textureBase);
            } else {
                return get16bppTexel(x, y, textureBase);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int get4bppTexel(int x, int y, Point2D clut, Point2D textureBase) {
            ushort index = VRAM.GetPixelBGR555(x / 4 + textureBase.x, y + textureBase.y);
            int p = (index >> (x & 3) * 4) & 0xF;
            return VRAM.GetPixelRGB888(clut.x + p, clut.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int get8bppTexel(int x, int y, Point2D clut, Point2D textureBase) {
            ushort index = VRAM.GetPixelBGR555(x / 2 + textureBase.x, y + textureBase.y);
            int p = (index >> (x & 1) * 8) & 0xFF;
            return VRAM.GetPixelRGB888(clut.x + p, clut.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int get16bppTexel(int x, int y, Point2D textureBase) {
            return VRAM.GetPixelRGB888(x + textureBase.x, y + textureBase.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int orient2d(Point2D a, Point2D b, Point2D c) {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
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

            drawingXOffset = signed11bit(val & 0x7FF);
            drawingYOffset = signed11bit((val >> 11) & 0x7FF);
        }

        private void GP0_NOP() {
            pointer++;
        }

        private void forceSetE1(uint texpage) {
            textureXBase = (byte)(texpage & 0xF);
            textureYBase = (byte)((texpage >> 4) & 0x1);
            transparency = (byte)((texpage >> 5) & 0x3);
            textureDepth = (byte)((texpage >> 7) & 0x3);
            isTextureDisabled = isTextureDisabledAllowed ? ((texpage >> 11) & 0x1) != 0 : false;

            //Console.WriteLine("[GPU] [GP0] Force DrawMode ");
        }

        private void GP0_SetDrawMode() {
            uint val = commandBuffer[pointer++];

            textureXBase = (byte)(val & 0xF);
            textureYBase = (byte)((val >> 4) & 0x1);
            transparency = (byte)((val >> 5) & 0x3);
            textureDepth = (byte)((val >> 7) & 0x3);
            isDithered = ((val >> 9) & 0x1) != 0;
            isDrawingToDisplayAllowed = ((val >> 10) & 0x1) != 0;
            isTextureDisabled = isTextureDisabledAllowed ? ((val >> 11) & 0x1) != 0 : false;
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
            //Execute GP1 Command
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
                case 0x09: GP1_TextureDisable(value); break;
                case uint _ when opcode >= 0x10 && opcode <= 0x1F:
                    GP1_GPUInfo(value); break;
                default: Console.WriteLine("[GPU] Unsupported GP1 Command " + opcode.ToString("x8")); Console.ReadLine(); break;
            }
        }

        private void GP1_TextureDisable(uint value) {
            isTextureDisabledAllowed = (value & 0x1) != 0;
        }

        private void GP1_GPUInfo(uint value) {
            uint info = value & 0xF;
            switch (info) {
                case 0x2: GPUREAD = (uint)(textureWindowOffsetY << 15 | textureWindowOffsetX << 10 | textureWindowMaskY << 5 | textureWindowMaskX); break;
                case 0x3: GPUREAD = (uint)(drawingAreaTop << 10 | drawingAreaLeft); break;
                case 0x4: GPUREAD = (uint)(drawingAreaBottom << 10 | drawingAreaRight); break;
                case 0x5: GPUREAD = (uint)(drawingYOffset << 11 | (ushort)drawingXOffset); break;
                case 0x7: GPUREAD = 2; break;
                case 0x8: GPUREAD = 0; break;
                default: Console.WriteLine("[GPU] GP1 Unhandled GetInfo: " + info.ToString("x8")); break;
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

            window.setVerticalRange(displayY1, displayY2);
        }

        private void GP1_DisplayHorizontalRange(uint value) {
            displayX1 = (ushort)(value & 0xFFF);
            displayX2 = (ushort)((value >> 12) & 0xFFF);

            window.setHorizontalRange(displayX1, displayX2);
        }

        private void GP1_DisplayVRAMStart(uint value) {
            displayVRAMXStart = (ushort)(value & 0x3FE);
            displayVRAMYStart = (ushort)((value >> 10) & 0x1FE);

            window.setVRAMStart(displayVRAMXStart, displayVRAMYStart);
        }

        private void GP1_DMADirection(uint value) {
            dmaDirection = (byte)(value & 0x3);
        }

        private void GP1_DisplayMode(uint value) {
            horizontalResolution1 = (byte)(value & 0x3);
            isVerticalResolution480 = (value & 0x4) != 0;
            isPal = (value & 0x8) != 0;
            is24BitDepth = (value & 0x10) != 0;
            isVerticalInterlace = (value & 0x20) != 0;
            horizontalResolution2 = (byte)((value & 0x40) >> 6);
            isReverseFlag = (value & 0x80) != 0;

            isInterlaceField = isVerticalInterlace ? true : false;

            horizontalTiming = isPal ? 3406 : 3413;
            verticalTiming = isPal ? 314 : 263;

            int horizontalRes = resolutions[horizontalResolution2 << 2 | horizontalResolution1];
            int verticalRes = isVerticalResolution480 ? 480 : 240;

            window.setDisplayMode(horizontalRes, verticalRes, is24BitDepth);
        }

        private void GP1_ResetGPU() {
            GP1_ResetCommandBuffer();
            GP1_AckGPUInterrupt();
            GP1_DisplayEnable(1);
            GP1_DMADirection(0);
            GP1_DisplayVRAMStart(0);
            GP1_DisplayHorizontalRange(0xC00200);
            GP1_DisplayVerticalRange(0x100010);
            GP1_DisplayMode(0);

            //GP0 E1
            textureXBase = 0;
            textureYBase = 0;
            transparency = 0;
            textureDepth = 0;
            isDithered = false;
            isDrawingToDisplayAllowed = false;
            isTextureDisabled = false;
            isTexturedRectangleXFlipped = false;
            isTexturedRectangleYFlipped = false;

            //GP0 E2
            textureWindowMaskX = 0;
            textureWindowMaskY = 0;
            textureWindowOffsetX = 0;
            textureWindowOffsetY = 0;

            //GP0 E3
            drawingAreaTop = 0;
            drawingAreaLeft = 0;

            //GP0 E4
            drawingAreaBottom = 0;
            drawingAreaRight = 0;

            //GP0 E5
            drawingXOffset = 0;
            drawingYOffset = 0;

            //GP0 E6
            isMasked = false;
            isMaskedPriority = false;
        }

        private uint getTexpageFromGPU() {
            uint texpage = 0;

            texpage |= (isTexturedRectangleYFlipped ? 1u : 0) << 13;
            texpage |= (isTexturedRectangleXFlipped ? 1u : 0) << 12;
            texpage |= (isTextureDisabled ? 1u : 0) << 11;
            texpage |= (isDrawingToDisplayAllowed ? 1u : 0) << 10;
            texpage |= (isDithered ? 1u : 0) << 9;
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
        private static readonly int[] CommandSize = {
        //0  1   2   3   4   5   6   7   8   9   A   B   C   D   E   F
         1,  1,  3,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, //0
         1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, //1
         4,  4,  4,  4,  7,  7,  7,  7,  5,  5,  5,  5,  9,  9,  9,  9, //2
         6,  6,  6,  6,  9,  9,  9,  9,  8,  8,  8,  8, 12, 12, 12, 12, //3
         3,  3,  3,  3,  3,  3,  3,  3, 16, 16, 16, 16, 16, 16, 16, 16, //4
         4,  4,  4,  4,  4,  4,  4,  4, 16, 16, 16, 16, 16, 16, 16, 16, //5
         3,  3,  3,  1,  4,  4,  4,  4,  2,  1,  2,  1,  3,  3,  3,  3, //6
         2,  1,  2,  1,  3,  3,  3,  3,  2,  1,  2,  2,  3,  3,  3,  3, //7
         4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4, //8
         4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4, //9
         3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //A
         3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //B
         3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //C
         3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //D
         1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, //E
         1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1  //F
    };
    }
}
