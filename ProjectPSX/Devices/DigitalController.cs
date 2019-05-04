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
                            return 0xFF;
                        default:
                            //Console.WriteLine("[Controller] Idle value WARNING " + b);
                            return 0xFF;
                    }

                case Mode.Transfer:
                    switch (b) {
                        case 0x42:
                            //Console.WriteLine("[Controller] Transfer Process 0x42");
                            mode = Mode.Transfer;
                            generateResponse();
                            return transferDataFifo.Dequeue();
                        default:
                            //Console.WriteLine("[Controller] Transfer Process" + b.ToString("x2"));
                            byte data = transferDataFifo.Dequeue();
                            if (transferDataFifo.Count == 0) {
                                //Console.WriteLine("Changing to mode IDLE");
                                mode = Mode.Idle;
                            }
                            return data;
                    }
                default:
                    //Console.WriteLine("[JOYPAD] Mode Warning");
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

    }
}