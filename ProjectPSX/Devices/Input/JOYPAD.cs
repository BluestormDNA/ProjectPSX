using System;

namespace ProjectPSX {
    public class JOYPAD {

        private byte JOY_TX_DATA; //1F801040h JOY_TX_DATA(W)
        private byte JOY_RX_DATA; //1F801040h JOY_RX_DATA(R) FIFO
        private bool fifoFull;

        //1F801044 JOY_STAT(R)
        private bool TXreadyFlag1 = true;
        private bool TXreadyFlag2 = true;
        private bool RXparityError;
        private bool ackInputLevel;
        private bool interruptRequest;
        private int baudrateTimer;

        //1F801048 JOY_MODE(R/W)
        private uint baudrateReloadFactor;
        private uint characterLength;
        private bool parityEnable;
        private bool parityTypeOdd;
        private bool clkOutputPolarity;

        //1F80104Ah JOY_CTRL (R/W) (usually 1003h,3003h,0000h)
        private bool TXenable;
        private bool JoyOutput;
        private bool RXenable;
        private bool joyControl_unknow_bit3;
        private bool controlAck;
        private bool joyControl_unknow_bit5;
        private bool controlReset;
        private uint RXinterruptMode;
        private bool TXinterruptEnable;
        private bool RXinterruptEnable;
        private bool ACKinterruptEnable;
        private uint desiredSlotNumber;

        private ushort JOY_BAUD;    //1F80104Eh JOY_BAUD(R/W) (usually 0088h, ie.circa 250kHz, when Factor = MUL1)

        private enum JoypadDevice {
            None,
            Controller,
            MemoryCard
        }
        JoypadDevice joypadDevice = JoypadDevice.None;

        Controller controller;
        MemoryCard memoryCard;

        public JOYPAD(Controller controller, MemoryCard memoryCard) {
            this.controller = controller;
            this.memoryCard = memoryCard;
        }

        int counter;

        public bool tick() {
            if (counter > 0) {
                counter -= 100;
                if(counter == 0) {
                    //Console.WriteLine("[IRQ] TICK Triggering JOYPAD");
                    ackInputLevel = false;
                    interruptRequest = true;
                }
            }

            if (interruptRequest) return true;

            return false;
        }

        private void reloadTimer() {
            //Console.WriteLine("[JOYPAD] RELOAD TIMER");
            baudrateTimer = (int)(JOY_BAUD * baudrateReloadFactor) & ~0x1;
        }

        public void write(uint addr, uint value) {
            switch (addr & 0xFF) {
                case 0x40:
                    //Console.WriteLine("[JOYPAD] TX DATA ENQUEUE " + value.ToString("x2"));
                    JOY_TX_DATA = (byte)value;
                    JOY_RX_DATA = 0xFF;
                    fifoFull = true;

                    TXreadyFlag1 = true;
                    TXreadyFlag2 = false;

                    if (JoyOutput) {
                        TXreadyFlag2 = true;

                        //Console.WriteLine("[JOYPAD] DesiredSlot == " + desiredSlotNumber);
                        if (desiredSlotNumber == 1) {
                            JOY_RX_DATA = 0xFF;
                            ackInputLevel = false;
                            return;
                        }

                        if (joypadDevice == JoypadDevice.None) {
                            //Console.ForegroundColor = ConsoleColor.Red;
                            if (value == 0x01) {
                                //Console.ForegroundColor = ConsoleColor.Green;
                                joypadDevice = JoypadDevice.Controller;
                            } else if (value == 0x81) {
                                //Console.ForegroundColor = ConsoleColor.Blue;
                                joypadDevice = JoypadDevice.MemoryCard;
                            }
                        }

                        if(joypadDevice == JoypadDevice.Controller) {
                            JOY_RX_DATA = controller.process(JOY_TX_DATA);
                            ackInputLevel = controller.ack;
                            if (ackInputLevel) counter = 500;
                            //Console.WriteLine($"[JOYPAD] Conroller TICK Enqueued RX response {JOY_RX_DATA:x2} ack: {ackInputLevel}");
                            //Console.ReadLine();
                        } else if(joypadDevice == JoypadDevice.MemoryCard) {
                            JOY_RX_DATA = memoryCard.process(JOY_TX_DATA);
                            ackInputLevel = memoryCard.ack;
                            if (ackInputLevel) counter = 500;
                            //Console.WriteLine($"[JOYPAD] MemCard TICK Enqueued RX response {JOY_RX_DATA:x2} ack: {ackInputLevel}");
                            //Console.ReadLine();
                        } else {
                            ackInputLevel = false;
                        }
                        if (ackInputLevel == false) joypadDevice = JoypadDevice.None;
                    } else {
                        joypadDevice = JoypadDevice.None;
                        memoryCard.resetToIdle();
                        controller.resetToIdle();

                        ackInputLevel = false;
                    }


                    break;
                case 0x48:
                    //Console.WriteLine($"[JOYPAD] SET MODE {value:x4}");
                    setJOY_MODE(value);
                    break;
                case 0x4A:
                    //Console.WriteLine($"[JOYPAD] SET CONTROL {value:x4}");
                    setJOY_CTRL(value);
                    break;
                case 0x4E:
                    //Console.WriteLine($"[JOYPAD] SET BAUD {value:x4}");
                    JOY_BAUD = (ushort)value;
                    reloadTimer();
                    break;
                default: 
                    Console.WriteLine($"Unhandled JOYPAD Write {addr:x8} {value:x8}");
                    //Console.ReadLine();
                    break;
            }
        }

