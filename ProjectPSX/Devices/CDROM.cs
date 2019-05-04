using System;
using System.Collections.Generic;

namespace ProjectPSX.Devices {
    public class CDROM : Device {

        private Queue<uint> parameterBuffer = new Queue<uint>(16);
        private Queue<uint> responseBuffer = new Queue<uint>(16);
        private Queue<byte> dataBuffer = new Queue<byte>();
        private bool wantData;
        private bool isBusy;

        private byte IE; // InterruptEnableRegister
        private byte IF; // InterruptFlagRegister

        private byte INDEX;
        private byte STAT;
        //7  Play Playing CD-DA         ;\only ONE of these bits can be set
        //6  Seek Seeking; at a time(ie.Read/Play won't get
        //5  Read Reading data sectors  ;/set until after Seek completion)
        //4  ShellOpen Once shell open(0=Closed, 1=Is/was Open)
        //3  IdError(0=Okay, 1=GetID denied) (also set when Setmode.Bit4=1)
        //2  SeekError(0=Okay, 1=Seek error)     (followed by Error Byte)
        //1  Spindle Motor(0=Motor off, or in spin-up phase, 1=Motor on)
        //0  Error Invalid Command/parameters(followed by Error Byte)

        private int Loc;
        private int SeekL;
        private int ReadN;

        //Mode
        //7   Speed(0 = Normal speed, 1 = Double speed)
        //6   XA - ADPCM(0 = Off, 1 = Send XA - ADPCM sectors to SPU Audio Input)
        //5   Sector Size(0 = 800h = DataOnly, 1 = 924h = WholeSectorExceptSyncBytes)
        //4   Ignore Bit(0 = Normal, 1 = Ignore Sector Size and Setloc position)
        //3   XA - Filter(0 = Off, 1 = Process only XA - ADPCM sectors that match Setfilter)
        //2   Report(0 = Off, 1 = Enable Report - Interrupts for Audio Play)
        //1   AutoPause(0 = Off, 1 = Auto Pause upon End of Track); for Audio Play
        //0   CDDA(0 = Off, 1 = Allow to Read CD - DA Sectors; ignore missing EDC)

        private bool isDoubleSpeed;
        private bool XA_ADPCM;
        private bool isSectorSizeRAW;
        private bool isIgnoreBit;
        private bool XA_Filter;
        private bool isReport;
        private bool isAutoPause;
        private bool isCDDA;



        private enum Mode {
            Idle,
            Seek,
            Read,
            Transfer
        }
        Mode mode = Mode.Idle;

        private uint counter;
        private Queue<byte> interruptQueue = new Queue<byte>();

        private CD cd;

        public CDROM() {
            cd = new CD();
        }

        public bool tick(uint cycles) {
            counter += cycles;
            if (counter < 10000) {
                return false; ;
            }

            if (interruptQueue.Count != 0 && IF == 0) {
                Console.WriteLine("[CD INT] Queue is " + interruptQueue.Count + " Dequeue = IF | " + interruptQueue.Peek());
                IF |= interruptQueue.Dequeue();
            }

            if ((IF & IE) != 0 /*&& ((InterruptController.loadISTAT() & 0x4) != 0x4)*/) {
                //Console.WriteLine("[CD INT] Triggering " + IF.ToString("x8"));
                //InterruptController.set(Interrupt.CDROM);
                return true;
            }

            switch (mode) {
                case Mode.Idle:
                    if (counter < 4000 || interruptQueue.Count != 0) { //Await some cycles so interrupts are not triggered instant
                        return false;
                    }
                    counter = 0;
                    break;

                case Mode.Seek:
                    if (counter < 20000 || interruptQueue.Count != 0) {
                        return false;
                    }
                    mode = Mode.Read; //???
                    break;

                case Mode.Read:
                    if (counter < 100000 || interruptQueue.Count != 0) {
                        return false;
                    }
                    //i should trigger here and add loc...
                    responseBuffer.Enqueue(STAT);
                    interruptQueue.Enqueue(0x1);
                    counter = 0;
                    break;

                case Mode.Transfer:
                    if (counter < 10000 || interruptQueue.Count != 0) {
                        return false;
                    }

                    //if(dataBuffer.Count == 0)
                    //dataBuffer = new Queue<byte>(cd.read(Loc));
                    counter = 0;

                    break;
            }
            return false;

        }

