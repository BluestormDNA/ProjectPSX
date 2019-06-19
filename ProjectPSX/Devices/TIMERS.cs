using System;

namespace ProjectPSX.Devices {
    public class TIMERS {

        TIMER[] timer = new TIMER[3];

        public TIMERS() {
            timer[0] = new TIMER();
            timer[1] = new TIMER();
            timer[2] = new TIMER();
        }

        public void write(Width w, uint addr, uint value) {
            int timerNumber = (int)(addr & 0xF0) >> 4;
            timer[timerNumber].write(w, addr, value);
            //Console.WriteLine("[TIMER] Write on" + ((addr & 0xF0) >> 4).ToString("x8") + " Value " + value.ToString("x8"));
        }

        public uint load(Width w, uint addr) {
            int timerNumber = (int)(addr & 0xF0) >> 4;
            //Console.WriteLine("[TIMER] load on" + ((addr & 0xF0) >> 4).ToString("x8") + " Value " + timer[timerNumber].load(w, addr).ToString("x4"));
            return timer[timerNumber].load(w, addr);
        }

        public bool tick(int timerNumber, int cycles) {
            return timer[timerNumber].tick(cycles);
        }

        public class TIMER {
            private int timerNumber;
            private static int timerCounter;

            private ushort counterValue;
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

            public TIMER() {
                this.timerNumber = timerCounter++;
            }

            public void write(Width w, uint addr, uint value) {
                switch (addr & 0xF) {
                    case 0x0: counterValue = (ushort)value; break;
                    case 0x4: setCounterMode(value); break;
                    case 0x8: counterTargetValue = value; break;
                }
            }

            public uint load(Width w, uint addr) {
                switch (addr & 0xF) {
                    case 0x0: return (uint)counterValue;
                    case 0x4: return getCounterMode();
                    case 0x8: return counterTargetValue;
                    default: return 0;
                }
            }

            public bool tick(int cycles) { //todo this needs rework
                switch (timerNumber) {
                    case 0:
                        //if (syncEnable == 1 && syncMode == 0 || syncMode == 3) {
                        //    return false; // counter stoped
                        //} //else free run

                        if (clockSource == 0 || clockSource == 2) {
                            counterValue += (ushort)cycles;
                        } else {
                            counterValue += (ushort)(cycles); //todo dotClock
                        }

                        //Console.WriteLine(counterValue.ToString("x4"));

                        if (resetCounterOnTarget == 1 && counterValue >= counterTargetValue) {
                            counterValue = 0;
                            reachedTarget = 1;
                            if (irqWhenCounterTarget == 1) {
                                //Console.WriteLine("[IRQ Timer 2] irqWhenTarget " + counterTargetValue.ToString("x4") + " ClockSource " + clockSource);
                                //Console.ReadLine();
                                return true;
                            }
                        }
                        if (counterValue == 0 && irqWhenCounterFFFF == 1) {
                            reachedFFFF = 1;
                            //Console.WriteLine("[IRQ Timer 2] counterWhenFFFF achieved. ClockSource: " + clockSource);
                            //Console.ReadLine();
                            return true;
                        }
                        return false;
                    case 1:
                        //if (syncEnable == 1 && syncMode == 0 || syncMode == 3) {
                        //   return false; // counter stoped
                        //} //else free run

                        if (clockSource == 0 || clockSource == 2) {
                            counterValue += (ushort)cycles;
                        } else {
                            counterValue += (ushort)(cycles);//todo VBlank
                        }

                        //Console.WriteLine(counterValue.ToString("x4"));

                        if (resetCounterOnTarget == 1 && counterValue >= counterTargetValue) {
                            counterValue = 0;
                            reachedTarget = 1;
                            if (irqWhenCounterTarget == 1) {
                                //Console.WriteLine("[IRQ Timer 2] irqWhenTarget " + counterTargetValue.ToString("x4") + " ClockSource " + clockSource);
                                //Console.ReadLine();
                                return true;
                            }
                        }
                        if (counterValue == 0 && irqWhenCounterFFFF == 1) {
                            reachedFFFF = 1;
                            //Console.WriteLine("[IRQ Timer 2] counterWhenFFFF achieved. ClockSource: " + clockSource);
                            //Console.ReadLine();
                            return true;
                        }
                        return false;
                    case 2:
                        if (syncEnable == 1 && syncMode == 0 || syncMode == 3) {
                            return false; // counter stoped
                        } //else free run

                        if (clockSource == 0 || clockSource == 1) {
                            counterValue += (ushort)cycles;
                        } else {
                            counterValue += (ushort)(cycles / 8);
                        }

                        //Console.WriteLine(counterValue.ToString("x4"));

                        if (resetCounterOnTarget == 1 && counterValue >= counterTargetValue) {
                            counterValue = 0;
                            reachedTarget = 1;
                            if (irqWhenCounterTarget == 1) {
                                //Console.WriteLine("[IRQ Timer 2] irqWhenTarget " + counterTargetValue.ToString("x4") + " ClockSource " + clockSource);
                                //Console.ReadLine();
                                return true;
                            }
                        }
                        if (counterValue == 0 && irqWhenCounterFFFF == 1) {
                            reachedFFFF = 1;
                            //Console.WriteLine("[IRQ Timer 2] counterWhenFFFF achieved. ClockSource: " + clockSource);
                            //Console.ReadLine();
                            return true;
                        }
                        return false;
                    default:
                        return false;
                }

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
                interruptRequest = (byte)((value >> 10) & 0x1);
                reachedTarget = (byte)((value >> 11) & 0x1);
                reachedFFFF = (byte)((value >> 12) & 0x1);

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