        private void setJOY_CTRL(uint value) {
            TXenable = (value & 0x1) != 0;
            JoyOutput = ((value >> 1) & 0x1) != 0;
            RXenable = ((value >> 2) & 0x1) != 0;
            joyControl_unknow_bit3 = ((value >> 3) & 0x1) != 0;
            controlAck = ((value >> 4) & 0x1) != 0;
            joyControl_unknow_bit5 = ((value >> 5) & 0x1) != 0;
            controlReset = ((value >> 6) & 0x1) != 0;
            RXinterruptMode = (value >> 8) & 0x3;
            TXinterruptEnable = ((value >> 10) & 0x1) != 0;
            RXinterruptEnable = ((value >> 11) & 0x1) != 0;
            ACKinterruptEnable = ((value >> 12) & 0x1) != 0;
            desiredSlotNumber = (value >> 13) & 0x1;

            if (controlAck) {
                //Console.WriteLine("[JOYPAD] CONTROL ACK");
                RXparityError = false;
                interruptRequest = false;
                controlAck = false;
            }

            if (controlReset) {
                //Console.WriteLine("[JOYPAD] CONTROL RESET");
                joypadDevice = JoypadDevice.None;
                controller.resetToIdle();
                memoryCard.resetToIdle();
                fifoFull = false;

                setJOY_MODE(0);
                setJOY_CTRL(0);
                JOY_BAUD = 0;

                JOY_RX_DATA = 0xFF;
                JOY_TX_DATA = 0xFF;

                TXreadyFlag1 = true;
                TXreadyFlag2 = true;

                controlReset = false;
            }

            if (!JoyOutput) {
                joypadDevice = JoypadDevice.None;
                memoryCard.resetToIdle();
                controller.resetToIdle();
            }
        }

        private void setJOY_MODE(uint value) {
            baudrateReloadFactor = value & 0x3;
            characterLength = (value >> 2) & 0x3;
            parityEnable = ((value >> 4) & 0x1) != 0;
            parityTypeOdd = ((value >> 5) & 0x1) != 0;
            clkOutputPolarity = ((value >> 8) & 0x1) != 0;
        }

        public uint load(uint addr) {
            switch (addr & 0xFF) {
                case 0x40:
                    //Console.WriteLine($"[JOYPAD] GET RX DATA {JOY_RX_DATA:x2}");
                    fifoFull = false;
                    return JOY_RX_DATA;
                case 0x44:
                    //Console.WriteLine($"[JOYPAD] GET STAT {getJOY_STAT():x8}");
                    return getJOY_STAT();
                case 0x48:
                    //Console.WriteLine($"[JOYPAD] GET MODE {getJOY_MODE():x8}");
                    return getJOY_MODE();
                case 0x4A:
                    //Console.WriteLine($"[JOYPAD] GET CONTROL {getJOY_CTRL():x8}");
                    return getJOY_CTRL();
                case 0x4E:
                    //Console.WriteLine($"[JOYPAD] GET BAUD {JOY_BAUD:x8}");
                    return JOY_BAUD;
                default:
                    //Console.WriteLine($"[JOYPAD] Unhandled Read at {addr}"); Console.ReadLine();
                    return 0xFFFF_FFFF;
            }
        }

        private uint getJOY_CTRL() {
            uint joy_ctrl = 0;
            joy_ctrl |= TXenable ? 1u : 0u;
            joy_ctrl |= (JoyOutput ? 1u : 0u) << 1;
            joy_ctrl |= (RXenable ? 1u : 0u) << 2;
            joy_ctrl |= (joyControl_unknow_bit3 ? 1u : 0u) << 3;
            //joy_ctrl |= (ack ? 1u : 0u) << 4; // only writeable
            joy_ctrl |= (joyControl_unknow_bit5 ? 1u : 0u) << 5;
            //joy_ctrl |= (reset ? 1u : 0u) << 6; // only writeable
            //bit 7 allways 0
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
            joy_stat |= (fifoFull ? 1u : 0u) << 1;
            joy_stat |= (TXreadyFlag2 ? 1u : 0u) << 2;
            joy_stat |= (RXparityError ? 1u : 0u) << 3;
            joy_stat |= (ackInputLevel ? 1u : 0u) << 7;
            joy_stat |= (interruptRequest ? 1u : 0u) << 9;
            joy_stat |= (uint)baudrateTimer << 11;

            ackInputLevel = false;

            return joy_stat;
        }
    }
}
