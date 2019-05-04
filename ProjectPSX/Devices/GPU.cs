using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProjectPSX.Devices {

    public class GPU : Device {
        //private uint GP0;       //1F801810h-Write GP0    Send GP0 Commands/Packets(Rendering and VRAM Access)
        //private uint GP1;       //1F801814h-Write GP1    Send GP1 Commands(Display Control) (and DMA Control)
        //private uint GPUREAD;   //1F801810h-Read GPUREAD Receive responses to GP0(C0h) and GP1(10h) commands
        //private uint GPUSTAT/* = 0x1c00_0000*/;//temp value to force DMA   //1F801814h-Read GPUSTAT Receive GPU Status Register

        //private byte[] VRAM;    //todo

        //private Renderer renderer;

        private Command command;
        private int size;
        private Queue<uint> commandBuffer = new Queue<uint>();

        private Window window;

        public void setWindow(Window window) {
            this.window = window;
        }
        private enum Mode {
            COMMAND,
            VRAM
        }
        private Mode mode;

        private struct VRAM_Coord {
            public int x, y;
            public int origin_x;
            public ushort w, h;
        }
        private VRAM_Coord vram_coord;

        public struct Point2D {
            public int x, y;

            public Point2D(uint val) {
                x = (int)(val & 0xFFFF);
                y = (int)((val >> 16) & 0xFFFF);
            }

        }

        public struct Color {
            public byte m, r, g, b;
            public Color(int val) {
                m = (byte)((val & 0xFF00_0000) >> 24);
                r = (byte)((val & 0x00FF_0000) >> 16);
                g = (byte)((val & 0x0000_FF00) >> 8);
                b = (byte)((val & 0x0000_00FF));
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

        private uint timer;

        public GPU() {
            mode = Mode.COMMAND;
            GP1_Reset();
        }

        public bool tick(uint cycles) {
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
            //TODO
            //Console.WriteLine("[GPU] LOAD GPUREAD: {0}", 0.ToString("x8"));
            return 0;
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

            size--;
            if (size == 0) {
                mode = Mode.COMMAND;
            }
        }

        private void drawVRAMPixel(ushort val) {
            window.VRAM.SetPixel(vram_coord.x, vram_coord.y, get555Color(val));
            vram_coord.x++;
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

        private int get888Color(uint val) {
            byte m = (byte)((val & 0xFF00_0000) >> 24);
            byte r = (byte)(val & 0x0000_00FF);
            byte g = (byte)((val & 0x0000_FF00) >> 8);
            byte b = (byte)((val & 0x00FF_0000) >> 16);

            return (m << 24 | r << 16 | g << 8 | b);
        }

        private void ExecuteGP0Command(uint value) {
            if (size == 0) {
                uint opcode = (value >> 24) & 0xFF;
                (size, command) = decode(opcode);
            }
            //Console.WriteLine("[GPU] GP0 COMMAND: {0}", value.ToString("x8"));

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
                case 0x02: return (3, GP0_FillRectVRAM);
                case 0x2C: //return (9, GP0_RenderTexturedQuadBlend);
                case 0x2D: return (9, GP0_RenderTexturedQuadBlend);
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

                //case 0x48: return ();

                case 0x60:
                case 0x62: return (3, GP0_RenderMonoRectangle);
                case 0x68:
                case 0x6A:
                case 0x70:
                case 0x72:
                case 0x78:
                case 0x7A: return (2, GP0_RenderMonoRectangle); // todo hardcode return values and rewrite this also semi transp not impl


                default: Console.WriteLine("[GPU] Unsupported Command " + opcode.ToString("x8")); /*Console.ReadLine();*/ return (1, GP0_NOP);// throw new NotImplementedException();
            }
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
            uint lengthX = length & 0xFFFF;
            uint lengthY = (length >> 16) & 0xFFFF;

            for (int yPos = y; yPos < lengthY + y; yPos++) {
                for (int xPos = x; xPos < lengthX + x; xPos++) {
                    window.VRAM.SetPixel(xPos, yPos, (r << 16 | g << 8 | b));
                }
            }
        }

        private void GP0_RenderMonoRectangle() {
            //1st Color+Command(CcBbGgRrh)
            //2nd Vertex(YyyyXxxxh)
            //(3rd) Width + Height(YsizXsizh)(variable size only)(max 1023x511)
            uint command = commandBuffer.Dequeue();
            int r = (int)(command >> 0) & 0xFF;
            int g = (int)(command >> 8) & 0xFF;
            int b = (int)(command >> 16) & 0xFF;
            uint size = (command >> 24) & 0xFF;

            uint vertex = commandBuffer.Dequeue();
            int x = (int)(vertex & 0xFFFF);
            int y = (int)(vertex >> 16) & 0xFFFF;

            uint lengthX = 0;
            uint lengthY = 0;
            switch ((size & 0x18) >> 3) {
                case 0x0:
                    uint variable = commandBuffer.Dequeue();
                    lengthX = variable & 0xFFFF;
                    lengthY = (variable >> 16) & 0xFFFF;
                    break;
                case 0x1:
                    lengthX = 1;
                    lengthY = 1;
                    break;
                case 0x2:
                    lengthX = 8;
                    lengthY = 8;
                    break;
                case 0x3:
                    lengthX = 16;
                    lengthY = 16;
                    break;
                default:
                    Console.WriteLine("INCORRECT LENGTH");
                    break;
            }

            int color = (r << 16 | g << 8 | b);
            for (int yy = 0; yy < lengthY; yy++) {
                for (int xx = 0; xx < lengthX; xx++) {
                    window.VRAM.SetPixel((x/* & 0x3FF*/), (y/* & 0x1FF*/), color);
                }
            }
        }

        private void GP0_RenderTexturedQuadBlend() { //2C
            uint color = commandBuffer.Dequeue() & 0xFFFFFF;

            int quad = 4;
            Point2D[] vertex = new Point2D[quad];
            uint[] texture = new uint[quad];

            for (int i = 0; i < quad; i++) {
                vertex[i] = new Point2D(commandBuffer.Dequeue());
                texture[i] = commandBuffer.Dequeue();
            }

            uint palette = (texture[0] >> 16) & 0xFFFF;
            uint texpage = (texture[1] >> 16) & 0xFFFF;

            rasterizeTexturedTri(vertex[0], vertex[1], vertex[2], texture[0], texture[1], texture[2], color, palette, texpage);
            rasterizeTexturedTri(vertex[1], vertex[2], vertex[3], texture[1], texture[2], texture[3], color, palette, texpage);
        }

        private void rasterizeTexturedTri(Point2D v0, Point2D v1, Point2D v2, uint t0, uint t1, uint t2, uint color, uint palette, uint texpage) {

            int area = orient2d(v0, v1, v2);
            if (area < 0) {
                Point2D aux = v1;
                v1 = v2;
                v2 = aux;
                uint taux = t1;
                t1 = t2;
                t2 = taux;
            }

            (Point2D min, Point2D max) = boundingBox(v0, v1, v2);

            int A01 = v0.y - v1.y, B01 = v1.x - v0.x;
            int A12 = v1.y - v2.y, B12 = v2.x - v1.x;
            int A20 = v2.y - v0.y, B20 = v0.x - v2.x;

            int w0_row = orient2d(v1, v2, min);
            int w1_row = orient2d(v2, v0, min);
            int w2_row = orient2d(v0, v1, min);

            // Rasterize
            for (int y = min.y; y <= max.y; y++) {
                // Barycentric coordinates at start of row
                int w0 = w0_row;
                int w1 = w1_row;
                int w2 = w2_row;

                for (int x = min.x; x <= max.x; x++) {
                    // If p is on or inside all edges, render pixel.
                    if ((w0 | w1 | w2) >= 0) {
                        int col = getTextureColor(w0, w1, w2, t0, t1, t2, palette, texpage);

                        if ((uint)col != 0) {
                            window.VRAM.SetPixel((x & 0x3FF), (y & 0x1FF), col);
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

        private void GP0_RenderShadedTriOpaque() { // 0x30
            int tri = 3;
            int[] color = new int[tri];
            Point2D[] vertex = new Point2D[tri];

            for (int i = 0; i < tri; i++) {
                color[i] = get888Color(commandBuffer.Dequeue());
                vertex[i] = new Point2D(commandBuffer.Dequeue());
            }


            rasterizeShadedTri(vertex[0], vertex[1], vertex[2], color[0], color[1], color[2]);
        }

        private void GP0_RenderShadedQuadOpaque() { //0x38
            int quad = 4;
            int[] color = new int[quad];
            Point2D[] vertex = new Point2D[quad];

            for (int i = 0; i < quad; i++) {
                color[i] = get888Color(commandBuffer.Dequeue());
                vertex[i] = new Point2D(commandBuffer.Dequeue());
            }

            rasterizeShadedTri(vertex[0], vertex[1], vertex[2], color[0], color[1], color[2]);
            rasterizeShadedTri(vertex[1], vertex[2], vertex[3], color[1], color[2], color[3]);
        }

        private void GP0_MemCopyRectVRAMtoCPU() {
            uint command = commandBuffer.Dequeue();
            uint yx = commandBuffer.Dequeue();
            uint wh = commandBuffer.Dequeue();

            ushort x = (ushort)(yx & 0xFFFF);
            ushort y = (ushort)(yx >> 16);

            ushort h = (ushort)(wh & 0xFFFF);
            ushort w = (ushort)(wh >> 16);
            Console.WriteLine("WARNING UNIMPLEMENTED GPU COMMAND VRAM TO CPU");
            //Console.ReadLine();
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

            size = (ushort)(((h * w) + 1) >> 1);
            vram_coord.x = x;
            vram_coord.origin_x = x;
            vram_coord.y = y;
            vram_coord.w = w;
            vram_coord.h = h;

            mode = Mode.VRAM;
        }

        private void GP0_MemClearCache() {
            uint val0 = commandBuffer.Dequeue();
            //throw new NotImplementedException();
        }

        private void GP0_RenderMonoQuadOpaque() { //<----------------- 28
            uint color = commandBuffer.Dequeue();

            int quad = 4;
            Point2D[] vertex = new Point2D[quad];


            for (int i = 0; i < quad; i++) { // test
                uint ver = commandBuffer.Dequeue();
                vertex[i] = new Point2D(ver);
                //Console.WriteLine("GP0 QUAD: " + ver.ToString("x8"));
            }

            rasterizeTri(vertex[0], vertex[1], vertex[2], color);
            rasterizeTri(vertex[1], vertex[2], vertex[3], color);
        }

        //remember to refactor Point2D to rasterizer class and... well clear this mess beetwin gpu rasterizer and window
        internal void rasterizeTri(Point2D v0, Point2D v1, Point2D v2, uint color) {
            v0.x += drawingXOffset;
            v1.x += drawingXOffset;
            v2.x += drawingXOffset;
            
            v0.y += drawingYOffset;
            v1.y += drawingYOffset;
            v2.y += drawingYOffset;

            if (orient2d(v0, v1, v2) < 0) {
                Point2D aux = v1;
                v1 = v2;
                v2 = aux;
            }

            (Point2D min, Point2D max) = boundingBox(v0, v1, v2);

            int A01 = v0.y - v1.y, B01 = v1.x - v0.x;
            int A12 = v1.y - v2.y, B12 = v2.x - v1.x;
            int A20 = v2.y - v0.y, B20 = v0.x - v2.x;

            int w0_row = orient2d(v1, v2, min);
            int w1_row = orient2d(v2, v0, min);
            int w2_row = orient2d(v0, v1, min);

            // Rasterize
            for (int y = min.y; y <= max.y; y++) {
                // Barycentric coordinates at start of row
                int w0 = w0_row;
                int w1 = w1_row;
                int w2 = w2_row;

                for (int x = min.x; x <= max.x; x++) {
                    // If p is on or inside all edges, render pixel.
                    if ((w0 | w1 | w2) >= 0) {
                        window.VRAM.SetPixel((x & 0x3FF), (y & 0x1FF), get888Color(color));
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

        internal void rasterizeShadedTri(Point2D v0, Point2D v1, Point2D v2, int c0, int c1, int c2) {

            v0.x += drawingXOffset;
            v1.x += drawingXOffset;
            v2.x += drawingXOffset;
            
            v0.y += drawingYOffset;
            v1.y += drawingYOffset;
            v2.y += drawingYOffset;

            int area = orient2d(v0, v1, v2);
            if (area < 0) {
                Point2D aux = v1;
                v1 = v0;
                v0 = aux;
                int caux = c1;
                c1 = c0;
                c0 = caux;
            }

            (Point2D min, Point2D max) = boundingBox(v0, v1, v2);

            int A01 = v0.y - v1.y, B01 = v1.x - v0.x;
            int A12 = v1.y - v2.y, B12 = v2.x - v1.x;
            int A20 = v2.y - v0.y, B20 = v0.x - v2.x;

            int w0_row = orient2d(v1, v2, min);
            int w1_row = orient2d(v2, v0, min);
            int w2_row = orient2d(v0, v1, min);

            // Rasterize
            for (int y = min.y; y <= max.y; y++) {
                // Barycentric coordinates at start of row
                int w0 = w0_row;
                int w1 = w1_row;
                int w2 = w2_row;

                for (int x = min.x; x <= max.x; x++) {
                    // If p is on or inside all edges, render pixel.
                    if ((w0 | w1 | w2) >= 0) {
                        window.VRAM.SetPixel((x & 0x3FF), (y & 0x1FF), getShadedColor(w0, w1, w2, c0, c1, c2));
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

        private int getShadedColor(int w0, int w1, int w2, int color0, int color1, int color2) {
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

        private int getTextureColor(int w0, int w1, int w2, uint t0, uint t1, uint t2, uint palette, uint texpage) {

            uint clutX = (palette & 0x3f) * 16;
            uint clutY = ((palette >> 6) & 0x1FF);

            //Console.WriteLine("TextureCoord0 {0} {3}  TextureCoord1 {1} {4}  TextureCoord2 {2} {5}",
            //  texture[0] & 0xFF, texture[1] & 0xFF, texture[2] & 0xFF,
            // ((texture[0] >> 8) & 0xFF), ((texture[1] >> 8) & 0xFF) , ((texture[2] >> 8) & 0xFF));

            //https://codeplea.com/triangular-interpolation

            int XBase = (int)(texpage & 0xF) * 64;
            int YBase = (int)((texpage >> 4) & 0x1) * 256;

            float w = w0 + w1 + w2;
            float x = ((t0 & 0xFF) * w0 + (t1 & 0xFF) * w1 + (t2 & 0xFF) * w2) / w;
            float y = (((t0 >> 8) & 0xFF) * w0 + ((t1 >> 8) & 0xFF) * w1 + ((t2 >> 8) & 0xFF) * w2) / w;

            //byte xr = (byte)Math.Round(x);
            //byte yr = (byte)Math.Round(y);

            ushort index = window.VRAM.GetPixel16((int)x / 4 + XBase, (int)y + YBase);
            //Console.WriteLine(index.ToString("x8"));

            byte p = 0;
            byte xx = (byte)x;
            byte pix = (byte)(xx % 4);
            //Console.WriteLine(pix);
            switch (pix) {
                case 0: p = (byte)(index & 0xF); break;
                case 1: p = (byte)((index >> 4) & 0xF); break;
                case 2: p = (byte)((index >> 8) & 0xF); break;
                case 3: p = (byte)((index >> 12) & 0xF); break;
                default: Console.WriteLine((pix)); Console.ReadLine(); break;
            }

            return window.VRAM.GetPixel((int)(clutX + p), (int)clutY);

            //Console.WriteLine("index " + index);
            //window.VRAM.SetPixel((int)((x) + (XBase)),  (int)(y + (YBase)), Color.Red);

            //Console.WriteLine("x {0} y {1}", x, y);
            //window.VRAM.SetPixel(xr + XBase,yr + YBase, Color.Red);

            //clut test
            //window.VRAM.SetPixel((int)clutX + index , (int)clutY, Color.Red); //works on line!

            //return window.VRAM.GetPixel((int)x + XBase, (int)y + YBase);
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
            //maybe something about timings?
            commandBuffer.Dequeue();
            //Console.WriteLine("[GPU] [GP0] NOP");
            //Console.ReadLine();
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

            //Console.WriteLine("[GPU] [GP0] DrawMode");
        }

        private void GP0_SetDrawingAreaTopLeft() {
            uint val = commandBuffer.Dequeue();

            drawingAreaTop = (ushort)((val >> 10) & 0x3FF);
            drawingAreaLeft = (ushort)(val & 0x3FF); //todo 0x3FF???
        }

        private void GP0_SetDrawingAreaBottomRight() {
            uint val = commandBuffer.Dequeue();

            drawingAreaBottom = (ushort)((val >> 10) & 0x3FF);
            drawingAreaRight = (ushort)(val & 0x3FF);//todo 0x3FF???
        }

        public void writeGP1(uint value) {
            //TODO
            //Console.WriteLine("[GPU] GP1 Write Value: {0}", value.ToString("x8"));
            ////Console.ReadLine();
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