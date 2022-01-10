using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ProjectPSX.Devices.CdRom;
using static ProjectPSX.Devices.CdRom.TrackBuilder;

namespace ProjectPSX.Devices {
    //TODO This is class is pretty much broken and the culprit ofc that many games doesn't work.
    //Need to rework timmings. An edge trigger should be implemented for interrupts
    public class CDROM {

        private Queue<byte> parameterBuffer = new Queue<byte>(16);
        private Queue<byte> responseBuffer = new Queue<byte>(16);
        private Sector currentSector = new Sector(Sector.RAW_BUFFER);
        private Sector lastReadSector = new Sector(Sector.RAW_BUFFER);

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

        private bool mutedAudio;
        private bool mutedXAADPCM;

        private byte pendingVolumeLtoL = 0xFF;
        private byte pendingVolumeLtoR = 0;
        private byte pendingVolumeRtoL = 0;
        private byte pendingVolumeRtoR = 0xFF;

        private byte volumeLtoL = 0xFF;
        private byte volumeLtoR = 0;
        private byte volumeRtoL = 0;
        private byte volumeRtoR = 0xFF;

        private bool cdDebug = false;
        private bool isLidOpen = false;

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
        private Mode mode = Mode.Idle;

        private int counter;
        private Queue<byte> interruptQueue = new Queue<byte>();

        private CD cd;
        private SPU spu;

        public CDROM(CD cd, SPU spu) {
            this.cd = cd;
            this.spu = spu;
        }

        private bool edgeTrigger;

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
                    counter = 0;
                    return false;

                case Mode.Seek: //Hardcoded seek time...
                    if (counter < 33868800 / 3 || interruptQueue.Count != 0) {
                        return false;
                    }
                    counter = 0;
                    mode = Mode.Idle;
                    STAT = (byte)(STAT & (~0x40));

                    responseBuffer.Enqueue(STAT);
                    interruptQueue.Enqueue(0x2);
                    break;

                case Mode.Read:
                case Mode.Play:
                    if (counter < (33868800 / (isDoubleSpeed ? 150 : 75)) || interruptQueue.Count != 0) {
                        return false;
                    }
                    counter = 0;

                    byte[] readSector = cd.Read(readLoc++);

                    if (cdDebug) {
                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.WriteLine($"Reading readLoc: {readLoc - 1} seekLoc: {seekLoc} size: {readSector.Length}");
                        Console.ResetColor();
                    }

                    //Handle Mode.Play:
                    if (mode == Mode.Play) {
                        if (!mutedAudio && isCDDA) {
                            applyVolume(readSector);
                            spu.pushCdBufferSamples(readSector);
                        }

                        if (isAutoPause && cd.isTrackChange) {
                            responseBuffer.Enqueue(STAT);
                            interruptQueue.Enqueue(0x4);

                            pause();
                        }

                        if (isReport) {
                            //Console.WriteLine("Report Not Handled");
                        }

                        return false; //CDDA isn't delivered to CPU and doesn't raise interrupt
                    }

                    //Handle Mode.Read:

                    //first 12 are the sync header
                    sectorHeader.mm = readSector[12];
                    sectorHeader.ss = readSector[13];
                    sectorHeader.ff = readSector[14];
                    sectorHeader.mode = readSector[15];

                    sectorSubHeader.file = readSector[16];
                    sectorSubHeader.channel = readSector[17];
                    sectorSubHeader.subMode = readSector[18];
                    sectorSubHeader.codingInfo = readSector[19];

                    //if (sectorSubHeader.isVideo) Console.WriteLine("is video");
                    //if (sectorSubHeader.isData) Console.WriteLine("is data");
                    //if (sectorSubHeader.isAudio) Console.WriteLine("is audio");

                    if (isXAADPCM && sectorSubHeader.isForm2) {
                        if (sectorSubHeader.isEndOfFile) {
                            if (cdDebug) Console.WriteLine("[CDROM] XA ON: End of File!");
                            //is this even needed? There seems to not be an AutoPause flag like on CDDA
                            //RR4, Castlevania and others hang sound here if hard stoped to STAT 0x2
                        }

                        if (sectorSubHeader.isRealTime && sectorSubHeader.isAudio) {

                            if (isXAFilter && (filterFile != sectorSubHeader.file || filterChannel != sectorSubHeader.channel)) {
                                if (cdDebug) Console.WriteLine("[CDROM] XA Filter: file || channel");
                                return false;
                            }

                            if (cdDebug) Console.WriteLine("[CDROM] XA ON: Realtime + Audio"); //todo flag to pass to SPU?

                            if (!mutedAudio && !mutedXAADPCM) {
                                byte[] decodedXaAdpcm = XaAdpcm.Decode(readSector, sectorSubHeader.codingInfo);
                                applyVolume(decodedXaAdpcm);
                                spu.pushCdBufferSamples(decodedXaAdpcm);
                            }

                            return false;
                        }
                    }

