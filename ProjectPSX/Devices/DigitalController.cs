using System;
using System.Collections.Generic;

namespace ProjectPSX {
    internal class DigitalController : Controller {

        private ushort CONTROLLER_TYPE = 0x5A41; //digital
        private bool enabled;
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
                            Console.WriteLine($"[Controller] Idle Process Warning: {b:x2}");
                            ack = false;
                            return 0xFF;
                    }

                case Mode.Transfer:
                    switch (b) {
                        case 0x42:
                            //Console.WriteLine("[Controller] Init Transfer Process 0x42");
                            generateResponse();
                            ack = true;
                            return transferDataFifo.Dequeue();
                        default:
                            byte data;
                            bool pendingBytes = transferDataFifo.Count > 0;
                            if (pendingBytes) {
                                data = transferDataFifo.Dequeue();
                            } else {
                                data = 0xFF;
                            }
                            ack = pendingBytes;
                            if (!ack) {
                                //Console.WriteLine("[Controller] Changing to idle");
                                enabled = false;
                                mode = Mode.Idle;
                            }
                            //Console.WriteLine($"[Controller] Transfer Process value:{b:x2} response: {data:x2} queueCount: {transferDataFifo.Count} ack: {ack}");
                            return data;
                    }
                default:
                    //Console.WriteLine("[JOYPAD] Mode Warning");
                    ack = false;
                    return 0xFF;
            }
        }

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
