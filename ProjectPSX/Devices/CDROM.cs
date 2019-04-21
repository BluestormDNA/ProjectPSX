using System;
using System.Collections.Generic;

namespace ProjectPSX.Devices {
    public class CDROM : Device {

        private InterruptController InterruptController;

        private Queue<uint> parameterBuffer = new Queue<uint>(16);
        private Queue<uint> responseBuffer = new Queue<uint>(16);
        private Queue<uint> dataBuffer = new Queue<uint>();
        private byte wantData;

        private byte IE; // InterruptEnableRegister
        private byte IF; // InterruptFlagRegister

        //private uint STATUS = 0x8;
        private uint INDEX;

        public CDROM(InterruptController InterruptController) {
            this.InterruptController = InterruptController;
        }

        public new uint load(Width w, uint addr) {
            switch (addr) {
                case 0x1F801800: Console.WriteLine("[CDROM] [LOAD 00] STATUS = {0}", STATUS().ToString("x8")); return STATUS();
                case 0x1F801801: Console.WriteLine("[CDROM] [LOAD 01] RESPONSE"); return responseBuffer.Dequeue();
                case 0x1F801802: Console.WriteLine("[CDROM] [LOAD 02] DATA"); return dataBuffer.Dequeue();
                case 0x1F801803:
                    switch (INDEX) {
                        case 0:
                        case 2:
                            Console.WriteLine("[CDROM] [LOAD 03.0] IE: {0}", ((uint)(0xe0 | IE)).ToString("x8")); return (uint)(0xe0 | IE);
                        case 1:
                        case 3:
                            Console.WriteLine("[CDROM] [LOAD 03.1] IF: {0}", ((uint)(0xe0 | IF)).ToString("x8"));
                            Console.WriteLine("IF Loaded" + IF.ToString("x8"));
                            return (uint)(0xe0 | IF);
                        default: Console.WriteLine("[CDROM] [LOAD 03.X] Unimplemented"); return 0;
                    }
                default: return 0;
            }
        }

        public new void write(Width w, uint addr, uint value) {
            switch (addr) {
                case 0x1F801800:
                    Console.WriteLine("[CDROM] [WRITE 00] Index: {0}", value.ToString("x8"));
                    INDEX = value & 0x3;
                    break;
                case 0x1F801801:
                    if (INDEX == 0) {
                        Console.WriteLine("[CDROM] [WRITE 01.0] Command: {0}", value.ToString("x8"));
                        ExecuteCommand(value);
                    } else {
                        Console.WriteLine("[CDROM] [Unhandled Write] Access: {0} Value: {1}", addr.ToString("x8"), value.ToString("x8"));
                    }
                    break;
                case 0x1F801802:
                    switch (INDEX) {
                        case 0:
                            Console.WriteLine("[CDROM] [WRITE 02.0] Parameter: {0}", value.ToString("x8"));
                            parameterBuffer.Enqueue(value);
                            Console.ReadLine();
                            break;
                        case 1:
                            Console.WriteLine("[CDROM] [WRITE 02.1] Set IE: {0}", value.ToString("x8"));
                            IE = (byte)(value & 0x1F);
                            break;
                        default:
                            Console.WriteLine("[CDROM] [Unhandled Write] Access: {0} Value: {1}", addr.ToString("x8"), value.ToString("x8"));
                            break;
                    }
                    break;
                case 0x1F801803:
                    switch (INDEX) {
                        case 0:
                            // 1F801803h.Index0 - Request Register(W)
                            //0 - 4 0    Not used(should be zero)
                            //5   SMEN Want Command Start Interrupt on Next Command(0 = No change, 1 = Yes)
                            //6   BFWR...
                            //7   BFRD Want Data(0 = No / Reset Data Fifo, 1 = Yes / Load Data Fifo)
                            Console.WriteLine("[CDROM] [Write 03.0 Want Data] value {0}", (value & 0x80) != 0);
                            if((value & 0x80) != 0) {
                                dataBuffer.Clear();
                            }
                            break;
                        case 1:
                            Console.WriteLine("[CDROM] [Write 03.1] Set IF {0}", value.ToString("x8"));
                            IF &= (byte)~(value & 0x1F);
                            if (value == 0x40) {
                                Console.WriteLine("[CDROM] [Write 03.1 Parameter Buffer Clear] value {0}", value.ToString("x8"));
                                parameterBuffer.Clear();
                            }
                            Console.WriteLine("IF Writed" + IF.ToString("x8"));
                            break;

                        default:
                            Console.WriteLine("[CDROM] [Unhandled Write] Access: {0} Value: {1}", addr.ToString("x8"), value.ToString("x8"));
                            break;
                    }
                    break;
                default:
                    Console.WriteLine("[CDROM] [Unhandled Write] Access: {0} Value: {1}", addr.ToString("x8"), value.ToString("x8"));
                    break;
            }
        }

        private void ExecuteCommand(uint value) {
            switch (value) {
                case 0x19: test(); break;
                default: UnimplementedCDCommand(value); break;
            }
        }

        private void UnimplementedCDCommand(uint value) {
            Console.WriteLine("[CDROM] Unimplemented CD Command " + value.ToString("x8"));
            Console.ReadLine();
        }

        private void test() {
            switch (parameterBuffer.Dequeue()) {
                case 0x20: //INT3(yy,mm,dd,ver) ;Get cdrom BIOS date/version (yy,mm,dd,ver) http://www.psxdev.net/forum/viewtopic.php?f=70&t=557
                    responseBuffer.Enqueue(0x94);
                    responseBuffer.Enqueue(0x09);
                    responseBuffer.Enqueue(0x19);
                    responseBuffer.Enqueue(0xC0);

                    //force ISTAT 4
                    IF |= 0x3;
                    InterruptController.set(Interrupt.CDROM);
                    break;
                default: Console.WriteLine("[CDROM] Unimplemented test command"); break;
            }
        }

        private byte STATUS() {
            //1F801800h - Index/Status Register (Bit0-1 R/W) (Bit2-7 Read Only)
            //0 - 1 Index Port 1F801801h - 1F801803h index(0..3 = Index0..Index3)   (R / W)
            //2   ADPBUSY XA-ADPCM fifo empty(0 = Empty) ; set when playing XA-ADPCM sound
            //3   PRMEMPT Parameter fifo empty(1 = Empty) ; triggered before writing 1st byte
            //4   PRMWRDY Parameter fifo full(0 = Full)  ; triggered after writing 16 bytes
            //5   RSLRRDY Response fifo empty(0 = Empty) ; triggered after reading LAST byte
            //6   DRQSTS Data fifo empty(0 = Empty) ; triggered after reading LAST byte
            //7   BUSYSTS Command/ parameter transmission busy(1 = Busy)

            int stat = 0;
            //stat |= busy() << 7;
            stat |= dataBuffer_hasData() << 6;
            stat |= responseBuffer_hasData() << 5;
            stat |= parametterBuffer_hasSpace() << 4;
            stat |= parametterBuffer_isEmpty() << 3;
            //stat |= XA-ADPCM() << 2;
            stat |= (int)INDEX;
            return (byte)stat;
        }

        private int dataBuffer_hasData() {
            return (dataBuffer.Count > 0) ? 1 : 0;
        }

        private int parametterBuffer_isEmpty() {
            return (parameterBuffer.Count == 0) ? 1 : 0;
        }

        private int parametterBuffer_hasSpace() {
            return (parameterBuffer.Count < 16) ? 1 : 0;
        }

        private int responseBuffer_hasData() {
            return (responseBuffer.Count > 0) ? 1 : 0;
        }
    }
}