                    //If we arived here sector is supposed to be delivered to CPU so slice out sync and header based on flag
                    if (!isSectorSizeRAW) {
                        var dataSector = readSector.AsSpan().Slice(24, 0x800);
                        lastReadSector.fillWith(dataSector);
                    } else {
                        var rawSector = readSector.AsSpan().Slice(12);
                        lastReadSector.fillWith(rawSector);
                    }

                    responseBuffer.Enqueue(STAT);
                    interruptQueue.Enqueue(0x1);

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

        public uint load(uint addr) {
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
                    return currentSector.readByte();

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

        public void write(uint addr, uint value) {
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
                    } else if (INDEX == 3) {
                        if (cdDebug) Console.WriteLine($"[CDROM] [W01.3] pendingVolumeRtoR: {value:x8}");
                        pendingVolumeRtoR = (byte)value;
                    } else {
                        if (cdDebug) Console.WriteLine($"[CDROM] [Unhandled Write] Index: {INDEX:x8} Access: {addr:x8} Value: {value:x8}");
                    }
                    break;
                case 0x1F801802:
                    switch (INDEX) {
                        case 0:
                            if (cdDebug) Console.WriteLine($"[CDROM] [W02.0] Parameter: {value:x8}");
                            parameterBuffer.Enqueue((byte)value);
                            break;
                        case 1:
                            if (cdDebug) Console.WriteLine($"[CDROM] [W02.1] Set IE: {value:x8}");
                            IE = (byte)(value & 0x1F);
                            break;

                        case 2:
                            if (cdDebug) Console.WriteLine($"[CDROM] [W02.2] pendingVolumeLtoL: {value:x8}");
                            pendingVolumeLtoL = (byte)value;
                            break;

                        case 3:
                            if (cdDebug) Console.WriteLine($"[CDROM] [W02.3] pendingVolumeRtoL: {value:x8}");
                            pendingVolumeRtoL = (byte)value;
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
                                if (currentSector.hasData()) { /*Console.WriteLine(">>>>>>> CDROM BUFFER WAS NOT EMPTY <<<<<<<<<");*/ return; }
                                currentSector.fillWith(lastReadSector.read());
                            } else {
                                if (cdDebug) {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("[CDROM] [W03.0] Data Clear");
                                    Console.ResetColor();
                                }
                                currentSector.clear();
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

                        case 2:
                            if (cdDebug) Console.WriteLine($"[CDROM] [W03.2] pendingVolumeLtoR: {value:x8}");
                            pendingVolumeLtoR = (byte)value;
                            break;

                        case 3:
                            if (cdDebug) Console.WriteLine($"[CDROM] [W03.3] ApplyVolumes: {value:x8}");
                            mutedXAADPCM = (value & 0x1) != 0;
                            bool applyVolume = (value & 0x20) != 0;
                            if (applyVolume) {
                                volumeLtoL = pendingVolumeLtoL;
                                volumeLtoR = pendingVolumeLtoR;
                                volumeRtoL = pendingVolumeRtoL;
                                volumeRtoR = pendingVolumeRtoR;
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
            mutedAudio = true;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
        }

        private void readTOC() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.Enqueue(0x5);
                return;
            }

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
            if (cdDebug) {
                Console.WriteLine($"mm: {sectorHeader.mm} ss: {sectorHeader.ss} ff: {sectorHeader.ff} mode: {sectorHeader.mode}" +
                    $" file: {sectorSubHeader.file} channel: {sectorSubHeader.channel} subMode: {sectorSubHeader.subMode} codingInfo: {sectorSubHeader.codingInfo}");
            }

            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.Enqueue(0x5);
                return;
            }

            Span<byte> response = stackalloc byte[] {
                sectorHeader.mm,
                sectorHeader.ss,
                sectorHeader.ff,
                sectorHeader.mode,
                sectorSubHeader.file,
                sectorSubHeader.channel,
                sectorSubHeader.subMode,
                sectorSubHeader.codingInfo
            };

            responseBuffer.EnqueueRange(response);

            interruptQueue.Enqueue(0x3);
        }

        private void getLocP() { //SubQ missing...
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.Enqueue(0x5);
                return;
            }

