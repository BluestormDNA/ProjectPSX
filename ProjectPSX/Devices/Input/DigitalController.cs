namespace ProjectPSX {
    public sealed class DigitalController : Controller {

        private const ushort CONTROLLER_TYPE = 0x5A41; //digital

        private enum Mode {
            Idle,
            Connected,
            Transfering,
        }
        Mode mode = Mode.Idle;

        public override byte process(byte b) {
            switch (mode) {
                case Mode.Idle:
                    switch (b) {
                        case 0x01:
                            //Console.WriteLine("[Controller] Idle Process 0x01");
                            mode = Mode.Connected;
                            ack = true;
                            return 0xFF;
                        default:
                            //Console.WriteLine($"[Controller] Idle Process Warning: {b:x2}");
                            transferDataFifo.Clear();
                            ack = false;
                            return 0xFF;
                    }

                case Mode.Connected:
                    switch (b) {
                        case 0x42:
                            //Console.WriteLine("[Controller] Connected Init Transfer Process 0x42");
                            mode = Mode.Transfering;
                            generateResponse();
                            ack = true;
                            return transferDataFifo.Dequeue();
                        default:
                            //Console.WriteLine("[Controller] Connected Transfer Process unknow command {b:x2} RESET TO IDLE");
                            mode = Mode.Idle;
                            transferDataFifo.Clear();
                            ack = false;
                            return 0xFF;
                    }

                case Mode.Transfering:
                    byte data = transferDataFifo.Dequeue();
                    ack = transferDataFifo.Count > 0;
                    if (!ack) {
                        //Console.WriteLine("[Controller] Changing to idle");
                        mode = Mode.Idle;
                    }
                    //Console.WriteLine($"[Controller] Transfer Process value:{b:x2} response: {data:x2} queueCount: {transferDataFifo.Count} ack: {ack}");
                    return data;
                default:
                    //Console.WriteLine("[Controller] This should be unreachable");
                    return 0xFF;
            }
        }

        public void generateResponse() {
            byte b0 = CONTROLLER_TYPE & 0xFF;
            byte b1 = (CONTROLLER_TYPE >> 8) & 0xFF;

            byte b2 = (byte)(buttons & 0xFF);
            byte b3 = (byte)((buttons >> 8) & 0xFF);

            transferDataFifo.Enqueue(b0);
            transferDataFifo.Enqueue(b1);
            transferDataFifo.Enqueue(b2);
            transferDataFifo.Enqueue(b3);
        }

        public override void resetToIdle() {
            mode = Mode.Idle;
        }
    }
}
