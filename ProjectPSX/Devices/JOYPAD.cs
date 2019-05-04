using ProjectPSX.Devices;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ProjectPSX {
    internal class JOYPAD : Device {

        private Queue<byte> JOY_TX_DATA = new Queue<byte>(8); //1F801040h JOY_TX_DATA(W)
        private Queue<byte> JOY_RX_DATA = new Queue<byte>(8); //1F801040h JOY_RX_DATA(R) FIFO

        private uint JOY_STAT = 0x7;      //1F801044 JOY_STAT(R) //hard coded value
        private uint JOY_MODE;      //1F801048 JOY_MODE(R/W)

        private ushort JOY_CTRL;    //1F80104Ah JOY_CTRL (R/W) (usually 1003h,3003h,0000h)
        private ushort JOY_BAUD;    //1F80104Eh JOY_BAUD(R/W) (usually 0088h, ie.circa 250kHz, when Factor = MUL1)

        private bool TXEN { get { return (JOY_CTRL & 0x1) != 0; } }
        private bool irq = false;

        Controller controller = new DigitalController();

        int testTimer = 500;
        int counter;

        public bool tick(uint cycles) {
            if (TXEN && JOY_TX_DATA.Count != 0) {
                JOY_RX_DATA.Enqueue(controller.process(JOY_TX_DATA.Dequeue()));
                //Console.WriteLine("Enqueued RX response " + JOY_RX_DATA.Peek().ToString("x2"));
                irq = true;
                counter = 0;
            }
            counter += (int)cycles;
            //Console.WriteLine(counter);
            if (irq == true && counter >= testTimer) {
                //Console.WriteLine("Triggering Contr Interrupt");
                counter = 0;
                irq = false;
                return true;
            }
            return false;
        }

        public new void write(Width w, uint addr, uint value) {
            switch (addr & 0xFF) {
                case 0x40:
                    //Console.WriteLine("[JOYPAD] TX DATA ENQUEUE" + value.ToString("x2"));
                    JOY_TX_DATA.Enqueue((byte)value);
                    break;
                case 0x48:
                    //Console.WriteLine("[JOYPAD] SET MODE");
                    JOY_MODE = value;
                    break;
                case 0x4A:
                    //Console.WriteLine("[JOYPAD] SET CONTROL " + ((ushort)value).ToString("x4"));
                    JOY_CTRL = (ushort)value;
                    if(JOY_CTRL == 0x2) JOY_RX_DATA.Enqueue(0xFF);
                    break;
                case 0x4E:
                    //Console.WriteLine("[JOYPAD] SET BAUD");
                    JOY_BAUD = (ushort)value;
                    break;
            }
        }

        public new uint load(Width w, uint addr) {
            switch (addr & 0xFF) {
                case 0x40:
                    //Console.WriteLine("[JOYPAD] GET RX DATA" + JOY_RX_DATA.Peek().ToString("x8"));
                    return JOY_RX_DATA.Dequeue();
                case 0x44:
                    //Console.WriteLine("[JOYPAD] GET STAT");
                    return JOY_STAT;
                case 0x48:
                    //Console.WriteLine("[JOYPAD] GET MODE");
                    return JOY_MODE;
                case 0x4A:
                    //Console.WriteLine("[JOYPAD] GET CONTROL");
                    return JOY_CTRL;
                case 0x4E:
                    //Console.WriteLine("[JOYPAD] GET BAUD");
                    return JOY_BAUD;
                default:
                    //Console.WriteLine("[JOYPAD] Unhandled Read at" + addr);
                    return 0xFFFF_FFFF;
            }
        }

        public void setWindow(Window window) {
            controller.setWindow(window);
        }

    }
}