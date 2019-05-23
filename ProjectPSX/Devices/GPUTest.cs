using System;
using System.Collections.Generic;

namespace ProjectPSX.Devices {

    public class GPUTest : Device {
        //private uint GP0;       //1F801810h-Write GP0    Send GP0 Commands/Packets(Rendering and VRAM Access)
        //private uint GP1;       //1F801814h-Write GP1    Send GP1 Commands(Display Control) (and DMA Control)
        private uint GPUREAD;     //1F801810h-Read GPUREAD Receive responses to GP0(C0h) and GP1(10h) commands
        //private uint GPUSTAT/* = 0x1c00_0000*/;//temp value to force DMA   //1F801814h-Read GPUSTAT Receive GPU Status Register

        //private byte[] VRAM;    //todo

        //private Renderer renderer;

        private Command command;
        private int commandSize;
        private Queue<uint> commandBuffer = new Queue<uint>(16);

        private Window window;

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

        public struct Point2D {
            public int x, y;

            public Point2D(uint val) {
                x = (short)(val & 0xFFFF);
                y = (short)((val >> 16) & 0xFFFF);
            }

            public Point2D(int x, int y) {
                this.x = x;
                this.y = y;
            }

        }

        public struct TextureData {
            public int x, y;

            public TextureData(uint val) {
                x = (byte)(val & 0xFF);
                y = (byte)((val >> 8) & 0xFF);
            }

            public TextureData(int x, int y) {
                this.x = x;
                this.y = y;
            }

        }

        public struct Color {
            public byte m, r, g, b;
            public Color(uint val) { //psx colors are bgr
                m = (byte)((val & 0xFF00_0000) >> 24);
                r = (byte)((val & 0x0000_00FF));
                g = (byte)((val & 0x0000_FF00) >> 8);
                b = (byte)((val & 0x00FF_0000) >> 16);
            }
        }

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

        private int timer;

        public GPUTest() {
            mode = Mode.COMMAND;
            GP1_ResetGPU();
        }

