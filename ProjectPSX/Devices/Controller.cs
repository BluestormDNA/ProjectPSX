using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ProjectPSX {
    internal abstract class Controller {//todo revamp all this but needs a study on how to handle the form events
        protected Window window;

        protected Queue<byte> transferDataFifo = new Queue<byte>();
        protected ushort buttons = 0xFFFF;
        public bool ack;

        public abstract byte process(byte b);
        public abstract void resetToIdle();

        public void setWindow(Window window) {
            this.window = window;
            window.KeyDown += new KeyEventHandler(handleJoyPadDown);
            window.KeyUp += new KeyEventHandler(handleJoyPadUp);
        }

        void handleJoyPadDown(object sender, KeyEventArgs e) {
            switch (e.KeyCode) {
                case Keys.Space: buttons &= (ushort)~(buttons & 0x1); break;
                case Keys.Z: buttons &= (ushort)~(buttons & 0x2); break;
                case Keys.C: buttons &= (ushort)~(buttons & 0x4); break;
                case Keys.Enter: buttons &= (ushort)~(buttons & 0x8); break;
                case Keys.Up: buttons &= (ushort)~(buttons & 0x10); break;
                case Keys.Right: buttons &= (ushort)~(buttons & 0x20); break;
                case Keys.Down: buttons &= (ushort)~(buttons & 0x40); break;
                case Keys.Left: buttons &= (ushort)~(buttons & 0x80); break;
                case Keys.D1: buttons &= (ushort)~(buttons & 0x100); break;
                case Keys.D3: buttons &= (ushort)~(buttons & 0x200); break;
                case Keys.Q: buttons &= (ushort)~(buttons & 0x400); break;
                case Keys.E: buttons &= (ushort)~(buttons & 0x800); break;
                case Keys.W: buttons &= (ushort)~(buttons & 0x1000); break;
                case Keys.D: buttons &= (ushort)~(buttons & 0x2000); break;
                case Keys.S: buttons &= (ushort)~(buttons & 0x4000); break;
                case Keys.A: buttons &= (ushort)~(buttons & 0x8000); break;
            }
            //Console.WriteLine(buttons.ToString("x8"));
        }

        void handleJoyPadUp(object sender, KeyEventArgs e) {
            switch (e.KeyCode) {
                case Keys.Space: buttons |= 0x1; break;
                case Keys.Z: buttons |= 0x2; break;
                case Keys.C: buttons |= 0x4; break;
                case Keys.Enter: buttons |= 0x8; break;
                case Keys.Up: buttons |= 0x10; break;
                case Keys.Right: buttons |= 0x20; break;
                case Keys.Down: buttons |= 0x40; break;
                case Keys.Left: buttons |= 0x80; break;
                case Keys.D1: buttons |= 0x100; break;
                case Keys.D3: buttons |= 0x200; break;
                case Keys.Q: buttons |= 0x400; break;
                case Keys.E: buttons |= 0x800; break;
                case Keys.W: buttons |= 0x1000; break;
                case Keys.D: buttons |= 0x2000; break;
                case Keys.S: buttons |= 0x4000; break;
                case Keys.A: buttons |= 0x8000; break;
            }
            //Console.WriteLine(buttons.ToString("x8"));
        }

    }
}
