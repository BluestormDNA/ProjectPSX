using System;
using System.Collections.Generic;

namespace ProjectPSX.Devices {
    //TODO This is class is pretty much broken and the culprit ofc that many games dosnt work.
    //Need to rework timmings. An edge trigger should be implemented for interrupts
    public class CDROM {

        private Queue<uint> parameterBuffer = new Queue<uint>(16);
        private Queue<uint> responseBuffer = new Queue<uint>(16);
        private Queue<byte> dataBuffer = new Queue<byte>();
        private Queue<byte> cdBuffer = new Queue<byte>();

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

        private int seekLoc;
        private int readLoc;

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
        private bool isXAADPCM;
        private bool isSectorSizeRAW;
        private bool isIgnoreBit;
        private bool isXAFilter;
        private bool isReport;
        private bool isAutoPause;
        private bool isCDDA;

        private byte filterFile;
        private byte filterChannel;

        private bool cdDebug = false;

        private struct SectorHeader {
            public byte mm;
            public byte ss;
            public byte ff;
            public byte mode;
        }
        private SectorHeader sectorHeader;

        private struct SectorSubHeader {
            public byte file;
            public byte channel;
            public byte subMode;
            public byte codingInfo;

            public bool isEndOfRecord => (subMode & 0x1) != 0;
            public bool isVideo => (subMode & 0x2) != 0;
            public bool isAudio => (subMode & 0x4) != 0;
            public bool isData => (subMode & 0x8) != 0;
            public bool isTrigger => (subMode & 0x10) != 0;
            public bool isForm2 => (subMode & 0x20) != 0;
            public bool isRealTime => (subMode & 0x40) != 0;
            public bool isEndOfFile => (subMode & 0x80) != 0;
        }
        private SectorSubHeader sectorSubHeader;

        private enum Mode {
            Idle,
            Seek,
            Read,
            Play,
            TOC
        }
        Mode mode = Mode.Idle;

        private int counter;
        private Queue<byte> interruptQueue = new Queue<byte>();

        private CD cd;

        public CDROM() {
            cd = new CD();
        }

        bool edgeTrigger;
        public bool tick(int cycles) {
            counter += cycles;

            if (interruptQueue.Count != 0 && IF == 0) {
                if (cdDebug) Console.WriteLine($"[CDROM] Interrupt Queue is size: {interruptQueue.Count} dequeue to IF next Interrupt: {interruptQueue.Peek()}");
                IF |= interruptQueue.Dequeue();
                //edgeTrigger = true;
            }

            if (/*edgeTrigger &&*/ (IF & IE) != 0) {
                if (cdDebug) Console.WriteLine($"[CD INT] Triggering {IF:x8}");
                edgeTrigger = false;
                isBusy = false;
                return true;
            }

            switch (mode) {
                case Mode.Idle:
                    if (interruptQueue.Count != 0) { //Await some cycles so interrupts are not triggered instant
                        return false;
                    }
                    counter = 0;
                    //Console.WriteLine("[CDROM] MODE IDLE");
                    break;

                case Mode.Seek:
                    if (counter < 100000 || interruptQueue.Count != 0) {
                        return false;
                    }
                    counter = 0;
                    //Console.WriteLine("[CDROM] MODE SEEK");
                    mode = Mode.Idle;
                    STAT = (byte)(STAT & (~0x40));
                    break;

                case Mode.Read:
                case Mode.Play:
                    if (counter < (33868800 / (isDoubleSpeed ? 150 : 75)) /*&& dataBuffer.Count > 0*/) {
                        return false;
                    }
                    //if (dataBuffer.Count == 0) {

                    Queue<byte> sector = new Queue<byte>(cd.Read(isSectorSizeRAW, readLoc++));

                    //if (cdDebug) {
                    //Console.ForegroundColor = ConsoleColor.DarkGreen;
                    //Console.WriteLine($"Reading readLoc: {readLoc - 1} seekLoc: {seekLoc} size: {sector.Count}");
                    //Console.ResetColor();
                    //}

                    if (!isSectorSizeRAW) { //header tests
                        sectorHeader.mm = sector.Dequeue();
                        sectorHeader.ss = sector.Dequeue();
                        sectorHeader.ff = sector.Dequeue();
                        sectorHeader.mode = sector.Dequeue();

                        sectorSubHeader.file = sector.Dequeue();
                        sectorSubHeader.channel = sector.Dequeue();
                        sectorSubHeader.subMode = sector.Dequeue();
                        sectorSubHeader.codingInfo = sector.Dequeue();

                        //copy of subheader
                        sector.Dequeue();
                        sector.Dequeue();
                        sector.Dequeue();
                        sector.Dequeue();
                    }

                    //Console.WriteLine(mode);
                    //if ((STAT & 0x80) != 0) Console.WriteLine("is play");
                    //if (sectorSubHeader.isVideo) Console.WriteLine("is video");
                    //if (sectorSubHeader.isData) Console.WriteLine("is data");
                    //if (sectorSubHeader.isAudio) Console.WriteLine("is audio");

                    if (isXAADPCM && sectorSubHeader.isForm2) {
                        if (sectorSubHeader.isEndOfFile) {
                            if (cdDebug) Console.WriteLine("[CDROM] XA ON: End of File!");
                            //Console.ReadLine();
                            //mode = Mode.Idle; STAT = 0x2;
                        }

                        if (sectorSubHeader.isRealTime && sectorSubHeader.isAudio) {

                            if (isXAFilter && (filterFile != sectorSubHeader.file || filterChannel != sectorSubHeader.channel)) {
                                return false;
                            }

                            //Here we should send the XA to SPU but...
                            if (cdDebug) Console.WriteLine("[CDROM] XA ON: Realtime + Audio >> Skipping as no SPU");

                            return false;
                        }
                    }
                    // }


                    cdBuffer = new Queue<byte>(sector);
                    //Console.WriteLine(cdBuffer.Count);

                    isCDDA = !sectorSubHeader.isData;

                    if (interruptQueue.Count != 0) {
                        return false;
                    }
                    responseBuffer.Enqueue(STAT);
                    interruptQueue.Enqueue(0x1);
                    counter = 0;

                    //Console.WriteLine("[CDROM] MODE READ");
                    break;

                case Mode.TOC:
                    if (counter < 33868800 / (isDoubleSpeed ? 150 : 75) || interruptQueue.Count != 0) {
                        return false;
                    }
                    mode = Mode.Idle;
                    responseBuffer.Enqueue(STAT);
                    interruptQueue.Enqueue(0x2);
                    counter = 0;
                    break;
            }
            return false;

        }

