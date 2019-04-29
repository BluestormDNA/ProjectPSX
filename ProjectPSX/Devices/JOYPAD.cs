using ProjectPSX.Devices;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ProjectPSX {
    internal class JOYPAD : Device {

        Window window;

        Queue<byte> JOY_TX_DATA = new Queue<byte>(8); //1F801040h JOY_TX_DATA(W)
        Queue<byte> JOY_RX_DATA = new Queue<byte>(8); //1F801040h JOY_RX_DATA(R) FIFO

        uint JOY_STAT;      //1F801044 JOY_STAT(R)
        uint JOY_MODE;      //1F801048 JOY_MODE(R/W)

        ushort JOY_CTRL;    //1F80104Ah JOY_CTRL (R/W) (usually 1003h,3003h,0000h)
        ushort JOY_BAUD;    //1F80104Eh JOY_BAUD(R/W) (usually 0088h, ie.circa 250kHz, when Factor = MUL1)

        ushort controller_Type = 0x5A41; //digital
        ushort buttons = 0xFFFF;

        public void tick(uint cycles) {
            if(JOY_TX_DATA.Count != 0) {

            }
        }

        public new void write(Width w, uint addr, uint value) {
            switch (addr & 0xFF) {
                case 0x40:
                    JOY_TX_DATA.Enqueue((byte)value);
                    break;
                case 0x48:
                    JOY_MODE = value;
                    break;
                case 0x4A:
                    JOY_CTRL = (ushort)value;
                    break;
                case 0x4E:
                    JOY_BAUD = (ushort)value;
                    break;
            }
        }

        public new uint load(Width w, uint addr) {
            switch (addr & 0xFF) {
                case 0x40: return JOY_RX_DATA.Dequeue();
                case 0x44: return JOY_STAT;
                case 0x48: return JOY_MODE;
                case 0x4A: return JOY_CTRL;
                case 0x4E: return JOY_BAUD;
                default: Console.WriteLine("[JOYPAD] Unhandled Read at" + addr); return 0xFFFF_FFFF;
            }
        }

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
            Console.WriteLine(buttons.ToString("x8"));
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
            Console.WriteLine(buttons.ToString("x8"));
        }

    }
}