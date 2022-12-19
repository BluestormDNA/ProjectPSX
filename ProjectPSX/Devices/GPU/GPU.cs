using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProjectPSX.Devices {

    public class GPU {

        private uint GPUREAD;     //1F801810h-Read GPUREAD Receive responses to GP0(C0h) and GP1(10h) commands

        private uint command;
        private int commandSize;
        private uint[] commandBuffer = new uint[16];
        private int pointer;

        private int scanLine = 0;

        private static readonly int[] resolutions = { 256, 320, 512, 640, 368 };//gpustat res index
        private static readonly int[] dotClockDiv = { 10, 8, 5, 4, 7 };

        private IHostWindow window;

        private VRAM vram = new VRAM(); //Vram is 8888 and we transform everything to it
        private VRAM1555 vram1555 = new VRAM1555(); //an un transformed 1555 to 8888 vram so we can fetch clut indexes without reverting to 1555

        private int[] color1555to8888LUT;

        public bool debug;

        private enum Mode {
            COMMAND,
            VRAM
        }
        private Mode mode;

        private struct Primitive {
            public bool isShaded;
            public bool isTextured;
            public bool isSemiTransparent;
            public bool isRawTextured;//if not: blended
            public int depth;
            public int semiTransparencyMode;
            public Point2D clut;
            public Point2D textureBase;
        }

        private struct VramTransfer {
            public int x, y;
            public ushort w, h;
            public int origin_x;
            public int origin_y;
            public int halfWords;
        }
        private VramTransfer vramTransfer;


        [StructLayout(LayoutKind.Explicit)]
        private struct Point2D {
            [FieldOffset(0)] public short x;
            [FieldOffset(2)] public short y;
        }
        private Point2D min = new Point2D();
        private Point2D max = new Point2D();

        [StructLayout(LayoutKind.Explicit)]
        private struct TextureData {
            [FieldOffset(0)] public ushort val;
            [FieldOffset(0)] public byte x;
            [FieldOffset(1)] public byte y;
        }
        TextureData textureData = new TextureData();

        [StructLayout(LayoutKind.Explicit)]
        private struct Color {
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
        private byte transparencyMode;
        private byte textureDepth;
        private bool isDithered;
        private bool isDrawingToDisplayAllowed;
        private int maskWhileDrawing;
        private bool checkMaskBeforeDraw;
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

        private bool isReadyToReceiveCommand = true; //todo
        private bool isReadyToSendVRAMToCPU; 
        private bool isReadyToReceiveDMABlock = true; //todo

        private byte dmaDirection;
        private bool isOddLine;

        private bool isTexturedRectangleXFlipped;
        private bool isTexturedRectangleYFlipped;

        private uint textureWindowBits = 0xFFFF_FFFF;
        private int preMaskX;
        private int preMaskY;
        private int postMaskX;
        private int postMaskY;

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

        public GPU(IHostWindow window) {
            this.window = window;
            mode = Mode.COMMAND;
            initColorTable();
            GP1_00_ResetGPU();
        }

        public void initColorTable() {
            color1555to8888LUT = new int[ushort.MaxValue + 1];
            for (int m = 0; m < 2; m++) {
                for (int r = 0; r < 32; r++) {
                    for (int g = 0; g < 32; g++) {
                        for (int b = 0; b < 32; b++) {
                            color1555to8888LUT[m << 15 | b << 10 | g << 5 | r] = m << 24 | r << 16 + 3 | g << 8 + 3| b << 3;
                        }
                    }
                }
            }
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
                        isInterlaceField = !isOddLine;
                    }

                    window.Render(vram.Bits);
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
            GPUSTAT |= (uint)transparencyMode << 5;
            GPUSTAT |= (uint)textureDepth << 7;
            GPUSTAT |= (uint)(isDithered ? 1 : 0) << 9;
            GPUSTAT |= (uint)(isDrawingToDisplayAllowed ? 1 : 0) << 10;
            GPUSTAT |= (uint)maskWhileDrawing << 11;
            GPUSTAT |= (uint)(checkMaskBeforeDraw ? 1 : 0) << 12;
            GPUSTAT |= (uint)(isInterlaceField ? 1 : 0) << 13;
            GPUSTAT |= (uint)(isReverseFlag ? 1 : 0) << 14;
            GPUSTAT |= (uint)(isTextureDisabled ? 1 : 0) << 15;
            GPUSTAT |= (uint)horizontalResolution2 << 16;
            GPUSTAT |= (uint)horizontalResolution1 << 17;
            GPUSTAT |= (uint)(isVerticalResolution480 ? 1 : 0) << 19;
            GPUSTAT |= (uint)(isPal ? 1 : 0) << 20;
            GPUSTAT |= (uint)(is24BitDepth ? 1 : 0) << 21;
            GPUSTAT |= (uint)(isVerticalInterlace ? 1 : 0) << 22;
            GPUSTAT |= (uint)(isDisplayDisabled ? 1 : 0) << 23;
            GPUSTAT |= (uint)(isInterruptRequested ? 1 : 0) << 24;
            GPUSTAT |= (uint)(isDmaRequest ? 1 : 0) << 25;

            GPUSTAT |= (uint)(isReadyToReceiveCommand ? 1 : 0) << 26;
            GPUSTAT |= (uint)(isReadyToSendVRAMToCPU ? 1 : 0) << 27;
            GPUSTAT |= (uint)(isReadyToReceiveDMABlock ? 1 : 0) << 28;

            GPUSTAT |= (uint)dmaDirection << 29;
            GPUSTAT |= (uint)(isOddLine ? 1 : 0) << 31;

            //Console.WriteLine("[GPU] LOAD GPUSTAT: {0}", GPUSTAT.ToString("x8"));
            return GPUSTAT;
        }

        public uint loadGPUREAD() {
            //TODO check if correct and refact
            uint value;
            if (vramTransfer.halfWords > 0) {
                value = readFromVRAM();
            } else {
                value = GPUREAD;
            }
            //Console.WriteLine("[GPU] LOAD GPUREAD: {0}", value.ToString("x8"));
            return value;
        }

        public void write(uint addr, uint value) {
            uint register = addr & 0xF;
            if (register == 0) {
                writeGP0(value);
            } else if (register == 4) {
                writeGP1(value);
            } else {
                Console.WriteLine($"[GPU] Unhandled GPU write access to register {register} : {value}");
            }
        }

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
        internal void processDma(Span<uint> dma) {
            if (mode == Mode.COMMAND) {
                DecodeGP0Command(dma);
            } else {
                for (int i = 0; i < dma.Length; i++) {
                    WriteToVRAM(dma[i]);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteToVRAM(uint value) {
            ushort pixel1 = (ushort)(value >> 16);
            ushort pixel0 = (ushort)(value & 0xFFFF);

            pixel0 |= (ushort)(maskWhileDrawing << 15);
            pixel1 |= (ushort)(maskWhileDrawing << 15);

            drawVRAMPixel(pixel0);

            //Force exit if we arrived to the end pixel (fixes weird artifacts on textures on Metal Gear Solid)
            if (--vramTransfer.halfWords == 0) {
                mode = Mode.COMMAND;
                return;
            }

            drawVRAMPixel(pixel1);

            if (--vramTransfer.halfWords == 0) {
                mode = Mode.COMMAND;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint readFromVRAM() {
            ushort pixel0 = vram.GetPixelBGR555(vramTransfer.x & 0x3FF, vramTransfer.y & 0x1FF);
            stepVramTransfer();
            ushort pixel1 = vram.GetPixelBGR555(vramTransfer.x & 0x3FF, vramTransfer.y & 0x1FF);
            stepVramTransfer();

            vramTransfer.halfWords -= 2;

            if (vramTransfer.halfWords == 0) {
                isReadyToSendVRAMToCPU = false;
                isReadyToReceiveDMABlock = true;
            }

            return (uint)(pixel1 << 16 | pixel0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void stepVramTransfer() {
            if (++vramTransfer.x == vramTransfer.origin_x + vramTransfer.w) {
                vramTransfer.x -= vramTransfer.w;
                vramTransfer.y++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void drawVRAMPixel(ushort color1555) {
            if (!checkMaskBeforeDraw || vram.GetPixelRGB888(vramTransfer.x, vramTransfer.y) >> 24 == 0) {
                vram.SetPixel(vramTransfer.x & 0x3FF, vramTransfer.y & 0x1FF, color1555to8888LUT[color1555]);
                vram1555.SetPixel(vramTransfer.x & 0x3FF, vramTransfer.y & 0x1FF, color1555);
            }

            stepVramTransfer();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeGP0Command(uint value) {
            if (pointer == 0) {
                command = value >> 24;
                commandSize = CommandSizeTable[(int)command];
                //Console.WriteLine("[GPU] Direct GP0 COMMAND: {0} size: {1}", value.ToString("x8"), commandSize);
            }

            commandBuffer[pointer++] = value;
            //Console.WriteLine("[GPU] Direct GP0: {0} buffer: {1}", value.ToString("x8"), pointer);

            if (pointer == commandSize || commandSize == 16 && (value & 0xF000_F000) == 0x5000_5000) {
                pointer = 0;
                //Console.WriteLine("EXECUTING");
                ExecuteGP0(command, commandBuffer.AsSpan());
                pointer = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeGP0Command(Span<uint> buffer) {
            //Console.WriteLine(commandBuffer.Length);

            while (pointer < buffer.Length) {
                if (mode == Mode.COMMAND) {
                    command = buffer[pointer] >> 24;
                    //if (debug) Console.WriteLine("Buffer Executing " + command.ToString("x2") + " pointer " + pointer);
                    ExecuteGP0(command, buffer);
                } else {
                    WriteToVRAM(buffer[pointer++]);
                }
            }
            pointer = 0;
            //Console.WriteLine("fin");
        }

        private void ExecuteGP0(uint opcode, Span<uint> buffer) {
            //Console.WriteLine("GP0 Command: " + opcode.ToString("x2"));
            switch (opcode) {
                case 0x00: GP0_00_NOP(); break;
                case 0x01: GP0_01_MemClearCache(); break;
                case 0x02: GP0_02_FillRectVRAM(buffer); break;
                case 0x1F: GP0_1F_InterruptRequest(); break;

                case 0xE1: GP0_E1_SetDrawMode(buffer[pointer++]); break;
                case 0xE2: GP0_E2_SetTextureWindow(buffer[pointer++]); break;
                case 0xE3: GP0_E3_SetDrawingAreaTopLeft(buffer[pointer++]); break;
                case 0xE4: GP0_E4_SetDrawingAreaBottomRight(buffer[pointer++]); break;
                case 0xE5: GP0_E5_SetDrawingOffset(buffer[pointer++]); break;
                case 0xE6: GP0_E6_SetMaskBit(buffer[pointer++]); break;

                case uint _ when opcode >= 0x20 && opcode <= 0x3F:
                    GP0_RenderPolygon(buffer); break;
                case uint _ when opcode >= 0x40 && opcode <= 0x5F:
                    GP0_RenderLine(buffer); break;
                case uint _ when opcode >= 0x60 && opcode <= 0x7F:
                    GP0_RenderRectangle(buffer); break;
                case uint _ when opcode >= 0x80 && opcode <= 0x9F:
                    GP0_MemCopyRectVRAMtoVRAM(buffer); break;
                case uint _ when opcode >= 0xA0 && opcode <= 0xBF:
                    GP0_MemCopyRectCPUtoVRAM(buffer); break;
                case uint _ when opcode >= 0xC0 && opcode <= 0xDF:
                    GP0_MemCopyRectVRAMtoCPU(buffer); break;
                case uint _ when (opcode >= 0x3 && opcode <= 0x1E) || opcode == 0xE0 || opcode >= 0xE7 && opcode <= 0xEF:
                    GP0_00_NOP(); break;

                default: Console.WriteLine("[GPU] Unsupported GP0 Command " + opcode.ToString("x8")); /*Console.ReadLine();*/ GP0_00_NOP(); break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GP0_00_NOP() => pointer++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GP0_01_MemClearCache() => pointer++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GP0_02_FillRectVRAM(Span<uint> buffer) {
            color0.val = buffer[pointer++];
            uint yx = buffer[pointer++];
            uint hw = buffer[pointer++];

            ushort x = (ushort)(yx & 0x3F0);
            ushort y = (ushort)((yx >> 16) & 0x1FF);

            ushort w = (ushort)(((hw & 0x3FF) + 0xF) & ~0xF);
            ushort h = (ushort)((hw >> 16) & 0x1FF);

            int color = color0.r << 16 | color0.g << 8 | color0.b;

            if(x + w <= 0x3FF && y + h <= 0x1FF) {
                var vramSpan = new Span<int>(vram.Bits);
                for (int yPos = y; yPos < h + y; yPos++) {
                    vramSpan.Slice(x + (yPos * 1024), w).Fill(color);
                }
            } else {
                for (int yPos = y; yPos < h + y; yPos++) {
                    for (int xPos = x; xPos < w + x; xPos++) {
                        vram.SetPixel(xPos & 0x3FF, yPos & 0x1FF, color);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GP0_1F_InterruptRequest() {
            pointer++;
            isInterruptRequested = true;
        }

        public void GP0_RenderPolygon(Span<uint> buffer) {
            uint command = buffer[pointer];
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
            Span<uint> c = stackalloc uint[vertexN];
            Span<Point2D> v = stackalloc Point2D[vertexN];
            Span<TextureData> t = stackalloc TextureData[vertexN];

            if (!isShaded) {
                uint color = buffer[pointer++];
                c[0] = color; //triangle 1 opaque color
                c[1] = color; //triangle 2 opaque color
            }

            primitive.semiTransparencyMode = transparencyMode;

            for (int i = 0; i < vertexN; i++) {
                if (isShaded) c[i] = buffer[pointer++];

                uint xy = buffer[pointer++];
                v[i].x = (short)(signed11bit(xy & 0xFFFF) + drawingXOffset);
                v[i].y = (short)(signed11bit(xy >> 16) + drawingYOffset);

                if (isTextured) {
                    uint textureData = buffer[pointer++];
                    t[i].val = (ushort)textureData;
                    if (i == 0) {
                        uint palette = textureData >> 16;

                        primitive.clut.x = (short)((palette & 0x3f) << 4);
                        primitive.clut.y = (short)((palette >> 6) & 0x1FF);
                    } else if (i == 1) {
                        uint texpage = textureData >> 16;

                        //SET GLOBAL GPU E1
                        textureXBase = (byte)(texpage & 0xF);
                        textureYBase = (byte)((texpage >> 4) & 0x1);
                        transparencyMode = (byte)((texpage >> 5) & 0x3);
                        textureDepth = (byte)((texpage >> 7) & 0x3);
                        isTextureDisabled = isTextureDisabledAllowed && ((texpage >> 11) & 0x1) != 0;

                        primitive.depth = textureDepth;
                        primitive.textureBase.x = (short)(textureXBase << 6);
                        primitive.textureBase.y = (short)(textureYBase << 8);
                        primitive.semiTransparencyMode = transparencyMode;
                    }
                }
            }

            rasterizeTri(v[0], v[1], v[2], t[0], t[1], t[2], c[0], c[1], c[2], primitive);
            if (isQuad) rasterizeTri(v[1], v[2], v[3], t[1], t[2], t[3], c[1], c[2], c[3], primitive);
        }

        private void rasterizeTri(Point2D v0, Point2D v1, Point2D v2, TextureData t0, TextureData t1, TextureData t2, uint c0, uint c1, uint c2, Primitive primitive) {

            int area = orient2d(v0, v1, v2);

            if (area == 0) return;

            if (area < 0) {
                (v1, v2) = (v2, v1);
                (t1, t2) = (t2, t1);
                (c1, c2) = (c2, c1);
                area = -area;
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

            int w0_row = orient2d(v1, v2, min) + bias0;
            int w1_row = orient2d(v2, v0, min) + bias1;
            int w2_row = orient2d(v0, v1, min) + bias2;

            int baseColor = GetRgbColor(c0);

            // Rasterize
            for (int y = min.y; y < max.y; y++) {
                // Barycentric coordinates at start of row
                int w0 = w0_row;
                int w1 = w1_row;
                int w2 = w2_row;

                for (int x = min.x; x < max.x; x++) {
                    // If p is on or inside all edges, render pixel.
                    if ((w0 | w1 | w2) >= 0) {
                        //Adjustements per triangle instead of per pixel can be done at area level
                        //but it still does some little by 1 error apreciable on some textured quads
                        //I assume it could be handled recalculating AXX and BXX offsets but those maths are beyond my scope

                        //Check background mask
                        if (checkMaskBeforeDraw) {
                            color0.val = (uint)vram.GetPixelRGB888(x, y); //back
                            if (color0.m != 0) {
                                w0 += A12;
                                w1 += A20;
                                w2 += A01;
                                continue;
                            }
                        }

                        // reset default color of the triangle calculated outside the for as it gets overwriten as follows...
                        int color = baseColor;

                        if (primitive.isShaded) {
                            color0.val = c0;
                            color1.val = c1;
                            color2.val = c2;

                            int r = interpolate(w0 - bias0, w1 - bias1, w2 - bias2, color0.r, color1.r, color2.r, area);
                            int g = interpolate(w0 - bias0, w1 - bias1, w2 - bias2, color0.g, color1.g, color2.g, area);
                            int b = interpolate(w0 - bias0, w1 - bias1, w2 - bias2, color0.b, color1.b, color2.b, area);
                            color = r << 16 | g << 8 | b;
                        }

                        if (primitive.isTextured) {
                            int texelX = interpolate(w0 - bias0, w1 - bias1, w2 - bias2, t0.x, t1.x, t2.x, area);
                            int texelY = interpolate(w0 - bias0, w1 - bias1, w2 - bias2, t0.y, t1.y, t2.y, area);
                            int texel = getTexel(maskTexelAxis(texelX, preMaskX, postMaskX), maskTexelAxis(texelY, preMaskY, postMaskY), primitive.clut, primitive.textureBase, primitive.depth);
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
                            color = handleSemiTransp(x, y, color, primitive.semiTransparencyMode);
                        }

                        color |= maskWhileDrawing << 24;

                        vram.SetPixel(x, y, color);
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

        private void GP0_RenderLine(Span<uint> buffer) {
            //Console.WriteLine("size " + commandBuffer.Count);
            //int arguments = 0;
            uint command = buffer[pointer++];
            //arguments++;

            uint color1 = command & 0xFFFFFF;
            uint color2 = color1;

            bool isPoly = (command & (1 << 27)) != 0;
            bool isShaded = (command & (1 << 28)) != 0;
            bool isTransparent = (command & (1 << 25)) != 0;

            //if (isTextureMapped /*isRaw*/) return;

            uint v1 = buffer[pointer++];
            //arguments++;

            if (isShaded) {
                color2 = buffer[pointer++];
                //arguments++;
            }
            uint v2 = buffer[pointer++];
            //arguments++;

            rasterizeLine(v1, v2, color1, color2, isTransparent);

            if (!isPoly) return;
            //renderline = 0;
            while (/*arguments < 0xF &&*/ (buffer[pointer] & 0xF000_F000) != 0x5000_5000) {
                //Console.WriteLine("DOING ANOTHER LINE " + ++renderline);
                //arguments++;
                color1 = color2;
                if (isShaded) {
                    color2 = buffer[pointer++];
                    //arguments++;
                }
                v1 = v2;
                v2 = buffer[pointer++];
                rasterizeLine(v1, v2, color1, color2, isTransparent);
                //Console.WriteLine("RASTERIZE " + ++rasterizeline);
                //window.update(VRAM.Bits);
                //Console.ReadLine();
            }

            /*if (arguments != 0xF) */
            pointer++; // discard 5555_5555 termination (need to rewrite all this from the GP0...)
        }

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
                        color = handleSemiTransp(x, y, color, transparencyMode);
                    }

                    color |= maskWhileDrawing << 24;

                    vram.SetPixel(x, y, color);
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

        private void GP0_RenderRectangle(Span<uint> buffer) {
            //1st Color+Command(CcBbGgRrh)
            //2nd Vertex(YyyyXxxxh)
            //3rd Texcoord+Palette(ClutYyXxh)(for 4bpp Textures Xxh must be even!) //Only textured
            //4rd (3rd non textured) Width + Height(YsizXsizh)(variable opcode only)(max 1023x511)
            uint command = buffer[pointer++];
            uint color = command & 0xFFFFFF;
            uint opcode = command >> 24;

            bool isTextured = (command & (1 << 26)) != 0;
            bool isSemiTransparent = (command & (1 << 25)) != 0;
            bool isRawTextured = (command & (1 << 24)) != 0;

            Primitive primitive = new Primitive();
            primitive.isTextured = isTextured;
            primitive.isSemiTransparent = isSemiTransparent;
            primitive.isRawTextured = isRawTextured;

            uint vertex = buffer[pointer++];
            short xo = (short)(vertex & 0xFFFF);
            short yo = (short)(vertex >> 16);

            if (isTextured) {
                uint texture = buffer[pointer++];
                textureData.x = (byte)(texture & 0xFF);
                textureData.y = (byte)((texture >> 8) & 0xFF);

                ushort palette = (ushort)((texture >> 16) & 0xFFFF);
                primitive.clut.x = (short)((palette & 0x3f) << 4);
                primitive.clut.y = (short)((palette >> 6) & 0x1FF);
            }

            primitive.depth = textureDepth;
            primitive.textureBase.x = (short)(textureXBase << 6);
            primitive.textureBase.y = (short)(textureYBase << 8);
            primitive.semiTransparencyMode = transparencyMode;

            short width = 0;
            short heigth = 0;

            switch ((opcode & 0x18) >> 3) {
                case 0x0:
                    uint hw = buffer[pointer++];
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

            short y = signed11bit((uint)(yo + drawingYOffset));
            short x = signed11bit((uint)(xo + drawingXOffset));

            Point2D origin;
            origin.x = x;
            origin.y = y;

            Point2D size;
            size.x = (short)(x + width);
            size.y = (short)(y + heigth);

            rasterizeRect(origin, size, textureData, color, primitive);
        }

        private void rasterizeRect(Point2D origin, Point2D size, TextureData texture, uint bgrColor, Primitive primitive) {
            int xOrigin = Math.Max(origin.x, drawingAreaLeft);
            int yOrigin = Math.Max(origin.y, drawingAreaTop);
            int width = Math.Min(size.x, drawingAreaRight);
            int height = Math.Min(size.y, drawingAreaBottom);

            int uOrigin = texture.x + (xOrigin - origin.x);
            int vOrigin = texture.y + (yOrigin - origin.y);

            int baseColor = GetRgbColor(bgrColor);

            for (int y = yOrigin, v = vOrigin; y < height; y++, v++) {
                for (int x = xOrigin, u = uOrigin; x < width; x++, u++) {
                    //Check background mask
                    if (checkMaskBeforeDraw) {
                        color0.val = (uint)vram.GetPixelRGB888(x & 0x3FF, y & 0x1FF); //back
                        if (color0.m != 0) continue;
                    }

                    int color = baseColor;

                    if (primitive.isTextured) {
                        //int texel = getTexel(u, v, clut, textureBase, depth);
                        int texel = getTexel(maskTexelAxis(u, preMaskX, postMaskX),maskTexelAxis(v, preMaskY, postMaskY),primitive.clut, primitive.textureBase, primitive.depth);
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
                        color = handleSemiTransp(x, y, color, primitive.semiTransparencyMode);
                    }

                    color |= maskWhileDrawing << 24;

                    vram.SetPixel(x, y, color);
                }

            }
        }

        private void GP0_MemCopyRectVRAMtoVRAM(Span<uint> buffer) {
            pointer++; //Command/Color parameter unused
            uint sourceXY = buffer[pointer++];
            uint destinationXY = buffer[pointer++];
            uint wh = buffer[pointer++];

            ushort sx = (ushort)(sourceXY & 0x3FF);
            ushort sy = (ushort)((sourceXY >> 16) & 0x1FF);

            ushort dx = (ushort)(destinationXY & 0x3FF);
            ushort dy = (ushort)((destinationXY >> 16) & 0x1FF);

            ushort w = (ushort)((((wh & 0xFFFF) - 1) & 0x3FF) + 1);
            ushort h = (ushort)((((wh >> 16) - 1) & 0x1FF) + 1);

            for (int yPos = 0; yPos < h; yPos++) {
                for (int xPos = 0; xPos < w; xPos++) {
                    int color = vram.GetPixelRGB888((sx + xPos) & 0x3FF, (sy + yPos) & 0x1FF);

                    if (checkMaskBeforeDraw) {
                        color0.val = (uint)vram.GetPixelRGB888((dx + xPos) & 0x3FF, (dy + yPos) & 0x1FF);
                        if (color0.m != 0) continue;
                    }

                    color |= maskWhileDrawing << 24;

                    vram.SetPixel((dx + xPos) & 0x3FF, (dy + yPos) & 0x1FF, color);
                }
            }
        }

        private void GP0_MemCopyRectCPUtoVRAM(Span<uint> buffer) { //todo rewrite VRAM coord struct mess
            pointer++; //Command/Color parameter unused
            uint yx = buffer[pointer++];
            uint wh = buffer[pointer++];

            ushort x = (ushort)(yx & 0x3FF);
            ushort y = (ushort)((yx >> 16) & 0x1FF);

            ushort w = (ushort)((((wh & 0xFFFF) - 1) & 0x3FF) + 1);
            ushort h = (ushort)((((wh >> 16) - 1) & 0x1FF) + 1);

            vramTransfer.x = x;
            vramTransfer.y = y;
            vramTransfer.w = w;
            vramTransfer.h = h;
            vramTransfer.origin_x = x;
            vramTransfer.origin_y = y;
            vramTransfer.halfWords = w * h;

            mode = Mode.VRAM;
        }

        private void GP0_MemCopyRectVRAMtoCPU(Span<uint> buffer) {
            pointer++; //Command/Color parameter unused
            uint yx = buffer[pointer++];
            uint wh = buffer[pointer++];

            ushort x = (ushort)(yx & 0x3FF);
            ushort y = (ushort)((yx >> 16) & 0x1FF);

            ushort w = (ushort)((((wh & 0xFFFF) - 1) & 0x3FF) + 1);
            ushort h = (ushort)((((wh >> 16) - 1) & 0x1FF) + 1);

            vramTransfer.x = x;
            vramTransfer.y = y;
            vramTransfer.w = w;
            vramTransfer.h = h;
            vramTransfer.origin_x = x;
            vramTransfer.origin_y = y;
            vramTransfer.halfWords = w * h;

            isReadyToSendVRAMToCPU = true;
            isReadyToReceiveDMABlock = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int maskTexelAxis(int axis, int preMaskAxis, int postMaskAxis) {
            return axis & 0xFF & preMaskAxis | postMaskAxis;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int getTexel(int x, int y, Point2D clut, Point2D textureBase, int depth) {
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
            ushort index = vram1555.GetPixel(x / 4 + textureBase.x, y + textureBase.y);
            int p = (index >> (x & 3) * 4) & 0xF;
            return vram.GetPixelRGB888(clut.x + p, clut.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int get8bppTexel(int x, int y, Point2D clut, Point2D textureBase) {
            ushort index = vram1555.GetPixel(x / 2 + textureBase.x, y + textureBase.y);
            int p = (index >> (x & 1) * 8) & 0xFF;
            return vram.GetPixelRGB888(clut.x + p, clut.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int get16bppTexel(int x, int y, Point2D textureBase) {
            return vram.GetPixelRGB888(x + textureBase.x, y + textureBase.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int orient2d(Point2D a, Point2D b, Point2D c) {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        private void GP0_E1_SetDrawMode(uint val) {
            textureXBase = (byte)(val & 0xF);
            textureYBase = (byte)((val >> 4) & 0x1);
            transparencyMode = (byte)((val >> 5) & 0x3);
            textureDepth = (byte)((val >> 7) & 0x3);
            isDithered = ((val >> 9) & 0x1) != 0;
            isDrawingToDisplayAllowed = ((val >> 10) & 0x1) != 0;
            isTextureDisabled = isTextureDisabledAllowed && ((val >> 11) & 0x1) != 0;
            isTexturedRectangleXFlipped = ((val >> 12) & 0x1) != 0;
            isTexturedRectangleYFlipped = ((val >> 13) & 0x1) != 0;

            //Console.WriteLine("[GPU] [GP0] DrawMode ");
        }

        private void GP0_E2_SetTextureWindow(uint val) {
            uint bits = val & 0xFF_FFFF;

            if (bits == textureWindowBits) return;

            textureWindowBits = bits;

            byte textureWindowMaskX = (byte)(val & 0x1F);
            byte textureWindowMaskY = (byte)((val >> 5) & 0x1F);
            byte textureWindowOffsetX = (byte)((val >> 10) & 0x1F);
            byte textureWindowOffsetY = (byte)((val >> 15) & 0x1F);

            preMaskX = ~(textureWindowMaskX * 8);
            preMaskY = ~(textureWindowMaskY * 8);
            postMaskX = (textureWindowOffsetX & textureWindowMaskX) * 8;
            postMaskY = (textureWindowOffsetY & textureWindowMaskY) * 8;
        }

        private void GP0_E3_SetDrawingAreaTopLeft(uint val) {
            drawingAreaTop = (ushort)((val >> 10) & 0x1FF);
            drawingAreaLeft = (ushort)(val & 0x3FF);
        }

        private void GP0_E4_SetDrawingAreaBottomRight(uint val) {
            drawingAreaBottom = (ushort)((val >> 10) & 0x1FF);
            drawingAreaRight = (ushort)(val & 0x3FF);
        }

        private void GP0_E5_SetDrawingOffset(uint val) {
            drawingXOffset = signed11bit(val & 0x7FF);
            drawingYOffset = signed11bit((val >> 11) & 0x7FF);
        }

        private void GP0_E6_SetMaskBit(uint val) {
            maskWhileDrawing = (int)(val & 0x1);
            checkMaskBeforeDraw = (val & 0x2) != 0;
        }

        public void writeGP1(uint value) {
            //Console.WriteLine($"[GPU] GP1 Write Value: {value:x8}");
            uint opcode = value >> 24;
            switch (opcode) {
                case 0x00: GP1_00_ResetGPU(); break;
                case 0x01: GP1_01_ResetCommandBuffer(); break;
                case 0x02: GP1_02_AckGPUInterrupt(); break;
                case 0x03: GP1_03_DisplayEnable(value); break;
                case 0x04: GP1_04_DMADirection(value); break;
                case 0x05: GP1_05_DisplayVRAMStart(value); break;
                case 0x06: GP1_06_DisplayHorizontalRange(value); break;
                case 0x07: GP1_07_DisplayVerticalRange(value); break;
                case 0x08: GP1_08_DisplayMode(value); break;
                case 0x09: GP1_09_TextureDisable(value); break;
                case uint _ when opcode >= 0x10 && opcode <= 0x1F:
                    GP1_GPUInfo(value); break;
                default: Console.WriteLine("[GPU] Unsupported GP1 Command " + opcode.ToString("x8")); Console.ReadLine(); break;
            }
        }

        private void GP1_00_ResetGPU() {
            GP1_01_ResetCommandBuffer();
            GP1_02_AckGPUInterrupt();
            GP1_03_DisplayEnable(1);
            GP1_04_DMADirection(0);
            GP1_05_DisplayVRAMStart(0);
            GP1_06_DisplayHorizontalRange(0xC00200);
            GP1_07_DisplayVerticalRange(0x100010);
            GP1_08_DisplayMode(0);

            GP0_E1_SetDrawMode(0);
            GP0_E2_SetTextureWindow(0);
            GP0_E3_SetDrawingAreaTopLeft(0);
            GP0_E4_SetDrawingAreaBottomRight(0);
            GP0_E5_SetDrawingOffset(0);
            GP0_E6_SetMaskBit(0);
        }

        private void GP1_01_ResetCommandBuffer() => pointer = 0;

        private void GP1_02_AckGPUInterrupt() => isInterruptRequested = false;

        private void GP1_03_DisplayEnable(uint value) => isDisplayDisabled = (value & 1) != 0;

        private void GP1_04_DMADirection(uint value) {
            dmaDirection = (byte)(value & 0x3);

            isDmaRequest = dmaDirection switch {
                0 => false,
                1 => isReadyToReceiveDMABlock,
                2 => isReadyToReceiveDMABlock,
                3 => isReadyToSendVRAMToCPU,
                _ => false,
            };
        }


        private void GP1_05_DisplayVRAMStart(uint value) {
            displayVRAMXStart = (ushort)(value & 0x3FE);
            displayVRAMYStart = (ushort)((value >> 10) & 0x1FE);

            window.SetVRAMStart(displayVRAMXStart, displayVRAMYStart);
        }

        private void GP1_06_DisplayHorizontalRange(uint value) {
            displayX1 = (ushort)(value & 0xFFF);
            displayX2 = (ushort)((value >> 12) & 0xFFF);

            window.SetHorizontalRange(displayX1, displayX2);
        }

        private void GP1_07_DisplayVerticalRange(uint value) {
            displayY1 = (ushort)(value & 0x3FF);
            displayY2 = (ushort)((value >> 10) & 0x3FF);

            window.SetVerticalRange(displayY1, displayY2);
        }

        private void GP1_08_DisplayMode(uint value) {
            horizontalResolution1 = (byte)(value & 0x3);
            isVerticalResolution480 = (value & 0x4) != 0;
            isPal = (value & 0x8) != 0;
            is24BitDepth = (value & 0x10) != 0;
            isVerticalInterlace = (value & 0x20) != 0;
            horizontalResolution2 = (byte)((value & 0x40) >> 6);
            isReverseFlag = (value & 0x80) != 0;

            isInterlaceField = isVerticalInterlace;

            horizontalTiming = isPal ? 3406 : 3413;
            verticalTiming = isPal ? 314 : 263;

            int horizontalRes = resolutions[horizontalResolution2 << 2 | horizontalResolution1];
            int verticalRes = isVerticalResolution480 ? 480 : 240;

            window.SetDisplayMode(horizontalRes, verticalRes, is24BitDepth);
        }

        private void GP1_09_TextureDisable(uint value) => isTextureDisabledAllowed = (value & 0x1) != 0;

        private void GP1_GPUInfo(uint value) {
            uint info = value & 0xF;
            switch (info) {
                case 0x2: GPUREAD = textureWindowBits; break;
                case 0x3: GPUREAD = (uint)(drawingAreaTop << 10 | drawingAreaLeft); break;
                case 0x4: GPUREAD = (uint)(drawingAreaBottom << 10 | drawingAreaRight); break;
                case 0x5: GPUREAD = (uint)(drawingYOffset << 11 | (ushort)drawingXOffset); break;
                case 0x7: GPUREAD = 2; break;
                case 0x8: GPUREAD = 0; break;
                default: Console.WriteLine("[GPU] GP1 Unhandled GetInfo: " + info.ToString("x8")); break;
            }
        }

        private uint getTexpageFromGPU() {
            uint texpage = 0;

            texpage |= (isTexturedRectangleYFlipped ? 1u : 0) << 13;
            texpage |= (isTexturedRectangleXFlipped ? 1u : 0) << 12;
            texpage |= (isTextureDisabled ? 1u : 0) << 11;
            texpage |= (isDrawingToDisplayAllowed ? 1u : 0) << 10;
            texpage |= (isDithered ? 1u : 0) << 9;
            texpage |= (uint)(textureDepth << 7);
            texpage |= (uint)(transparencyMode << 5);
            texpage |= (uint)(textureYBase << 4);
            texpage |= textureXBase;

            return texpage;
        }

        private int handleSemiTransp(int x, int y, int color, int semiTranspMode) {
            color0.val = (uint)vram.GetPixelRGB888(x, y); //back
            color1.val = (uint)color; //front
            switch (semiTranspMode) {
                case 0: //0.5 x B + 0.5 x F    ;aka B/2+F/2
                    color1.r = (byte)((color0.r + color1.r) >> 1);
                    color1.g = (byte)((color0.g + color1.g) >> 1);
                    color1.b = (byte)((color0.b + color1.b) >> 1);
                    break;
                case 1://1.0 x B + 1.0 x F    ;aka B+F
                    color1.r = clampToFF(color0.r + color1.r);
                    color1.g = clampToFF(color0.g + color1.g);
                    color1.b = clampToFF(color0.b + color1.b);
                    break;
                case 2: //1.0 x B - 1.0 x F    ;aka B-F
                    color1.r = clampToZero(color0.r - color1.r);
                    color1.g = clampToZero(color0.g - color1.g);
                    color1.b = clampToZero(color0.b - color1.b);
                    break;
                case 3: //1.0 x B +0.25 x F    ;aka B+F/4
                    color1.r = clampToFF(color0.r + (color1.r >> 2));
                    color1.g = clampToFF(color0.g + (color1.g >> 2));
                    color1.b = clampToFF(color0.b + (color1.b >> 2));
                    break;
            }//actually doing RGB calcs on BGR struct...
            return (int)color1.val;
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
        private static bool isTopLeft(Point2D a, Point2D b) => a.y == b.y && b.x > a.x || b.y < a.y;

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
        private static int interpolate(int w0, int w1, int w2, int t0, int t1, int t2, int area) {
            //https://codeplea.com/triangular-interpolation
            return (t0 * w0 + t1 * w1 + t2 * w2) / area;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short signed11bit(uint n) {
            return (short)(((int)n << 21) >> 21);
        }

        //This is only needed for the Direct GP0 commands as the command number needs to be
        //known ahead of the first command on queue.
        private static ReadOnlySpan<byte> CommandSizeTable => new byte[] {
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
