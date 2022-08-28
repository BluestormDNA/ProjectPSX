using System;

namespace ProjectPSX.Devices.Expansion;
public class Exp2 {

    public uint load(uint addr) {
        //Console.WriteLine($"[BUS] Read Unsupported to EXP2 address: {addr:x8}");
        return 0xFF;
    }

    public void write(uint addr, uint value) {
        switch (addr) {
            case 0x1F802041:
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[EXP2] PSX: POST [{value:x1}]");
                Console.ResetColor();
                break;
            case 0x1F802023:
            case 0x1F802080:
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write((char)value);
                Console.ResetColor();
                break;
            default:
                Console.WriteLine($"[BUS] Write Unsupported to EXP2: {addr:x8} Value: {value:x8}");
                break;
        }
    }
}
