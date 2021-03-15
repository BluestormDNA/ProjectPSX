using System;

namespace ProjectPSX.Devices {
    public class TIMERS {

        private TIMER[] timer = new TIMER[3];

        public TIMERS() {
            timer[0] = new TIMER(0);
            timer[1] = new TIMER(1);
            timer[2] = new TIMER(2);
        }

        public void write(uint addr, uint value) {
            int timerNumber = (int)(addr & 0xF0) >> 4;
            if (timerNumber > 2) {
                Console.WriteLine($"[TIMER] WRITE WARNING: Access to unavailable hardware timer {timerNumber}");
                return;
            }
            timer[timerNumber].write(addr, value);
            //Console.WriteLine($"[TIMER] WRITE Timer {timerNumber}:{value}");
        }

        public uint load(uint addr) {
            int timerNumber = (int)(addr & 0xF0) >> 4;
            if(timerNumber > 2) {
                Console.WriteLine($"[TIMER] LOAD WARNING: Access to unavailable hardware timer {timerNumber}");
                return 0xFFFF_FFFF;
            }
            //Console.WriteLine($"[TIMER] LOAD Timer {timerNumber}");
            return timer[timerNumber].load(addr);
        }

        public bool tick(int timerNumber, int cycles) {
            return timer[timerNumber].tick(cycles);
        }

        public void syncGPU((int, bool, bool) sync) {
            timer[0].syncGPU(sync);
            timer[1].syncGPU(sync);
        }

        public class TIMER {
            private int timerNumber;

            private uint counterValue;
            private uint counterTargetValue;

            private byte syncEnable;
            private byte syncMode;
            private byte resetCounterOnTarget;
            private byte irqWhenCounterTarget;
            private byte irqWhenCounterFFFF;
            private byte irqRepeat;
            private byte irqPulse;
            private byte clockSource;
            private byte interruptRequest;
            private byte reachedTarget;
            private byte reachedFFFF;

            private bool vblank;
            private bool hblank;
            private int dotDiv = 1;

            private bool prevHblank;
            private bool prevVblank;

            private bool irq;
            private bool alreadFiredIrq;

            public TIMER(int timerNumber) {
                this.timerNumber = timerNumber;
            }

            public void write(uint addr, uint value) {
                switch (addr & 0xF) {
                    case 0x0: counterValue = (ushort)value; break;
                    case 0x4: setCounterMode(value); break;
                    case 0x8: counterTargetValue = value; break;
                    default: Console.WriteLine("[TIMER] " + timerNumber + "Unhandled Write" + addr); Console.ReadLine(); ; break;
                }
            }

            public uint load(uint addr) {
                switch (addr & 0xF) {
                    case 0x0: return counterValue;
                    case 0x4: return getCounterMode();
                    case 0x8: return counterTargetValue;
                    default: Console.WriteLine("[TIMER] " + timerNumber + "Unhandled load" + addr); Console.ReadLine(); return 0;
                }
            }

            public void syncGPU((int dotDiv, bool hblank, bool vblank) sync) {
                prevHblank = hblank;
                prevVblank = vblank;
                dotDiv = sync.dotDiv;
                hblank = sync.hblank;
                vblank = sync.vblank;
            }

            int cycles;
            public bool tick(int cyclesTicked) { //todo this needs rework
                cycles += cyclesTicked;
                switch (timerNumber) {
                    case 0:
                        if (syncEnable == 1) {
                            switch (syncMode) {
                                case 0: if (hblank) return false; break;
                                case 1: if (hblank) counterValue = 0; break;
                                case 2: if (hblank) counterValue = 0; if (!hblank) return false; break;
                                case 3: if (!prevHblank && hblank) syncEnable = 0; else return false; break;
                            }
                        } //else free run

                        if (clockSource == 0 || clockSource == 2) {
                            counterValue += (ushort)cycles;
                            cycles = 0;
                        } else {
                            ushort dot = (ushort)(cycles * 11 / 7 / dotDiv);
                            counterValue += dot; //DotClock
                            cycles = 0;
                        }

                        return handleIrq();

                    case 1:
                        if (syncEnable == 1) {
                            switch (syncMode) {
                                case 0: if (vblank) return false; break;
                                case 1: if (vblank) counterValue = 0; break;
                                case 2: if (vblank) counterValue = 0; if (!vblank) return false; break;
                                case 3: if (!prevVblank && vblank) syncEnable = 0; else return false; break;
                            }
                        }

                        if (clockSource == 0 || clockSource == 2) {
                            counterValue += (ushort)cycles;
                            cycles = 0;
                        } else {
                            counterValue += (ushort)(cycles / 2160);
                            cycles %= 2160;
                        }

                        return handleIrq();
                    case 2:
                        if (syncEnable == 1 && syncMode == 0 || syncMode == 3) {
                            return false; // counter stoped
                        } //else free run

                        if (clockSource == 0 || clockSource == 1) {
                            counterValue += (ushort)cycles;
                            cycles = 0;
                        } else {
                            counterValue += (ushort)(cycles / 8);
                            cycles %= 8;
                        }

                        return handleIrq();
                    default:
                        return false;
                }

            }

            private bool handleIrq() {
                irq = false;

                if (counterValue >= counterTargetValue) {
                    reachedTarget = 1;
                    if (resetCounterOnTarget == 1) counterValue = 0;
                    if (irqWhenCounterTarget == 1) irq = true;
                }

                if (counterValue >= 0xFFFF) {
                    reachedFFFF = 1;
                    if (irqWhenCounterFFFF == 1) irq = true;
                }

                counterValue &= 0xFFFF;

                if (!irq) return false;

                if(irqPulse == 0){ //short bit10
                    interruptRequest = 0;
                } else { //toggle it
                    interruptRequest = (byte)((interruptRequest + 1) & 0x1);
                }

                bool trigger = interruptRequest == 0;

                if (irqRepeat == 0) { //once
                    if (!alreadFiredIrq && trigger) {
                        alreadFiredIrq = true;
                    } else { //already fired
                        return false;
                    }
                } // repeat

                interruptRequest = 1;

                return trigger;
            }

            private void setCounterMode(uint value) {
                syncEnable = (byte)(value & 0x1);
                syncMode = (byte)((value >> 1) & 0x3);
                resetCounterOnTarget = (byte)((value >> 3) & 0x1);
                irqWhenCounterTarget = (byte)((value >> 4) & 0x1);
                irqWhenCounterFFFF = (byte)((value >> 5) & 0x1);
                irqRepeat = (byte)((value >> 6) & 0x1);
                irqPulse = (byte)((value >> 7) & 0x1);
                clockSource = (byte)((value >> 8) & 0x3);

                interruptRequest = 1;
                alreadFiredIrq = false;

                counterValue = 0;
            }

            private uint getCounterMode() {
                uint counterMode = 0;
                counterMode |= syncEnable;
                counterMode |= (uint)syncMode << 1;
                counterMode |= (uint)resetCounterOnTarget << 3;
                counterMode |= (uint)irqWhenCounterTarget << 4;
                counterMode |= (uint)irqWhenCounterFFFF << 5;
                counterMode |= (uint)irqRepeat << 6;
                counterMode |= (uint)irqPulse << 7;
                counterMode |= (uint)clockSource << 8;
                counterMode |= (uint)interruptRequest << 10;
                counterMode |= (uint)reachedTarget << 11;
                counterMode |= (uint)reachedFFFF << 12;

                reachedTarget = 0;
                reachedFFFF = 0;

                return counterMode;
            }

        }

    }
}
