using System;

namespace ProjectPSX.Devices {
    public class InterruptController {

        private uint ISTAT; //IF Trigger that needs to be ack
        private uint IMASK; //IE Global Interrupt enable

        internal void set(Interrupt interrupt) {
            ISTAT |= (uint)interrupt;
            //Console.WriteLine("ISTAT SET MANUAL FROM DEVICE: " + ISTAT.ToString("x8") + " IMASK " + IMASK.ToString("x8"));
        }


        internal void writeISTAT(uint value) {
            ISTAT &= value & 0x7FF;
            //Console.ForegroundColor = ConsoleColor.Magenta;
            //Console.WriteLine("[IRQ] [ISTAT] Write " + value.ToString("x8") + " ISTAT " + ISTAT.ToString("x8"));
            //Console.ResetColor();
            //Console.ReadLine();
        }

        internal void writeIMASK(uint value) {
            IMASK = value & 0x7FF;
            //Console.WriteLine("[IRQ] [IMASK] Write " + IMASK.ToString("x8"));
            //Console.ReadLine();
        }

        internal uint loadISTAT() {
            //Console.WriteLine("[IRQ] [ISTAT] Load " + ISTAT.ToString("x8"));
            //Console.ReadLine();
            return ISTAT;
        }

        internal uint loadIMASK() {
            //Console.WriteLine("[IRQ] [IMASK] Load " + IMASK.ToString("x8"));
            //Console.ReadLine();
            return IMASK;
        }
    }
}