        public new uint load(Width w, uint addr) {
            switch (addr) {
                case 0x1F801800: Console.WriteLine("[CDROM] [L00] STATUS = {0}", STATUS().ToString("x8")); return STATUS();
                case 0x1F801801: Console.WriteLine("[CDROM] [L01] RESPONSE " + responseBuffer.Peek().ToString("x8")); return responseBuffer.Dequeue();
                case 0x1F801802: Console.WriteLine("[CDROM] [L02] DATA"); return dataBuffer.Dequeue();
                case 0x1F801803:
                    switch (INDEX) {
                        case 0:
                        case 2:
                            Console.WriteLine("[CDROM] [L03.0] IE: {0}", ((uint)(0xe0 | IE)).ToString("x8")); return (uint)(0xe0 | IE);
                        case 1:
                        case 3:
                            Console.WriteLine("[CDROM] [L03.1] IF: {0}", ((uint)(0xe0 | IF)).ToString("x8")); return (uint)(0xe0 | IF);
                        default: Console.WriteLine("[CDROM] [L03.X] Unimplemented"); return 0;
                    }
                default: return 0;
            }
        }

        public new void write(Width w, uint addr, uint value) {
            switch (addr) {
                case 0x1F801800:
                    Console.WriteLine("[CDROM] [W00] I: {0}", value.ToString("x8"));
                    //Console.ReadLine();
                    INDEX = (byte)(value & 0x3);
                    break;
                case 0x1F801801:
                    if (INDEX == 0) {
                        Console.WriteLine("[CDROM] [W01.0]          COMMAND: {0}", value.ToString("x8"));
                        ExecuteCommand(value);
                    } else {
                        Console.WriteLine("[CDROM] [Unhandled Write] Index: {0} Access: {1} Value: {2}", INDEX.ToString("x8"), addr.ToString("x8"), value.ToString("x8"));
                    }
                    break;
                case 0x1F801802:
                    switch (INDEX) {
                        case 0:
                            Console.WriteLine("[CDROM] [W02.0] Parameter: {0}", value.ToString("x8"));
                            parameterBuffer.Enqueue(value);
                            break;
                        case 1:
                            Console.WriteLine("[CDROM] [W02.1] Set IE: {0}", value.ToString("x8"));
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
                            Console.WriteLine("[CDROM] [W03.0 Want Data] value {0}", (value & 0x80) != 0);
                            if ((value & 0x80) != 0) {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("[CDROM] [W03.0] Data Clear");
                                Console.ResetColor();
                                dataBuffer.Clear();
                                //interruptQueue.Clear();
                                //Console.ReadLine();
                            }
                            break;
                        case 1:
                            Console.WriteLine("[CDROM] [W03.1] Set IF {0}", value.ToString("x8"));
                            IF &= (byte)~(value & 0x1F);
                            if (value == 0x40) {
                                Console.WriteLine("[CDROM] [W03.1 Parameter Buffer Clear] value {0}", value.ToString("x8"));
                                parameterBuffer.Clear();
                            }
                            //Console.WriteLine("IF Writed" + IF.ToString("x8"));
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
                case 0x01: getStat(); break;
                case 0x02: setLoc(); break;
                case 0x06: readN(); break;
                case 0x09: pause(); break;
                case 0x0A: init(); break;
                case 0x0E: setMode(); break;
                case 0x15: seekL(); break;
                case 0x1A: getID(); break;
                case 0x19: test(); break;
                default: UnimplementedCDCommand(value); break;
            }
        }

        private void init() {
            STAT = 0x2;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x2);
        }

        private void pause() {
            //todo actual pause transfers
            STAT = 0x2;
            mode = Mode.Idle;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x2);
        }

        private void readN() {
            //todo actual read
            STAT = 0x2;
            STAT |= 0x20;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            mode = Mode.Read;
            //isBusy = true;
        }

