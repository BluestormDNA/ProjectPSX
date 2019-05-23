using System;
using System.Collections.Generic;

namespace ProjectPSX {
    internal class DigitalController : Controller {

        ushort CONTROLLER_TYPE = 0x5A41; //digital

        private enum Mode {
            Idle,
            Transfer
        }
        Mode mode = Mode.Idle;

        public override byte process(byte b) {
            switch (mode) {
                case Mode.Idle:
                    switch (b) {
                        case 0x01:
                            //Console.WriteLine("[Controller] Idle Process 0x01");
                            mode = Mode.Transfer;
                            enabled = true;
                            ack = true;
                            return 0xFF;
                        default:
                            //Console.WriteLine("[Controller] Idle value WARNING " + b);
                            ack = false;
                            return 0xFF;
                    }

                case Mode.Transfer:
                    switch (b) {
                        case 0x01:
                            //Console.WriteLine("[Controller] ERROR Transfer Process 0x1");
                            ack = true;
                                return 0xFF;
                        case 0x42:
                            //Console.WriteLine("[Controller] Transfer Process 0x42");
                            mode = Mode.Transfer;
                            ack = true;
                            generateResponse();
                            return transferDataFifo.Dequeue();
                        default:
                            //Console.WriteLine("[Controller] Transfer Process" + b.ToString("x2"));
                            byte data;
                            if (transferDataFifo.Count == 0) {
                                //Console.WriteLine("Changing to mode IDLE");
                                enabled = false;
                                mode = Mode.Idle;
                                ack = false;
                                data = 0xFF;
                            } else {
                                data = transferDataFifo.Dequeue();
                            }
                            return data;
                    }
                default:
                    //Console.WriteLine("[JOYPAD] Mode Warning");
                    ack = false;
                    return 0xFF;
            }
        }

        bool enabled;

        public void generateResponse() {
            byte b0 = (byte)(CONTROLLER_TYPE & 0xFF);
            byte b1 = (byte)((CONTROLLER_TYPE >> 8) & 0xFF);

            byte b2 = (byte)(buttons & 0xFF);
            byte b3 = (byte)((buttons >> 8) & 0xFF);

            transferDataFifo.Enqueue(b0);
            transferDataFifo.Enqueue(b1);
            transferDataFifo.Enqueue(b2);
            transferDataFifo.Enqueue(b3);
        }

        public override void idle() {
            mode = Mode.Idle;
        }

        public override bool isEnabled() {
            return enabled;
        }
    }
}