        public uint load(Width w, uint addr) {
            switch (addr) {
                case 0x1F801800:
                    if (cdDebug) Console.WriteLine($"[CDROM] [L00] STATUS: {STATUS():x2}");
                    return STATUS();

                case 0x1F801801:
                    if (cdDebug) Console.WriteLine("[CDROM] [L01] RESPONSE " /*+ responseBuffer.Peek().ToString("x8")*/);
                    //Console.ReadLine();
                    //if (w == Width.HALF || w == Width.WORD) Console.WriteLine("WARNING RESPONSE BUFFER LOAD " + w);

                    if (responseBuffer.Count > 0)
                        return responseBuffer.Dequeue();

                    return 0xFF;

                case 0x1F801802:
                    if (cdDebug) Console.WriteLine("[CDROM] [L02] DATA");
                    //Console.WriteLine(dataBuffer.Count);
                    //Console.ReadLine();
                    if (w == Width.BYTE) {
                        return dataBuffer.Dequeue();
                    } else {
                        byte b0 = dataBuffer.Dequeue();
                        byte b1 = dataBuffer.Dequeue();
                        return (uint)(b1 << 8 | b0);
                    }

                case 0x1F801803:
                    switch (INDEX) {
                        case 0:
                        case 2:
                            if (cdDebug) Console.WriteLine("[CDROM] [L03.0] IE: {0}", ((uint)(0xe0 | IE)).ToString("x8"));
                            return (uint)(0xe0 | IE);
                        case 1:
                        case 3:
                            if (cdDebug) Console.WriteLine("[CDROM] [L03.1] IF: {0}", ((uint)(0xe0 | IF)).ToString("x8"));
                            return (uint)(0xe0 | IF);
                        default:
                            if (cdDebug) Console.WriteLine("[CDROM] [L03.x] Unimplemented");
                            return 0;
                    }

                default: return 0;
            }
        }

