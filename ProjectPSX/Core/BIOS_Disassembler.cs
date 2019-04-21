using System;

namespace ProjectPSX {
    internal class BIOS_Disassembler {
        internal void verbose(uint PC, uint[] GPR) {
            uint pc = PC & 0x1fffffff;
            switch (pc) {
                case 0xA0:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    //Console.WriteLine("[BIOS] [VERBOSE] A0 Function " + GPR[9].ToString("x8"));
                    //Console.ReadLine();
                    Console.ResetColor();
                    break;
                case 0xB0:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    //Console.WriteLine("[BIOS] [VERBOSE] B0 Function " + GPR[9].ToString("x8"));
                    //Console.ReadLine();
                    Console.ResetColor();
                    break;
                case 0xC0:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    //Console.WriteLine("[BIOS] [VERBOSE] C0 Function " + GPR[9].ToString("x8"));
                    //Console.ReadLine();
                    Console.ResetColor();
                    break;
            }
        }
    }
}