            Track track = cd.getTrackFromLoc(readLoc);
            (byte mm, byte ss, byte ff) = getMMSSFFfromLBA(readLoc - track.lbaStart);
            (byte amm, byte ass, byte aff) = getMMSSFFfromLBA(readLoc);

            if (cdDebug) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"track: {track.number} index: {1} mm: {mm} ss: {ss}" +
                    $" ff: {ff} amm: {amm} ass: {ass} aff: {aff}");
                Console.WriteLine($"track: {track.number} index: {1} mm: {DecToBcd(mm)} ss: {DecToBcd(ss)}" +
                    $" ff: {DecToBcd(ff)} amm: {DecToBcd(amm)} ass: {DecToBcd(ass)} aff: {DecToBcd(aff)}");
                Console.ResetColor();
            }

            Span<byte> response = stackalloc byte[] { track.number, 1, DecToBcd(mm), DecToBcd(ss), DecToBcd(ff), DecToBcd(amm), DecToBcd(ass), DecToBcd(aff) };
            responseBuffer.EnqueueRange(response);

            interruptQueue.Enqueue(0x3);
        }

        private void lockUnlock() {
            interruptQueue.Enqueue(0x5);
        }

        private void videoCD() { //INT5(11h,40h)  ;-Unused/invalid
            responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x40 });

            interruptQueue.Enqueue(0x5);
        }

        private void setSession() { //broken
            parameterBuffer.Clear();

            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.Enqueue(0x5);
                return;
            }

            STAT = 0x42;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x2);
        }

        private void setFilter() {
            filterFile = parameterBuffer.Dequeue();
            filterChannel = parameterBuffer.Dequeue();

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
        }

        private void readS() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.Enqueue(0x5);
                return;
            }

            readLoc = seekLoc;

            STAT = 0x2;
            STAT |= 0x20;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            mode = Mode.Read;
        }

        private void seekP() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.Enqueue(0x5);
                return;
            }

            readLoc = seekLoc;
            STAT = 0x42; // seek

            mode = Mode.Seek;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
        }


        private void play() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.Enqueue(0x5);
                return;
            }
            //If theres a trackN param it seeks and plays from the start location of it
            int track = 0;
            if (parameterBuffer.Count > 0) {
                track = BcdToDec(parameterBuffer.Dequeue());
                readLoc = seekLoc = cd.tracks[track].lbaStart;
                //else it plays from the previously seekLoc and seeks if not done (actually not checking if already seeked)
            } else {
                readLoc = seekLoc;
            }

            Console.WriteLine($"[CDROM] CDDA Play Triggered Track: {track} readLoc: {readLoc}");

            STAT = 0x82;
            mode = Mode.Play;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
        }

        private void stop() {
            STAT = 0;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x2);

            mode = Mode.Idle;
        }

        private void getTD() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.Enqueue(0x5);
                return;
            }

            int track = BcdToDec(parameterBuffer.Dequeue());

            if (track == 0) { //returns CD LBA / End of last track
                (byte mm, byte ss, byte ff) = getMMSSFFfromLBA(cd.getLBA());
                responseBuffer.EnqueueRange(stackalloc byte[] { STAT, DecToBcd(mm), DecToBcd(ss) });
                //if (cdDebug)
                Console.WriteLine($"[CDROM] getTD Track: {track} STAT: {STAT:x2} {mm}:{ss}");
            } else { //returns Track Start
                (byte mm, byte ss, byte ff) = getMMSSFFfromLBA(cd.tracks[track - 1].lbaStart);
                responseBuffer.EnqueueRange(stackalloc byte[] { STAT, DecToBcd(mm), DecToBcd(ss) });
                //if (cdDebug)
                Console.WriteLine($"[CDROM] getTD Track: {track} STAT: {STAT:x2} {mm}:{ss}");
            }

            //Console.ReadLine();
            interruptQueue.Enqueue(0x3);
        }

        private void getTN() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.Enqueue(0x5);
                return;
            }
            //if (cdDebug)
            Console.WriteLine($"[CDROM] getTN First Track: 1 (Hardcoded) - Last Track: {cd.tracks.Count}");
            //Console.ReadLine();
            responseBuffer.EnqueueRange(stackalloc byte[] { STAT, 1, DecToBcd((byte)cd.tracks.Count) });
            interruptQueue.Enqueue(0x3);
        }

        private void demute() {
            mutedAudio = false;

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
            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            STAT = 0x2;
            mode = Mode.Idle;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x2);
        }

        private void readN() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.Enqueue(0x5);
                return;
            }

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

        private void seekL() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.Enqueue(0x5);
                return;
            }

            readLoc = seekLoc;
            STAT = 0x42; // seek

            mode = Mode.Seek;

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
        }

        private void setLoc() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.Enqueue(0x5);
                return;
            }

            byte mm = parameterBuffer.Dequeue();
            byte ss = parameterBuffer.Dequeue();
            byte ff = parameterBuffer.Dequeue();

            //Console.WriteLine($"[CDROM] setLoc BCD {mm:x2}:{ss:x2}:{ff:x2}");


            int minute = BcdToDec(mm);
            int second = BcdToDec(ss);
            int sector = BcdToDec(ff);

            //There are 75 sectors on a second
            seekLoc = sector + (second * 75) + (minute * 60 * 75);

            if (seekLoc < 0) {
                Console.WriteLine($"[CDROM] WARNING NEGATIVE setLOC {seekLoc:x8}");
                seekLoc = 0;
            }

            if (cdDebug) {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"[CDROM] setLoc {mm:x2}:{ss:x2}:{ff:x2} Loc: {seekLoc:x8}");
                Console.ResetColor();
            }

            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);
        }

        private void getID() {
            //No Disk                INT3(stat)     INT5(08h, 40h, 00h, 00h, 00h, 00h, 00h, 00h)
            //STAT = 0x2; //0x40 seek
            //responseBuffer.Enqueue(STAT);
            //interruptQueue.Enqueue(0x3);
            //
            //responseBuffer.EnqueueRange(stackalloc byte[] { 0x08, 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
            //interruptQueue.Enqueue(0x5);

            //Door Open              INT5(11h,80h)  N/A
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.Enqueue(0x5);
                return;
            }

            //Licensed: Mode2 INT3(stat)     INT2(02h, 00h, 20h, 00h, 53h, 43h, 45h, 4xh)
            STAT = 0x40; //0x40 seek
            STAT |= 0x2;
            responseBuffer.Enqueue(STAT);
            interruptQueue.Enqueue(0x3);

            Span<byte> response = stackalloc byte[] { 0x02, 0x00, 0x20, 0x00, 0x53, 0x43, 0x45, 0x41 }; //SCE | //A 0x41 (America) - I 0x49 (Japan) - E 0x45 (Europe)
            responseBuffer.EnqueueRange(response);
            interruptQueue.Enqueue(0x2);
        }

        private void getStat() {
            if (!isLidOpen) {
                STAT = (byte)(STAT & (~0x18));
                STAT |= 0x2;
            }

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
                    responseBuffer.EnqueueRange(stackalloc byte[] { 0, 0 });
                    interruptQueue.Enqueue(0x3);
                    break;
                case 0x20: //INT3(yy,mm,dd,ver) ;Get cdrom BIOS date/version (yy,mm,dd,ver) http://www.psxdev.net/forum/viewtopic.php?f=70&t=557
                    responseBuffer.EnqueueRange(stackalloc byte[] { 0x94, 0x09, 0x19, 0xC0 });
                    interruptQueue.Enqueue(0x3);
                    break;
                case 0x22: //INT3("for US/AEP") --> Region-free debug version --> accepts unlicensed CDRs
                    responseBuffer.EnqueueRange(stackalloc byte[] { 0x66, 0x6F, 0x72, 0x20, 0x55, 0x53, 0x2F, 0x41, 0x45, 0x50 });
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
            return (currentSector.hasData()) ? 1 : 0;
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

        private void applyVolume(byte[] rawSector) {
            var samples = MemoryMarshal.Cast<byte, short>(rawSector);

            for (int i = 0; i < samples.Length; i += 2) {
                short l = samples[i];
                short r = samples[i + 1];

                int volumeL = ((l * volumeLtoL) >> 7) + ((r * volumeRtoL) >> 7);
                int volumeR = ((l * volumeLtoR) >> 7) + ((r * volumeRtoR) >> 7);

                samples[i] =     (short)Math.Clamp(volumeL, -0x8000, 0x7FFF);
                samples[i + 1] = (short)Math.Clamp(volumeR, -0x8000, 0x7FFF);
            }

        }

        public Span<uint> processDmaLoad(int size) {
            return currentSector.read(size);
        }

        internal void toggleLid() {
            isLidOpen = !isLidOpen;
            if (isLidOpen) {
                STAT = 0x18;
                mode = Mode.Idle;
                interruptQueue.Clear();
                responseBuffer.Clear();
            } else {
                //todo handle the Cd load and not this hardcoded test:
                //cd = new CD(@"cd_change_path");
            }
            Console.WriteLine($"[CDROM] Shell is Open: {isLidOpen} STAT: {STAT}");
        }

    }
}
