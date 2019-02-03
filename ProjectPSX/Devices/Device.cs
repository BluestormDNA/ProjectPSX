
namespace ProjectPSX.Devices {

    public class Device {

        protected byte[] registers;
        protected uint memOffset;

        public byte load8(uint addr) {
            addr -= memOffset;
            return registers[addr];
        }

        public ushort load16(uint addr) {
            addr -= memOffset;
            byte b0 = registers[addr + 0];
            byte b1 = registers[addr + 1];

            return (ushort)(b1 << 8 | b0);
        }

        public uint load32(uint addr) {
            addr -= memOffset;
            byte b0 = registers[addr + 0];
            byte b1 = registers[addr + 1];
            byte b2 = registers[addr + 2];
            byte b3 = registers[addr + 3];

            return (uint)(b3 << 24 | b2 << 16 | b1 << 8 | b0);
        }

        public void write8(uint addr, byte value) {
            addr -= memOffset;
            registers[addr] = value;
        }

        public void write16(uint addr, ushort value) {
            addr -= memOffset;
            registers[addr + 0] = (byte)(value);
            registers[addr + 1] = (byte)(value >> 8);
        }

        public void write32(uint addr, uint value) {
            addr -= memOffset;
            registers[addr + 0] = (byte)(value);
            registers[addr + 1] = (byte)(value >> 8);
            registers[addr + 2] = (byte)(value >> 16);
            registers[addr + 3] = (byte)(value >> 24);
        }
    }
}