        public void write(Width w, uint addr, uint value) {
            switch (addr) {
                case 0x1F801800:
                    if (cdDebug) Console.WriteLine($"[CDROM] [W00] I: {value:x8}");
                    INDEX = (byte)(value & 0x3);
                    break;
                case 0x1F801801:
                    if (INDEX == 0) {
                        if (cdDebug) {
                            Console.BackgroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[CDROM] [W01.0] [COMMAND] >>> {value:x2}");
                            Console.ResetColor();
                        }
                        ExecuteCommand(value);
                    } else {
                        if (cdDebug) Console.WriteLine($"[CDROM] [Unhandled Write] Index: {INDEX:x8} Access: {addr:x8} Value: {value:x8}");
                    }
                    break;
                case 0x1F801802:
                    switch (INDEX) {
                        case 0:
                            if (cdDebug) Console.WriteLine($"[CDROM] [W02.0] Parameter: {value:x8}");
                            parameterBuffer.Enqueue(value);
                            break;
                        case 1:
                            if (cdDebug) Console.WriteLine($"[CDROM] [W02.1] Set IE: {value:x8}");
                            IE = (byte)(value & 0x1F);
                            break;
                        default:
                            if (cdDebug) Console.WriteLine($"[CDROM] [Unhandled Write] Access: {addr:x8} Value: {value:x8}");
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
                            if ((value & 0x80) != 0) {
                                //Console.WriteLine("[CDROM] [W03.0]  Want Data (Copy from cd buffer to databuffer)");
                                if (dataBuffer.Count > 0) { /*Console.WriteLine(">>>>>>> CDROM BUFFER WAS NOT EMPTY <<<<<<<<<");*/ return; }
                                dataBuffer = cdBuffer;
                            } else {
                                if (cdDebug) {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("[CDROM] [W03.0] Data Clear");
                                    Console.ResetColor();
                                }
                                dataBuffer.Clear();
                            }
                            break;
                        case 1:
                            IF &= (byte)~(value & 0x1F);
                            if (cdDebug) Console.WriteLine($"[CDROM] [W03.1] Set IF: {value:x8} -> IF = {IF:x8}");
                            if (interruptQueue.Count > 0) {
                                IF |= interruptQueue.Dequeue();
                            }

                            if ((value & 0x40) == 0x40) {
                                if (cdDebug) Console.WriteLine($"[CDROM] [W03.1 Parameter Buffer Clear] value {value:x8}");
                                parameterBuffer.Clear();
                            }
                            break;

                        default:
                            if (cdDebug) Console.WriteLine($"[CDROM] [Unhandled Write] Access: {addr:x8} Value: {value:x8}");
                            break;
                    }
                    break;
                default:
                    if (cdDebug) Console.WriteLine($"[CDROM] [Unhandled Write] Access: {addr:x8} Value: {value:x8}");
                    break;
            }
        }

        private void ExecuteCommand(uint value) {
            if (cdDebug) Console.WriteLine($"[CDROM] Command {value:x4}");
            interruptQueue.Clear();
            responseBuffer.Clear();
            isBusy = true;
            switch (value) {
                case 0x01: getStat(); break;
                case 0x02: setLoc(); break;
                case 0x03: play(); break;
                case 0x06: readN(); break;
                case 0x07: motorOn(); break;
                case 0x08: stop(); break;
                case 0x09: pause(); break;
                case 0x0A: init(); break;
                case 0x0B: mute(); break;
                case 0x0C: demute(); break;
                case 0x0D: setFilter(); break;
                case 0x0E: setMode(); break;
                case 0x10: getLocL(); break;
                case 0x11: getLocP(); break;
                case 0x12: setSession(); break;
                case 0x13: getTN(); break;
                case 0x14: getTD(); break;
                case 0x15: seekL(); break;
                case 0x16: seekP(); break;
                case 0x19: test(); break;
                case 0x1A: getID(); break;
                case 0x1B: readS(); break;
                case 0x1E: readTOC(); break;
                case 0x1F: videoCD(); break;
                case uint _ when value >= 0x50 && value <= 0x57: lockUnlock(); break;
                default: UnimplementedCDCommand(value); break;
            }
        }

        private void mute() {
            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
        }

        private void readTOC() {
            mode = Mode.TOC;
            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
        }

        private void motorOn() {
            STAT = 0x2;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x2);
        }

        private void getLocL() {
            responseBuffer.EnqueueRange(
                sectorHeader.mm, sectorHeader.ss, sectorHeader.ff, sectorHeader.mode, sectorSubHeader.file,
                sectorSubHeader.channel, sectorSubHeader.subMode, sectorSubHeader.codingInfo);

            interruptQueue.Enqueue(0x3);
        }

        private void getLocP() { //HARDCODED, THIS NEEDS SubChannelQ
            if (cdDebug) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[CDROM] getLocP WARNING BROKEN COMMAND");
                Console.ResetColor();
            }