        private void setMode() {
            //7   Speed(0 = Normal speed, 1 = Double speed)
            //6   XA - ADPCM(0 = Off, 1 = Send XA - ADPCM sectors to SPU Audio Input)
            //5   Sector Size(0 = 800h = DataOnly, 1 = 924h = WholeSectorExceptSyncBytes)
            //4   Ignore Bit(0 = Normal, 1 = Ignore Sector Size and Setloc position)
            //3   XA - Filter(0 = Off, 1 = Process only XA - ADPCM sectors that match Setfilter)
            //2   Report(0 = Off, 1 = Enable Report - Interrupts for Audio Play)
            //1   AutoPause(0 = Off, 1 = Auto Pause upon End of Track); for Audio Play
            //0   CDDA(0 = Off, 1 = Allow to Read CD - DA Sectors; ignore missing EDC)
            uint mode = parameterBuffer.Dequeue();

            isDoubleSpeed = ((mode >> 7) & 0x1) == 1;
            XA_ADPCM = ((mode >> 6) & 0x1) == 1;
            isSectorSizeRAW = ((mode >> 5) & 0x1) == 1;
            isIgnoreBit = ((mode >> 4) & 0x1) == 1;
            XA_Filter = ((mode >> 3) & 0x1) == 1;
            isReport = ((mode >> 2) & 0x1) == 1;
            isAutoPause = ((mode >> 1) & 0x1) == 1;
            isCDDA = (mode & 0x1) == 1;

            Console.WriteLine("SPEED MODE >>>>>>>>>>>>>>>>>>>" + isDoubleSpeed);
            Console.WriteLine("SECTOR SIZE >>>>>>>>>>>>>>>>>>>" + isSectorSizeRAW);
            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
        }

        internal uint getData() {
            //Console.WriteLine("databufferCount" + dataBuffer.Count);
            if (dataBuffer.Count == 0)
                dataBuffer = new Queue<byte>(cd.read(isSectorSizeRAW, Loc++));   

            byte b0 = dataBuffer.Dequeue();
            byte b1 = dataBuffer.Dequeue();
            byte b2 = dataBuffer.Dequeue();
            byte b3 = dataBuffer.Dequeue();

            return (uint)(b3 << 24 | b2 << 16 | b1 << 8 | b0);
        }

        private void seekL() {
            SeekL = Loc;
            STAT |= 0x40; // seek

            mode = Mode.Seek;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x2);
        }

        private void setLoc() {
            int minute = DecToBcd((byte)parameterBuffer.Dequeue());
            int second = DecToBcd((byte)parameterBuffer.Dequeue());
            int sector = DecToBcd((byte)parameterBuffer.Dequeue());

            //temporal bin hack to bypass cue parse - 2 secs
            second -= 2;

            //There are 75 sectors on a second
            Loc = sector + (second * 75) + (minute * 60 * 75);

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x2);
        }

        private void getID() {
            //No Disk                INT3(stat)     INT5(08h, 40h, 00h, 00h, 00h, 00h, 00h, 00h)
            //STAT = 0x2; //0x40 seek
            //responseBuffer.Enqueue(STAT);
            //interruptQueue.Enqueue(0x3);
            //
            //responseBuffer.Enqueue(0x08);
            //responseBuffer.Enqueue(0x40);
            //responseBuffer.Enqueue(0x00);
            //responseBuffer.Enqueue(0x00);
            //
            //responseBuffer.Enqueue(0x00);
            //responseBuffer.Enqueue(0x00);
            //responseBuffer.Enqueue(0x00);
            //responseBuffer.Enqueue(0x00);
            //interruptQueue.Enqueue(0x5);

            //Door Open              INT5(11h,80h)  N/A
            //STAT = 0x10; //Shell Open
            //interruptQueue.Enqueue(0x5);
            //
            //responseBuffer.Enqueue(0x11);
            //responseBuffer.Enqueue(0x80);
            //responseBuffer.Enqueue(0x0);
            //responseBuffer.Enqueue(0x0);

            //Licensed: Mode2 INT3(stat)     INT2(02h, 00h, 20h, 00h, 53h, 43h, 45h, 4xh)
            STAT |= 0x40; //0x40 seek
            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
            
            responseBuffer.Enqueue(0x02);
            responseBuffer.Enqueue(0x00);
            responseBuffer.Enqueue(0x20);
            responseBuffer.Enqueue(0x00);
            
            responseBuffer.Enqueue(0x53); //S
            responseBuffer.Enqueue(0x43); //C
            responseBuffer.Enqueue(0x45); //E
            responseBuffer.Enqueue(0x41); //A 0x41 (America) - I 0x49 (Japan) - E 0x45 (Europe) 
            interruptQueue.Enqueue(0x2);
        }

        private void getStat() {
            STAT = 0x2;
            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
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

                    interruptQueue.Enqueue(0x3);
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
            stat |= isBusy ? 1 : 0 << 7;
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

        int DecToBcd(byte val) {
            return (((val / 10) << 4) | (val % 10));
        }

        int BcdToDec(byte bcd) {
            return (((bcd >> 4) * 10) + (bcd & 0xF));
        }
    }
}

