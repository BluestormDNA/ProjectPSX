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
        private Queue<DelayedInterrupt> interruptQueue = new Queue<DelayedInterrupt>();

        public class DelayedInterrupt {
            public int delay;
            public byte interrupt;

            public DelayedInterrupt(int delay, byte interrupt) {
                this.delay = delay;
                this.interrupt = interrupt;
            }
        }

        //INT0 No response received(no interrupt request)
        //INT1 Received SECOND(or further) response to ReadS/ReadN(and Play+Report)
        //INT2 Received SECOND response(to various commands)
        //INT3 Received FIRST response(to any command)
        //INT4 DataEnd(when Play/Forward reaches end of disk) (maybe also for Read?)
        //INT5 Received error-code(in FIRST or SECOND response)
        //INT5 also occurs on SECOND GetID response, on unlicensed disks
        //INT5 also occurs when opening the drive door(even if no command
        //   was sent, ie.even if no read-command or other command is active)
        // INT6 N/A
        //INT7   N/A
        public class Interrupt {
            public const byte INT0_NO_RESPONSE = 0;
            public const byte INT1_SECOND_RESPONSE_READ_PLAY = 1;
            public const byte INT2_SECOND_RESPONSE = 2;
            public const byte INT3_FIRST_RESPONSE = 3;
            public const byte INT4_DATA_END = 4;
            public const byte INT5_ERROR = 5;
        }

        private CD cd;
        private SPU spu;

        public CDROM(CD cd, SPU spu) {
            this.cd = cd;
            this.spu = spu;
        }

        private bool edgeTrigger;

        private Dictionary<uint, string> commands = new Dictionary<uint, string> {
            [0x01] = "Cmd_01_GetStat",
            [0x02] = "Cmd_02_SetLoc",
            [0x03] = "Cmd_03_Play",
            [0x04] = "Cmd_04_Forward",
            [0x05] = "Cmd_05_Backward",
            [0x06] = "Cmd_06_ReadN",
            [0x07] = "Cmd_07_MotorOn",
            [0x08] = "Cmd_08_Stop",
            [0x09] = "Cmd_09_Pause",
            [0x0A] = "Cmd_0A_Init",
            [0x0B] = "Cmd_0B_Mute",
            [0x0C] = "Cmd_0C_Demute",
            [0x0D] = "Cmd_0D_SetFilter",
            [0x0E] = "Cmd_0E_SetMode",
            [0x0F] = "Cmd_0F_GetParam",
            [0x10] = "Cmd_10_GetLocL",
            [0x11] = "Cmd_11_GetLocP",
            [0x12] = "Cmd_12_SetSession",
            [0x13] = "Cmd_13_GetTN",
            [0x14] = "Cmd_14_GetTD",
            [0x15] = "Cmd_15_SeekL",
            [0x16] = "Cmd_16_SeekP",
            [0x17] = "--- [Unimplemented]",
            [0x18] = "--- [Unimplemented]",
            [0x19] = "Cmd_19_Test",
            [0x1A] = "Cmd_1A_GetID",
            [0x1B] = "Cmd_1B_ReadS",
            [0x1C] = "Cmd_1C_Reset [Unimplemented]",
            [0x1D] = "Cmd_1D_GetQ [Unimplemented]",
            [0x1E] = "Cmd_1E_ReadTOC",
            [0x1F] = "Cmd_1F_VideoCD",
        };

        public bool tick(int cycles) {
            counter += cycles;

            if (interruptQueue.Count != 0) {
                var delayedInterrupt = interruptQueue.Peek();
                delayedInterrupt.delay -= cycles;
            }

            if (interruptQueue.Count != 0 && IF == 0 && interruptQueue.Peek().delay <= 0) {
                if (cdDebug) Console.WriteLine($"[CDROM] Interrupt Queue is size: {interruptQueue.Count} dequeue to IF next Interrupt: {interruptQueue.Peek()}");
                IF |= interruptQueue.Dequeue().interrupt;
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
                    interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT2_SECOND_RESPONSE);
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
                        if (!mutedAudio) {
                            applyVolume(readSector);
                            spu.pushCdBufferSamples(readSector);
                        }

                        if (isAutoPause && cd.isTrackChange) {
                            responseBuffer.Enqueue(STAT);
                            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT4_DATA_END);

                            Cmd_09_Pause();
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
                    interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT1_SECOND_RESPONSE_READ_PLAY);

                    break;

                case Mode.TOC:
                    if (counter < 33868800 / (isDoubleSpeed ? 150 : 75) || interruptQueue.Count != 0) {
                        return false;
                    }
                    mode = Mode.Idle;
                    responseBuffer.Enqueue(STAT);
                    interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT2_SECOND_RESPONSE);
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
                    //Console.ReadLine();
                    //if (w == Width.HALF || w == Width.WORD) Console.WriteLine("WARNING RESPONSE BUFFER LOAD " + w);

                    if (responseBuffer.Count > 0) {
                        if (cdDebug) Console.WriteLine("[CDROM] [L01] RESPONSE " + responseBuffer.Peek().ToString("x8"));
                        return responseBuffer.Dequeue();
                    }

                    if (cdDebug) Console.WriteLine("[CDROM] [L01] RESPONSE 0xFF");

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
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[CDROM] [W01.0] [COMMAND] {value:x2} {commands.GetValueOrDefault(value, "---")}");
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
                            if (interruptQueue.Count > 0 && interruptQueue.Peek().delay <= 0) {
                                IF |= interruptQueue.Dequeue().interrupt;
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
            //Console.WriteLine($"PRE STAT {STAT:x2}");
            interruptQueue.Clear();
            responseBuffer.Clear();
            isBusy = true;
            switch (value) {
                case 0x01: Cmd_01_GetStat(); break;
                case 0x02: Cmd_02_SetLoc(); break;
                case 0x03: Cmd_03_Play(); break;
                //case 0x04: Cmd_04_Forward(); break; //todo
                //case 0x05: Cmd_05_Backward(); break; //todo
                case 0x06: Cmd_06_ReadN(); break;
                case 0x07: Cmd_07_MotorOn(); break;
                case 0x08: Cmd_08_Stop(); break;
                case 0x09: Cmd_09_Pause(); break;
                case 0x0A: Cmd_0A_Init(); break;
                case 0x0B: Cmd_0B_Mute(); break;
                case 0x0C: Cmd_0C_Demute(); break;
                case 0x0D: Cmd_0D_SetFilter(); break;
                case 0x0E: Cmd_0E_SetMode(); break;
                //case 0x0F: Cmd_0F_GetParam(); break; //todo
                case 0x10: Cmd_10_GetLocL(); break;
                case 0x11: Cmd_11_GetLocP(); break;
                case 0x12: Cmd_12_SetSession(); break;
                case 0x13: Cmd_13_GetTN(); break;
                case 0x14: Cmd_14_GetTD(); break;
                case 0x15: Cmd_15_SeekL(); break;
                case 0x16: Cmd_16_SeekP(); break;
                case 0x19: Cmd_19_Test(); break;
                case 0x1A: Cmd_1A_GetID(); break;
                case 0x1B: Cmd_1B_ReadS(); break;
                case 0x1E: Cmd_1E_ReadTOC(); break;
                case 0x1F: Cmd_1F_VideoCD(); break;
                case uint _ when value >= 0x50 && value <= 0x57: Cmd_5x_lockUnlock(); break;
                default: UnimplementedCDCommand(value); break;
            }
            //Console.WriteLine($"POST STAT {STAT:x2}");
        }

        private void Cmd_01_GetStat() {
            if (!isLidOpen) {
                STAT = (byte)(STAT & (~0x18));
                STAT |= 0x2;
            }

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
        }

        private void Cmd_02_SetLoc() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
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
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
        }

        private void Cmd_03_Play() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
                return;
            }
            //If theres a trackN param it seeks and plays from the start location of it
            int track = 0;
            if (parameterBuffer.Count > 0 && parameterBuffer.Peek() != 0) {
                track = BcdToDec(parameterBuffer.Dequeue());
                if (cd.isAudioCD()) {
                    readLoc = seekLoc = cd.tracks[track - 1].lbaStart;
                } else {
                    readLoc = seekLoc = cd.tracks[track].lbaStart;
                }
                //else it plays from the previously seekLoc and seeks if not done (actually not checking if already seeked)
            } else {
                readLoc = seekLoc;
            }

            Console.WriteLine($"[CDROM] CDDA Play Triggered Track: {track} readLoc: {readLoc}");

            STAT = 0x82;
            mode = Mode.Play;

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
        }

        private void Cmd_06_ReadN() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
                return;
            }

            readLoc = seekLoc;

            STAT = 0x2;
            STAT |= 0x20;

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);

            mode = Mode.Read;
        }

        private void Cmd_07_MotorOn() {
            STAT = 0x2;

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT2_SECOND_RESPONSE);
        }

        private void Cmd_08_Stop() {
            STAT = 0x2;
            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);

            STAT = 0;
            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT2_SECOND_RESPONSE);

            mode = Mode.Idle;
        }

        private void Cmd_09_Pause() {
            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);

            STAT = 0x2;
            mode = Mode.Idle;

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT2_SECOND_RESPONSE);
        }

        private void Cmd_0A_Init() {
            STAT = 0x2;

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT2_SECOND_RESPONSE);
        }

        private void Cmd_0B_Mute() {
            mutedAudio = true;

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
        }

        private void Cmd_0C_Demute() {
            mutedAudio = false;

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
        }

        private void Cmd_0D_SetFilter() {
            filterFile = parameterBuffer.Dequeue();
            filterChannel = parameterBuffer.Dequeue();

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
        }

        private void Cmd_0E_SetMode() {
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
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
        }

        private void Cmd_10_GetLocL() {
            if (cdDebug) {
                Console.WriteLine($"mm: {sectorHeader.mm} ss: {sectorHeader.ss} ff: {sectorHeader.ff} mode: {sectorHeader.mode}" +
                    $" file: {sectorSubHeader.file} channel: {sectorSubHeader.channel} subMode: {sectorSubHeader.subMode} codingInfo: {sectorSubHeader.codingInfo}");
            }

            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
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

            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
        }

        private void Cmd_11_GetLocP() { //SubQ missing...
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
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

            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
        }

        private void Cmd_12_SetSession() { //broken
            parameterBuffer.Clear();

            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
                return;
            }

            STAT = 0x42;

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT2_SECOND_RESPONSE);
        }

        private void Cmd_13_GetTN() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
                return;
            }
            //if (cdDebug)
            Console.WriteLine($"[CDROM] getTN First Track: 1 (Hardcoded) - Last Track: {cd.tracks.Count}");
            //Console.ReadLine();
            responseBuffer.EnqueueRange(stackalloc byte[] { STAT, 1, DecToBcd((byte)cd.tracks.Count) });
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
        }

        private void Cmd_14_GetTD() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
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
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
        }

        private void Cmd_15_SeekL() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
                return;
            }

            readLoc = seekLoc;
            STAT = 0x42; // seek

            mode = Mode.Seek;

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
        }

        private void Cmd_16_SeekP() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
                return;
            }

            readLoc = seekLoc;
            STAT = 0x42; // seek

            mode = Mode.Seek;

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
        }

        private void Cmd_19_Test() {
            uint command = parameterBuffer.Dequeue();
            responseBuffer.Clear(); //we need to clear the delay on response to get the actual 0 0 to bypass antimodchip protection
            switch (command) {
                case 0x04://Com 19h,04h   ;ResetSCExInfo (reset GetSCExInfo response to 0,0) Used for antimodchip games like Dino Crisis
                    Console.WriteLine("[CDROM] Command 19 04 ResetSCExInfo Anti Mod Chip Meassures");
                    STAT = 0x2;
                    responseBuffer.Enqueue(STAT);
                    interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
                    break;
                case 0x05:// 05h      -   INT3(total,success);Stop SCEx reading and get counters
                    Console.WriteLine("[CDROM] Command 19 05 GetSCExInfo Hack 0 0 Bypass Response");
                    responseBuffer.EnqueueRange(stackalloc byte[] { 0, 0 });
                    interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
                    break;
                case 0x20: //INT3(yy,mm,dd,ver) ;Get cdrom BIOS date/version (yy,mm,dd,ver) http://www.psxdev.net/forum/viewtopic.php?f=70&t=557
                    responseBuffer.EnqueueRange(stackalloc byte[] { 0x94, 0x09, 0x19, 0xC0 });
                    interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
                    break;
                case 0x22: //INT3("for US/AEP") --> Region-free debug version --> accepts unlicensed CDRs
                    responseBuffer.EnqueueRange(stackalloc byte[] { 0x66, 0x6F, 0x72, 0x20, 0x55, 0x53, 0x2F, 0x41, 0x45, 0x50 });
                    interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
                    break;
                case 0x60://  60h      lo,hi     INT3(databyte)   ;HC05 SUB-CPU read RAM and I/O ports
                    responseBuffer.Enqueue(0);
                    interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
                    break;
                default:
                    Console.WriteLine($"[CDROM] Unimplemented 0x19 Test Command {command:x8}");
                    break;
            }
        }

        private void Cmd_1A_GetID() {
            //Door Open              INT5(11h,80h)  N/A
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
                return;
            }

            //No Disk                INT3(stat)     INT5(08h, 40h, 00h, 00h, 00h, 00h, 00h, 00h)
            //STAT = 0x2; //0x40 seek
            //responseBuffer.Enqueue(STAT);
            //interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_RECEIVED_FIRST_RESPONSE);
            //
            //responseBuffer.EnqueueRange(stackalloc byte[] { 0x08, 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
            //interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);

            //Licensed: Mode2 INT3(stat)     INT2(02h, 00h, 20h, 00h, 53h, 43h, 45h, 4xh)
            STAT = 0x40; //0x40 seek
            STAT |= 0x2;
            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);

            // Audio Disk INT3(stat) INT5(0Ah,90h, 00h,00h, 00h,00h,00h,00h)
            if (cd.isAudioCD()) {
                Span<byte> audioCdResponse = stackalloc byte[] { 0x0A, 0x90, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                responseBuffer.EnqueueRange(audioCdResponse);
                interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
                return;
            }

            // Licensed: Mode2 INT3(stat)     INT2(02h, 00h, 20h, 00h, 53h, 43h, 45h, 4xh)
            Span<byte> gameResponse = stackalloc byte[] { 0x02, 0x00, 0x20, 0x00, 0x53, 0x43, 0x45, 0x41 }; //SCE | //A 0x41 (America) - I 0x49 (Japan) - E 0x45 (Europe)
            responseBuffer.EnqueueRange(gameResponse);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT2_SECOND_RESPONSE);
        }

        private void Cmd_1B_ReadS() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
                return;
            }

            readLoc = seekLoc;

            STAT = 0x2;
            STAT |= 0x20;

            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);

            mode = Mode.Read;
        }

        private void Cmd_1E_ReadTOC() {
            if (isLidOpen) {
                responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x80 });
                interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
                return;
            }

            mode = Mode.TOC;
            responseBuffer.Enqueue(STAT);
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT3_FIRST_RESPONSE);
        }

        private void Cmd_1F_VideoCD() { //INT5(11h,40h)  ;-Unused/invalid
            responseBuffer.EnqueueRange(stackalloc byte[] { 0x11, 0x40 });

            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
        }

        private void Cmd_5x_lockUnlock() {
            interruptQueue.EnqueueDelayedInterrupt(Interrupt.INT5_ERROR);
        }

        private void UnimplementedCDCommand(uint value) {
            Console.WriteLine($"[CDROM] Unimplemented CD Command {value}");
            Console.ReadLine();
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
