using System;

namespace ProjectPSX {
    internal class BIOS_Disassembler {
        internal void verbose(uint PC, uint[] GPR) {
            uint pc = PC & 0x1fffffff;
            uint function = GPR[9];

            switch (pc) {
                case 0xA0:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("[BIOS] [VERBOSE] A0 Function " + function.ToString("x8"));
                    switch (function) {
                        case 0xA1: Console.WriteLine("SystemErrorBootOrDiskFailure({0},{1})", (char)GPR[4], GPR[5].ToString("x8")); break;

                    }
                    //Console.ReadLine();
                    Console.ResetColor();
                    break;
                case 0xB0:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("[BIOS] [VERBOSE] B0 Function " + function.ToString("x8"));
                    //switch ()
                    //Console.ReadLine();
                    Console.ResetColor();
                    break;
                case 0xC0:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("[BIOS] [VERBOSE] C0 Function " + function.ToString("x8"));
                    //Console.ReadLine();
                    Console.ResetColor();
                    break;
            }
        }
    }
}