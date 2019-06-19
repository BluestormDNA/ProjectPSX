using ProjectPSX.Devices;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ProjectPSX {
    internal class JOYPAD {

        private Queue<byte> JOY_TX_DATA = new Queue<byte>(8); //1F801040h JOY_TX_DATA(W)
        private Queue<byte> JOY_RX_DATA = new Queue<byte>(8); //1F801040h JOY_RX_DATA(R) FIFO

        //1F801044 JOY_STAT(R)
        bool TXreadyFlag1 = true;
        bool TXreadyFlag2 = true;
        bool RXparityError;
        bool ackInputLevel;
        bool interruptRequest;
        int baudrateTimer;

        //1F801048 JOY_MODE(R/W)
        uint baudrateReloadFactor;
        uint characterLength;
        bool parityEnable;
        bool parityTypeOdd;
        bool clkOutputPolarity;


        //1F80104Ah JOY_CTRL (R/W) (usually 1003h,3003h,0000h)
        bool TXenable;
        bool JoyOutput;
        bool RXenable;
        bool ack;
        bool reset;
        uint RXinterruptMode;
        bool TXinterruptEnable;
        bool RXinterruptEnable;
        bool ACKinterruptEnable;
        uint desiredSlotNumber;

        private ushort JOY_BAUD;    //1F80104Eh JOY_BAUD(R/W) (usually 0088h, ie.circa 250kHz, when Factor = MUL1)

        Controller controller = new DigitalController();

        int counter = 500000;

        public bool tick(int cycles) {
            if (JoyOutput && JOY_TX_DATA.Count != 0) {
                JOY_RX_DATA.Enqueue(controller.process(JOY_TX_DATA.Dequeue()));
                //Console.WriteLine("[JOYPAD] TICK Enqueued RX response " + JOY_RX_DATA.Peek().ToString("x2"));
                //Console.ReadLine();
                ackInputLevel = controller.ack;

                TXreadyFlag2 = true;
                counter = 500;
                
            }
            //Console.WriteLine(ack);
            if (ackInputLevel && counter <= 0) {
                //Console.WriteLine("[IRQ] TICK Triggering JOYPAD");
                ackInputLevel = false;
                interruptRequest = true;
                return true;
            }
            if (counter > 0) counter -= cycles;

            //baudrateTimer -= cycles;

            //if (baudrateTimer <= 0)
            //    reloadTimer();

            return false;
        }

        private void reloadTimer() {
            //Console.WriteLine("RELOAD TIMER");
            baudrateTimer = (int)(JOY_BAUD * baudrateReloadFactor) & ~0x1;
        }

        public void write(Width w, uint addr, uint value) {
            switch (addr & 0xFF) {
                case 0x40:
                    //Console.WriteLine("[JOYPAD] TX DATA ENQUEUE " + value.ToString("x2"));
                    JOY_TX_DATA.Enqueue((byte)value);
                    TXreadyFlag1 = true;
                    TXreadyFlag2 = false;
                    break;
                case 0x48:
                    //Console.WriteLine("[JOYPAD] SET MODE " + value.ToString("x4"));
                    setJOY_MODE(value);
                    break;
                case 0x4A:
                    //Console.WriteLine("[JOYPAD] SET CONTROL " + ((ushort)value).ToString("x4"));
                    setJOY_CTRL(value);
                    if (ack) {
                        RXparityError = false;
                        interruptRequest = false;
                    }

                    if (reset) {
                        //Console.WriteLine("[JOYPAD] RESET");
                        setJOY_MODE(0);
                        setJOY_CTRL(0);
                        JOY_BAUD = 0;

                        JOY_RX_DATA.Clear();
                        JOY_TX_DATA.Clear();

                        TXreadyFlag1 = true;
                        TXreadyFlag2 = true;
                    }
                    break;
                case 0x4E:
                    //Console.WriteLine("[JOYPAD] SET BAUD " + value.ToString("x4"));
                    JOY_BAUD = (ushort)value;
                    reloadTimer();
                    break;
            }
        }

        private void setJOY_CTRL(uint value) {
            TXenable = (value & 0x1) != 0;
            JoyOutput = ((value >> 1) & 0x1) != 0;
            RXenable = ((value >> 2) & 0x1) != 0;
            ack = ((value >> 4) & 0x1) != 0;
            reset = ((value >> 6) & 0x1) != 0;
            RXinterruptMode = (value >> 8) & 0x3;
            TXinterruptEnable = ((value >> 10) & 0x1) != 0;
            RXinterruptEnable = ((value >> 11) & 0x1) != 0;
            ACKinterruptEnable = ((value >> 12) & 0x1) != 0;
            desiredSlotNumber = (value >> 13) & 0x1;
            //TODO ACK???
        }

        private void setJOY_MODE(uint value) {
            baudrateReloadFactor = value & 0x3;
            characterLength = (value >> 2) & 0x3;
            parityEnable = ((value >> 4) & 0x1) != 0;
            parityTypeOdd = ((value >> 5) & 0x1) != 0;
            clkOutputPolarity = ((value >> 8) & 0x1) != 0;
        }

        public uint load(Width w, uint addr) {
            switch (addr & 0xFF) {
                case 0x40:
                    if (JOY_RX_DATA.Count == 0) {
                        //Console.WriteLine("[JOYPAD] WARNING COUNT WAS 0 GET RX DATA returning 0");
                        return 0;
                    }
                    //Console.WriteLine("[JOYPAD] GET RX DATA " + JOY_RX_DATA.Peek().ToString("x2"));
                    //Console.WriteLine("count" + (JOY_RX_DATA.Count - 1));
                    return JOY_RX_DATA.Dequeue();
                case 0x44:
                    //Console.WriteLine("[JOYPAD] GET STAT " + getJOY_STAT().ToString("x8"));
                    return getJOY_STAT();
                case 0x48:
                    //Console.WriteLine("[JOYPAD] GET MODE " + getJOY_MODE().ToString("x8"));
                    return getJOY_MODE();
                case 0x4A:
                    //Console.WriteLine("[JOYPAD] GET CONTROL " + getJOY_CTRL().ToString("x8"));
                    return getJOY_CTRL();
                case 0x4E:
                    //Console.WriteLine("[JOYPAD] GET BAUD" + JOY_BAUD.ToString("x8"));
                    return JOY_BAUD;
                default:
                    //Console.WriteLine("[JOYPAD] Unhandled Read at" + addr);
                    return 0xFFFF_FFFF;
            }
        }

        private uint getJOY_CTRL() {
            uint joy_ctrl = 0;
            joy_ctrl |= TXenable ? 1u : 0u;
            joy_ctrl |= (JoyOutput ? 1u : 0u) << 1;
            joy_ctrl |= (RXenable ? 1u : 0u) << 2;
            joy_ctrl |= (ack ? 1u : 0u) << 4;
            joy_ctrl |= (reset ? 1u : 0u) << 6;
            joy_ctrl |= RXinterruptMode << 8;
            joy_ctrl |= (TXinterruptEnable ? 1u : 0u) << 10;
            joy_ctrl |= (RXinterruptEnable ? 1u : 0u) << 11;
            joy_ctrl |= (ACKinterruptEnable ? 1u : 0u) << 12;
            joy_ctrl |= desiredSlotNumber << 13;
            return joy_ctrl;
        }

        private uint getJOY_MODE() {
            uint joy_mode = 0;
            joy_mode |= baudrateReloadFactor;
            joy_mode |= characterLength << 2;
            joy_mode |= (parityEnable ? 1u : 0u) << 4;
            joy_mode |= (parityTypeOdd ? 1u : 0u) << 5;
            joy_mode |= (clkOutputPolarity ? 1u : 0u) << 4;
            return joy_mode;
        }

        private uint getJOY_STAT() {
            uint joy_stat = 0;
            joy_stat |= TXreadyFlag1 ? 1u : 0u;
            joy_stat |= (JOY_RX_DATA.Count > 0 ? 1u : 0u) << 1;
            joy_stat |= (TXreadyFlag2 ? 1u : 0u) << 2;
            joy_stat |= (RXparityError ? 1u : 0u) << 3;
            joy_stat |= (ackInputLevel ? 1u : 0u) << 7;
            joy_stat |= (interruptRequest ? 1u : 0u) << 9;
            joy_stat |= (uint)baudrateTimer << 11;

            ack = false;

            return joy_stat;
        }

        public void setWindow(Window window) {
            controller.setWindow(window);
        }

    }
}