            responseBuffer.EnqueueRange<uint>(0, 0, 0, 0, 0, 0, 0, 0);
            interruptQueue.Enqueue(0x3);
        }

        private void lockUnlock() {
            interruptQueue.Enqueue(0x5);
        }

        private void videoCD() { //INT5(11h,40h)  ;-Unused/invalid
            responseBuffer.EnqueueRange<uint>(0x11, 0x40);

            interruptQueue.Enqueue(0x5);
        }

        private void setSession() { //broken
            parameterBuffer.Clear();

            STAT = 0x42;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x2);
        }

        private void setFilter() {
            filterFile = (byte)parameterBuffer.Dequeue();
            filterChannel = (byte)parameterBuffer.Dequeue();

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
        }

        private void readS() {
            readLoc = seekLoc;

            STAT = 0x2;
            STAT |= 0x20;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            mode = Mode.Read;
        }

        private void seekP() {
            readLoc = seekLoc;
            STAT = 0x42; // seek

            mode = Mode.Seek;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            STAT = 0x2;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x2);
        }


        private void play() { //broken hardcoded to push puzzle bubble 2 to play
            if (cdDebug) Console.WriteLine("[CDROM] Broken Warning: CDDA Play Triggered");
            //Console.ReadLine();
            STAT = 0x82;
            mode = Mode.Play;
            //int track = BcdToDec((byte)parameterBuffer.Dequeue());
            // Console.WriteLine("Track " + track);

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);


            //responseBuffer.Enqueue(STAT);
            //interruptQueue.Enqueue(0x1);
        }

        private void stop() {
            STAT = 0;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x2);
        }

        private void getTD() {
            int track = BcdToDec((byte)parameterBuffer.Dequeue());

            if (track == 0) {
                (byte mm, byte ss, byte ff) = getMMSSFFfromLBA(cd.getLBA());
                responseBuffer.EnqueueRange<uint>(STAT, DecToBcd(mm), DecToBcd(ss));
                if (cdDebug) Console.WriteLine($"[CDROM] getTD Track: {track} STAT: {STAT:x2} {mm}:{ss}");
            } else {
                //assuming only 1 track BIN. Anything beyond would require a CUE parser
                responseBuffer.EnqueueRange<uint>(STAT, 0, 2);
                if (cdDebug) Console.WriteLine($"[CDROM] getTD Track: {track} STAT: {STAT:x2} HardCoded Track 1 00:02");
            }

            //Console.ReadLine();
            interruptQueue.Enqueue(0x3);
        }

        private void getTN() {
            //todo: Hardcoded tracks: 1. Anything beyond would require a CUE parser
            if (cdDebug) Console.WriteLine("[CDROM] getTN HardCoded Tracks: 1 1");
            //Console.ReadLine();
            responseBuffer.EnqueueRange<uint>(STAT, 1, 1);
            interruptQueue.Enqueue(0x3);
        }

        private void demute() {
            //this demutes the spu but we dont have one...
            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
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
            readLoc = seekLoc;

            STAT = 0x2;
            STAT |= 0x20;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            mode = Mode.Read;
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

            //Console.WriteLine($"[CDROM] SetMode: {mode:x8}");

            isDoubleSpeed = ((mode >> 7) & 0x1) == 1;
            isXAADPCM = ((mode >> 6) & 0x1) == 1;
            isSectorSizeRAW = ((mode >> 5) & 0x1) == 1;
            isIgnoreBit = ((mode >> 4) & 0x1) == 1;
            isXAFilter = ((mode >> 3) & 0x1) == 1;
            isReport = ((mode >> 2) & 0x1) == 1;
            isAutoPause = ((mode >> 1) & 0x1) == 1;
            isCDDA = (mode & 0x1) == 1;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
        }

        internal uint getData() {
            //Console.WriteLine(dataBuffer.Count);
            byte b0 = dataBuffer.Dequeue();
            byte b1 = dataBuffer.Dequeue();
            byte b2 = dataBuffer.Dequeue();
            byte b3 = dataBuffer.Dequeue();

            return (uint)(b3 << 24 | b2 << 16 | b1 << 8 | b0);
        }

        private void seekL() {
            readLoc = seekLoc;
            STAT = 0x42; // seek

            mode = Mode.Seek;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            STAT = 0x2;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x2);
        }

        private void setLoc() {
            byte mm = (byte)parameterBuffer.Dequeue();
            byte ss = (byte)parameterBuffer.Dequeue();
            byte ff = (byte)parameterBuffer.Dequeue();

            //Console.WriteLine($"[CDROM] setLoc BCD {mm:x2}:{ss:x2}:{ff:x2}");


            int minute = BcdToDec(mm);
            int second = BcdToDec(ss);
            int sector = BcdToDec(ff);

            //There are 75 sectors on a second
            seekLoc = sector + (second * 75) + (minute * 60 * 75);

            //temporal bin hack to bypass cue parse - 2 secs
            //WARNING this can wreck some games that setLoc to s0 or s1
            seekLoc -= 150; // -2 seconds

            if (seekLoc < 0) {
                Console.WriteLine($"[CDROM] WARNING NEGATIVE setLOC {seekLoc:x8}");
                seekLoc = 0;
            }

            if (cdDebug) {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"[CDROM] setLoc {minute:x2}:{second:x2}:{sector:x2} Loc: {seekLoc:x8}");
                Console.ResetColor();
            }

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
            //responseBuffer.EnqueueRange<uint>(0x08, 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00);
            //interruptQueue.Enqueue(0x5);

            //Door Open              INT5(11h,80h)  N/A
            //STAT = 0x10; //Shell Open
            //responseBuffer.EnqueueRange<uint>(0x11, 0x80, 0x00, 0x00);
            //interruptQueue.Enqueue(0x5);

            //Licensed: Mode2 INT3(stat)     INT2(02h, 00h, 20h, 00h, 53h, 43h, 45h, 4xh)
            STAT = 0x40; //0x40 seek
            STAT |= 0x2;
            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
            
            responseBuffer.EnqueueRange<uint>(0x02, 0x00, 0x20, 0x00, 0x53, 0x43, 0x45, 0x41); //SCE | //A 0x41 (America) - I 0x49 (Japan) - E 0x45 (Europe)
            interruptQueue.Enqueue(0x2);
        }

        private void getStat() {
            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
        }

        private void UnimplementedCDCommand(uint value) {
            Console.WriteLine($"[CDROM] Unimplemented CD Command {value}");
            //Console.ReadLine();
        }

        private void test() {
            uint command = parameterBuffer.Dequeue();
            responseBuffer.Clear(); //we need to clear the delay on response to get the actual 0 0 to bypass antimodchip protection
            switch (command) {
                case 0x04://Com 19h,04h   ;ResetSCExInfo (reset GetSCExInfo response to 0,0) Used for antimodchip games like Dino Crisis
                    Console.WriteLine("[CDROM] Command 19 04 ResetSCExInfo Anti Mod Chip Meassures");
                    STAT = 0x2;
                    responseBuffer.Enqueue(STAT);
                    interruptQueue.Enqueue(0x3);
                    break;
                case 0x05:// 05h      -   INT3(total,success);Stop SCEx reading and get counters
                    Console.WriteLine("[CDROM] Command 19 05 GetSCExInfo Hack 0 0 Bypass Response");
                    responseBuffer.EnqueueRange<uint>(0, 0);
                    interruptQueue.Enqueue(0x3);
                    break;
                case 0x20: //INT3(yy,mm,dd,ver) ;Get cdrom BIOS date/version (yy,mm,dd,ver) http://www.psxdev.net/forum/viewtopic.php?f=70&t=557
                    responseBuffer.EnqueueRange<uint>(0x94, 0x09, 0x19, 0xC0);
                    interruptQueue.Enqueue(0x3);
                    break;
                case 0x22: //INT3("for US/AEP") --> Region-free debug version --> accepts unlicensed CDRs
                    responseBuffer.EnqueueRange<uint>(0x66, 0x6F, 0x72, 0x20, 0x55, 0x53, 0x2F, 0x41, 0x45, 0x50);
                    interruptQueue.Enqueue(0x3);
                    break;
                case 0x60://  60h      lo,hi     INT3(databyte)   ;HC05 SUB-CPU read RAM and I/O ports
                    responseBuffer.Enqueue(0);
                    interruptQueue.Enqueue(0x3);
                    break;
                default:
                    Console.WriteLine($"[CDROM] Unimplemented 0x19 Test Command {command:x8}");
                    break;
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
            stat |= isXAADPCM ? (1 << 2) : 0;
            stat |= INDEX;
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

        private static byte DecToBcd(byte value) {
            return (byte)(value + 6 * (value / 10));
        }

        private static int BcdToDec(byte value) {
            return value - 6 * (value >> 4);
        }

        private static (byte mm, byte ss, byte ff) getMMSSFFfromLBA(int lba) {
            int ff = lba % 75;
            lba /= 75;

            int ss = lba % 60;
            lba /= 60;

            int mm = lba;

            return ((byte)mm, (byte)ss, (byte)ff);
        }

    }
}