        public bool tick(int cycles) {
            timer += cycles;
            if (timer >= 564480) {
                //Console.WriteLine("[GPU] Request Interrupt 0x1 VBLANK");
                timer -= 564480;
                window.update();
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

        public void writeGP0(uint value) {
            switch (mode) {
                case Mode.COMMAND: ExecuteGP0Command(value); break;
                case Mode.VRAM: WriteToVRAM(value); break;
                default: break;
            }
        }

        private void WriteToVRAM(uint value) { //todo rewrite this mess
            ushort val1 = (ushort)(value >> 16);
            ushort val2 = (ushort)(value & 0xFFFF);

            drawVRAMPixel(val2);
            drawVRAMPixel(val1);

            vram_coord.size--;
            if (vram_coord.size == 0) {
                mode = Mode.COMMAND;
            }
        }

        private uint readFromVRAM() {
            ushort pixel0 = window.VRAM.GetPixel16(vram_coord.x++ & 0x3FF, vram_coord.y & 0x1FF);
            ushort pixel1 = window.VRAM.GetPixel16(vram_coord.x++ & 0x3FF, vram_coord.y & 0x1FF);
            if (vram_coord.x == vram_coord.origin_x + vram_coord.w) {
                vram_coord.x -= vram_coord.w;
                vram_coord.y++;
            }
            vram_coord.size -= 2;
            return (uint)(pixel1 << 16 | pixel0);
        }

        private void drawVRAMPixel(ushort val) {
            window.VRAM.SetPixel(vram_coord.x++ & 0x3FF, vram_coord.y & 0x1FF, get555Color(val));
            if (vram_coord.x == vram_coord.origin_x + vram_coord.w) {
                vram_coord.x -= vram_coord.w;
                vram_coord.y++;
            }
        }


        private int get555Color(ushort val) {
            byte m = (byte)(val >> 15);
            byte r = (byte)((val & 0x1F) << 3);
            byte g = (byte)(((val >> 5) & 0x1F) << 3);
            byte b = (byte)(((val >> 10) & 0x1F) << 3);

            return (m << 24 | r << 16 | g << 8 | b);
        }

        private void ExecuteGP0Command(uint value) {
            if (commandSize == 0) {
                uint opcode = (value >> 24) & 0xFF;
                command = decode(opcode);
                commandSize = CommandSize[opcode];
                //Console.WriteLine("[GPU] GP0 COMMAND: {0} size: {1}", value.ToString("x8"), commandSize);
            }

            commandBuffer.Enqueue(value);
            // Console.WriteLine("[GPU] GP0: {0} buffer: {1}", value.ToString("x8"), commandBuffer.Count);

            if (commandBuffer.Count == commandSize || commandSize == 32 && /*commandBuffer.Count > 3 &&*/ value == 0x5555_5555) {
                commandSize = 0;
                command();
                //window.update(); //debug
                //Console.ReadLine();
            }
        }

        private Command decode(uint opcode) {
            //Console.WriteLine("GP0 Command: " + opcode.ToString("x2"));
            switch (opcode) {
                case 0x00: return GP0_NOP;
                case 0x01: return GP0_MemClearCache;
                case 0x02: return GP0_FillRectVRAM;
                case 0x1F: return GP0_InterruptRequest;

                case 0xE1: return GP0_SetDrawMode;
                case 0xE2: return GP0_SetTextureWindow;
                case 0xE3: return GP0_SetDrawingAreaTopLeft;
                case 0xE4: return GP0_SetDrawingAreaBottomRight;
                case 0xE5: return GP0_SetDrawingOffset;
                case 0xE6: return GP0_SetMaskBit;

                case uint polygon when opcode >= 0x20 && opcode <= 0x3F:
                    return GP0_RenderPolygon;
                case uint opaqueLine when opcode >= 0x40 && opcode <= 0x5F:
                    return GP0_RenderLine;
                case uint rect when opcode >= 0x60 && opcode <= 0x7F:
                    return GP0_RenderRectangle;
                case uint vramToVram when opcode >= 0x80 && opcode <= 0x9F:
                    return GP0_MemCopyRectVRAMtoVRAM;
                case uint cpuToVram when opcode >= 0xA0 && opcode <= 0xBF:
                    return GP0_MemCopyRectCPUtoVRAM;
                case uint vramToCpu when opcode >= 0xC0 && opcode <= 0xDF:
                    return GP0_MemCopyRectVRAMtoCPU;

                case uint nop when (opcode >= 0x3 && opcode <= 0x1E) || opcode == 0xE0 || opcode >= 0xE7 && opcode <= 0xEF:
                    return GP0_NOP;

                default: Console.WriteLine("[GPU] Unsupported GP0 Command " + opcode.ToString("x8")); Console.ReadLine(); return GP0_NOP;// throw new NotImplementedException();
            }
        }

        private void GP0_InterruptRequest() {
            uint command = commandBuffer.Dequeue();
            isInterruptRequested = true;
        }

        private void GP0_RenderLine() {
            //Console.WriteLine("size " + commandBuffer.Count);
            uint command = commandBuffer.Dequeue();
            uint color1 = command & 0xFFFFFF;
            uint color2 = color1;

            bool isPoly = (command & (1 << 27)) != 0;
            bool isShaded = (command & (1 << 28)) != 0;
            bool isTransparent = (command & (1 << 25)) != 0;

            uint v1 = commandBuffer.Dequeue();

            if (isShaded) color2 = commandBuffer.Dequeue();
            uint v2 = commandBuffer.Dequeue();

            rasterizeLine(v1, v2, color1, color2);

            if (!isPoly) return;

            while (commandBuffer.Peek() != 0x5555_5555) {
                Console.WriteLine("DOING ANOTHER LINE");
                color1 = color2;
                if (isShaded) color2 = commandBuffer.Dequeue();
                v1 = v2;
                v2 = commandBuffer.Dequeue();
                rasterizeLine(v1, v2, color1, color2);
            }

            commandBuffer.Dequeue(); // discard 5555_5555 termination (we need to rewrite all this from the GP0...)
        }

        private void rasterizeLine(uint v1, uint v2, uint color1, uint color2) {
            short x = (short)((v1 & 0xFFFF) & 0x3FF); //pending sign extend
            short y = (short)((v1 >> 16) & 0x1FF);

            short x2 = (short)((v2 & 0xFFFF) & 0x3FF);
            short y2 = (short)((v2 >> 16) & 0x1FF);

            x += (short)drawingXOffset;
            y += (short)drawingYOffset;

            x2 += (short)drawingXOffset;
            y2 += (short)drawingYOffset;

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
                //Console.WriteLine("XY " + x + " " + y);
                //Console.WriteLine("Longest" + longest);

                float l = (float)longest;
                float index = (float)(i);
                float ratio = index / l;
                // Console.WriteLine(ratio);
                int color = interpolate(color1, color2, ratio);

                if (x >= drawingAreaLeft && x < drawingAreaRight && y >= drawingAreaTop && y < drawingAreaBottom) //why boundingbox dosnt work???
                    window.VRAM.SetPixel(x, y, color);

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

        private int interpolate(uint color1, uint color2, float ratio) {
            Color c1 = new Color(color1);
            Color c2 = new Color(color2);

            byte r = (byte)(c2.r * ratio + c1.r * (1 - ratio));
            byte g = (byte)(c2.g * ratio + c1.g * (1 - ratio));
            byte b = (byte)(c2.b * ratio + c1.b * (1 - ratio));

            return (r << 16 | g << 8 | b);
        }

        public void GP0_RenderPolygon() {
            uint command = commandBuffer.Peek();

            bool isQuad = (command & (1 << 27)) != 0;
            bool isShaded = (command & (1 << 28)) != 0;
            bool isTextured = (command & (1 << 26)) != 0;
            bool isTransparent = (command & (1 << 25)) != 0; //todo unhandled still!

            type = isShaded ? Type.shaded : Type.opaque;
            if (isTextured) type = Type.textured;

            int vertexN = isQuad ? 4 : 3;

            Point2D[] v = new Point2D[vertexN];
            TextureData[] t = new TextureData[vertexN];
            uint[] c = new uint[vertexN];

            if (!isShaded) {
                uint color = commandBuffer.Dequeue();
                c[0] = color; //triangle 1 opaque color
                c[1] = color; //triangle 2 opaque color
            }

            uint palette = 0;
            uint texpage = 0;

            for (int i = 0; i < vertexN; i++) {
                if (isShaded) c[i] = commandBuffer.Dequeue();

                v[i] = new Point2D(commandBuffer.Dequeue());

                if (isTextured) {
                    uint textureData = commandBuffer.Dequeue();
                    t[i] = new TextureData(textureData);
                    if (i == 0) {
                        palette = textureData >> 16 & 0xFFFF;
                    } else if (i == 1) {
                        texpage = textureData >> 16 & 0xFFFF;
                    }
                }
            }

            for (int i = 0; i < v.Length; i++) {
                v[i].x += drawingXOffset;
                v[i].y += drawingYOffset;
            }

            rasterizeTri(v[0], v[1], v[2], t[0], t[1], t[2], c[0], c[1], c[2], palette, texpage, type);
            if (isQuad) rasterizeTri(v[1], v[2], v[3], t[1], t[2], t[3], c[1], c[2], c[3], palette, texpage, type);
        }

        private void rasterizeTri(Point2D v0, Point2D v1, Point2D v2, TextureData t0, TextureData t1, TextureData t2, uint c0, uint c1, uint c2, uint palette, uint texpage, Type type) {

            int area = orient2d(v0, v1, v2);

            if (area == 0) {
                return;
            } else if (area < 0) {
                //Console.WriteLine("AREA < 0");
                //Console.ReadLine();
                Point2D vertexAux = v1;
                v1 = v2;
                v2 = vertexAux;
                TextureData textureaAux = t1;
                t1 = t2;
                t2 = textureaAux;
                uint colorAux = c1;
                c1 = c2;
                c2 = colorAux;
            }

            (Point2D min, Point2D max) = boundingBox(v0, v1, v2);

            int A01 = v0.y - v1.y, B01 = v1.x - v0.x;
            int A12 = v1.y - v2.y, B12 = v2.x - v1.x;
            int A20 = v2.y - v0.y, B20 = v0.x - v2.x;

            int w0_row = orient2d(v1, v2, min);
            int w1_row = orient2d(v2, v0, min);
            int w2_row = orient2d(v0, v1, min);

            // Rasterize
            for (int y = min.y; y < max.y; y++) {
                // Barycentric coordinates at start of row
                int w0 = w0_row;
                int w1 = w1_row;
                int w2 = w2_row;

                for (int x = min.x; x < max.x; x++) {
                    // If p is on or inside all edges, render pixel.
                    if ((w0 | w1 | w2) >= 0) {
                        int col;
                        switch (type) {
                            case Type.opaque:
                                col = getRGBColor(c0);
                                window.VRAM.SetPixel((x & 0x3FF), (y & 0x1FF), col);
                                break;
                            case Type.shaded:
                                col = getShadedColor(w0, w1, w2, c0, c1, c2);
                                window.VRAM.SetPixel((x & 0x3FF), (y & 0x1FF), col);
                                break;
                            case Type.textured:
                                col = getTextureColor(w0, w1, w2, t0, t1, t2, palette, texpage);
                                if (col != 0)
                                    window.VRAM.SetPixel((x & 0x3FF), (y & 0x1FF), col);
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

        private int getRGBColor(uint value) {
            byte m = (byte)((value & 0xFF00_0000) >> 24);
            byte r = (byte)(value & 0x0000_00FF);
            byte g = (byte)((value & 0x0000_FF00) >> 8);
            byte b = (byte)((value & 0x00FF_0000) >> 16);

            return (m << 24 | r << 16 | g << 8 | b);
        }

        private void GP0_FillRectVRAM() {
            uint command = commandBuffer.Dequeue();
            int r = (int)(command >> 0) & 0xFF;
            int g = (int)(command >> 8) & 0xFF;
            int b = (int)(command >> 16) & 0xFF;

            uint vertex = commandBuffer.Dequeue();
            int x = (int)(vertex & 0xFFFF);
            int y = (int)(vertex >> 16) & 0xFFFF;

            uint length = commandBuffer.Dequeue();
            uint width = length & 0xFFFF;
            uint heigth = (length >> 16) & 0xFFFF;

            for (int yPos = y; yPos < heigth + y; yPos++) {
                for (int xPos = x; xPos < width + x; xPos++) {
                    window.VRAM.SetPixel(xPos & 0x3FF, yPos & 0x1FF, (r << 16 | g << 8 | b));
                    window.VRAM.SetPixel(xPos & 0x3FF, yPos & 0x1FF, ((r << 16) + 1 | g << 8 + 1 | b + 1));
                }
            }
        }

        private void GP0_RenderRectangle() {
            //1st Color+Command(CcBbGgRrh)
            //2nd Vertex(YyyyXxxxh)
            //3rd Texcoord+Palette(ClutYyXxh)(for 4bpp Textures Xxh must be even!) //Only textured
            //4rd (3rd non textured) Width + Height(YsizXsizh)(variable opcode only)(max 1023x511)
            uint command = commandBuffer.Dequeue();
            uint color = command & 0xFFFFFF;
            uint opcode = (command >> 24) & 0xFF;

            bool isShaded = (command & (1 << 28)) != 0;
            bool isTextured = (command & (1 << 26)) != 0;
            bool isTransparent = (command & (1 << 25)) != 0; //todo unhandled still!

            type = Type.opaque;
            if (isTextured) type = Type.textured;

            uint vertex = commandBuffer.Dequeue();
            short xo = (short)(vertex & 0xFFFF);
            short yo = (short)((vertex >> 16) & 0xFFFF);

            uint[] c = new uint[4];
            c[0] = color;
            c[1] = color;

            ushort palette = 0;
            short textureX = 0;
            short textureY = 0;
            if (isTextured) {
                uint texture = commandBuffer.Dequeue();
                palette = (ushort)((texture >> 16) & 0xFFFF);
                textureX = (short)(texture & 0xFF);
                textureY = (short)((texture >> 8) & 0xFF);
            }

            short width = 0;
            short heigth = 0;

            switch ((opcode & 0x18) >> 3) {
                case 0x0:
                    uint hw = commandBuffer.Dequeue();
                    width = (short)(hw & 0xFFFF);// width -= 1;
                    heigth = (short)((hw >> 16) & 0xFFFF);// heigth -= 1;
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

            //
            // ((xo & 0x400) != 0)
            //  xo = (short)((ushort)xo | 0xfc00);
            // ((yo & 0x400) != 0)
            //  xo = (short)((ushort)xo | 0xfc00);
            //
            //if ((width & 0x400) != 0)
            //    width = (short)((ushort)width | 0xfc00);
            //if ((heigth & 0x400) != 0)
            //    heigth = (short)((ushort)heigth | 0xfc00);
            //

            int y = yo + drawingYOffset; //this could be refactored from everywhere to the actual pixed draw ???
            int x = xo + drawingXOffset;

            Point2D v0 = new Point2D(x, y);
            Point2D v1 = new Point2D(x + width, y);
            Point2D v2 = new Point2D(x, y + heigth);
            Point2D v3 = new Point2D(x + width, y + heigth);

            TextureData t0 = new TextureData(textureX, textureY);
            TextureData t1 = new TextureData(textureX + width, textureY);
            TextureData t2 = new TextureData(textureX, textureY + heigth);
            TextureData t3 = new TextureData(textureX + width, textureY + heigth);

            rasterizeTri(v0, v1, v2, t0, t1, t2, c[0], c[1], c[2], palette, getTexpageFromGPU(), type);
            rasterizeTri(v1, v2, v3, t1, t2, t3, c[1], c[2], c[3], palette, getTexpageFromGPU(), type);

            //Console.WriteLine("after offset x" + x + " y" + y);
            //Console.ReadLine();
        }

        private void GP0_MemCopyRectVRAMtoCPU() {
            uint command = commandBuffer.Dequeue();
            uint yx = commandBuffer.Dequeue();
            uint wh = commandBuffer.Dequeue();

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

        private void GP0_MemCopyRectCPUtoVRAM() { //todo rewrite this vram mess
            uint command = commandBuffer.Dequeue();
            uint yx = commandBuffer.Dequeue();
            uint wh = commandBuffer.Dequeue();

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
            uint command = commandBuffer.Dequeue();
            uint sourceXY = commandBuffer.Dequeue();
            uint destinationXY = commandBuffer.Dequeue();
            uint wh = commandBuffer.Dequeue();

            short sx = (short)(sourceXY & 0xFFFF);
            short sy = (short)(sourceXY >> 16);

            short dx = (short)(destinationXY & 0xFFFF);
            short dy = (short)(destinationXY >> 16);

            short w = (short)(wh & 0xFFFF);
            short h = (short)(wh >> 16);

            for (int yPos = 0; yPos < h; yPos++) {
                for (int xPos = 0; xPos < w; xPos++) {
                    int color = window.VRAM.GetPixel((sx + xPos) & 0x3FF, (sy + yPos) & 0x1FF);
                    window.VRAM.SetPixel((dx + xPos) & 0x3FF, (dy + yPos) & 0x1FF, color);
                }
            }
        }


        private void GP0_MemClearCache() {
            commandBuffer.Clear();
            //throw new NotImplementedException();
        }

        private int getShadedColor(int w0, int w1, int w2, uint color0, uint color1, uint color2) {
            //https://codeplea.com/triangular-interpolation
            float w = w0 + w1 + w2;
            Color c0 = new Color(color0);
            Color c1 = new Color(color1);
            Color c2 = new Color(color2);

            byte r = (byte)((c0.r * w0 + c1.r * w1 + c2.r * w2) / w);
            byte g = (byte)((c0.g * w0 + c1.g * w1 + c2.g * w2) / w);
            byte b = (byte)((c0.b * w0 + c1.b * w1 + c2.b * w2) / w);

            return (r << 16 | g << 8 | b);
        }

        private int getTextureColor(int w0, int w1, int w2, TextureData t0, TextureData t1, TextureData t2, uint palette, uint texpage) {
            int type = (int)(texpage >> 7) & 0x3;

            switch (type) {
                case 0: return get4bppTexel(w0, w1, w2, t0, t1, t2, palette, texpage);
                case 1: return get8bppTexel(w0, w1, w2, t0, t1, t2, palette, texpage);
                case 2: return get16bppTexel(w0, w1, w2, t0, t1, t2, palette, texpage);
                default: Console.WriteLine("CLUT ERROR WAS " + textureDepth); Console.ReadLine(); return 0x00FF00FF;
            }
        }

        private int get8bppTexel(int w0, int w1, int w2, TextureData t0, TextureData t1, TextureData t2, uint palette, uint texpage) {
            int clutX = (int)(palette & 0x3f) * 16;
            int clutY = (int)(palette >> 6) & 0x1FF;

            //https://codeplea.com/triangular-interpolation
            int XBase = (int)(texpage & 0xF) * 64;
            int YBase = (int)((texpage >> 4) & 0x1) * 256;

            float w = w0 + w1 + w2;
            int x = (int)((t0.x * w0 + t1.x * w1 + t2.x * w2) / w);
            int y = (int)((t0.y * w0 + t1.y * w1 + t2.y * w2) / w);

            x %= 256;
            y %= 256;

            // Texture masking
            // texel = (texel AND(NOT(Mask * 8))) OR((Offset AND Mask) * 8)
            x = (x & ~(textureWindowMaskX * 8)) | ((textureWindowOffsetX & textureWindowMaskX) * 8);
            y = (y & ~(textureWindowMaskY * 8)) | ((textureWindowOffsetY & textureWindowMaskY) * 8);

            //window.VRAM.SetPixel(x / 2 + XBase, y + YBase, 0x000000FF);
            ushort index = window.VRAM.GetPixel16(x / 2 + XBase, y + YBase);

            byte p = 0;
            byte pix = (byte)(x % 2);
            switch (pix) {
                case 0: p = (byte)(index & 0xFF); break;
                case 1: p = (byte)((index >> 8) & 0xFF); break;
                default: Console.WriteLine(pix); Console.ReadLine(); break;
            }
            return window.VRAM.GetPixel(clutX + p, clutY);
        }

        private int get16bppTexel(int w0, int w1, int w2, TextureData t0, TextureData t1, TextureData t2, uint palette, uint texpage) {
            int XBase = (int)(texpage & 0xF) * 64;
            int YBase = (int)((texpage >> 4) & 0x1) * 256;

            float w = w0 + w1 + w2;
            float x = (t0.x * w0 + t1.x * w1 + t2.x * w2) / w;
            float y = (t0.y * w0 + t1.y * w1 + t2.y * w2) / w;

            return window.VRAM.GetPixel((int)(x + XBase), (int)y + YBase);
        }

        private int get4bppTexel(int w0, int w1, int w2, TextureData t0, TextureData t1, TextureData t2, uint palette, uint texpage) {
            uint clutX = (palette & 0x3f) * 16;
            uint clutY = ((palette >> 6) & 0x1FF);

            //https://codeplea.com/triangular-interpolation
            int XBase = (int)(texpage & 0xF) * 64;
            int YBase = (int)((texpage >> 4) & 0x1) * 256;

            float w = w0 + w1 + w2;
            int x = (int)((t0.x * w0 + t1.x * w1 + t2.x * w2) / w);
            int y = (int)((t0.y * w0 + t1.y * w1 + t2.y * w2) / w);

            //Console.WriteLine("clutX " + clutX +  " clutY " + clutY);
            //Console.WriteLine("xBase " + XBase + " yBase " + YBase);
            //Console.WriteLine("t0.x " + t0.x + " t0.y" + t0.y);
            //Console.WriteLine("t1.x " + t1.x + " t1.y" + t1.y);
            //Console.WriteLine("t2.x " + t2.x + " t1.y" + t2.y);

            x %= 256;
            y %= 256;

            // Texture masking
            // texel = (texel AND(NOT(Mask * 8))) OR((Offset AND Mask) * 8)
            x = (x & ~(textureWindowMaskX * 8)) | ((textureWindowOffsetX & textureWindowMaskX) * 8);
            y = (y & ~(textureWindowMaskY * 8)) | ((textureWindowOffsetY & textureWindowMaskY) * 8);

            //window.VRAM.SetPixel(x / 4 + XBase, y + YBase, 0x000000FF);
            ushort index = window.VRAM.GetPixel16(x / 4 + XBase, y + YBase);

            byte p = 0;
            byte pix = (byte)(x % 4);
            switch (pix) {
                case 0: p = (byte)(index & 0xF); break;
                case 1: p = (byte)((index >> 4) & 0xF); break;
                case 2: p = (byte)((index >> 8) & 0xF); break;
                case 3: p = (byte)((index >> 12) & 0xF); break;
                default: Console.WriteLine(pix); Console.ReadLine(); break;
            }

            int xx = (int)clutX + p;
            int yy = (int)clutY;

            return window.VRAM.GetPixel(xx, yy);
        }
        private int orient2d(Point2D a, Point2D b, Point2D c) {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        private (Point2D min, Point2D max) boundingBox(Point2D p0, Point2D p1, Point2D p2) {
            Point2D min = new Point2D();
            Point2D max = new Point2D();

            int minX = Math.Min(p0.x, Math.Min(p1.x, p2.x));
            int minY = Math.Min(p0.y, Math.Min(p1.y, p2.y));
            int maxX = Math.Max(p0.x, Math.Max(p1.x, p2.x));
            int maxY = Math.Max(p0.y, Math.Max(p1.y, p2.y));

            min.x = Math.Max(minX, drawingAreaLeft);
            min.y = Math.Max(minY, drawingAreaTop);
            max.x = Math.Min(maxX, drawingAreaRight);
            max.y = Math.Min(maxY, drawingAreaBottom);

            return (min, max);
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
            commandBuffer.Dequeue();
        }

        private void GP0_SetDrawMode() {
            uint val = commandBuffer.Dequeue();

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
            uint val = commandBuffer.Dequeue();

            drawingAreaTop = (ushort)((val >> 10) & 0x1FF);
            drawingAreaLeft = (ushort)(val & 0x3FF);
        }

        private void GP0_SetDrawingAreaBottomRight() {
            uint val = commandBuffer.Dequeue();

            drawingAreaBottom = (ushort)((val >> 10) & 0x1FF);
            drawingAreaRight = (ushort)(val & 0x3FF);
        }

        public void writeGP1(uint value) {
            //Console.WriteLine("[GPU] GP1 Write Value: {0}", value.ToString("x8"));
            ////Console.ReadLine();
            ExecuteGP1Command(value);
        }

        private void ExecuteGP1Command(uint value) {
            uint opcode = (value >> 24) & 0xFF;
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
            commandBuffer.Clear();
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

        private readonly int[] CommandSize = {
        //0  1   2   3   4   5   6   7   8   9   A   B   C   D   E   F
         1,  1,  3,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, //0
         1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, //1
         4,  4,  4,  4,  7,  7,  7,  7,  5,  5,  5,  5,  9,  9,  9,  9, //2
         6,  6,  6,  6,  9,  9,  9,  9,  8,  8,  8,  8, 12, 12, 12, 12, //3
         3,  3,  3,  3,  3,  3,  3,  3, 32, 32, 32, 32, 32, 32, 32, 32, //4
         4,  4,  4,  4,  4,  4,  4,  4, 32, 32, 32, 32, 32, 32, 32, 32, //5
         3,  1,  3,  1,  4,  4,  4,  4,  2,  1,  2,  1,  3,  3,  3,  3, //6
         2,  1,  2,  1,  3,  3,  3,  3,  2,  1,  2,  2,  3,  3,  3,  3, //7
         4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4, //8
         4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4, //9
         3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //A
         3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //B
         3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //C
         3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //D
         1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, //E
         1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, //F
    };
    }
}