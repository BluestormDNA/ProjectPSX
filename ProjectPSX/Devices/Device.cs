namespace ProjectPSX.Devices {

    public abstract class Device {

        protected byte[] mem;
        protected uint memOffset;

        public uint load(Width w, uint addr) {
            addr -= memOffset;

            switch (w) {
                case Width.BYTE: return mem[addr];
                case Width.HALF: return (uint)(mem[addr + 1] << 8 | mem[addr]);
                case Width.WORD: return (uint)(mem[addr + 3] << 24 | mem[addr + 2] << 16 | mem[addr + 1] << 8 | mem[addr]);
                default: return 0xFFFF_FFFF;
            }
        }

        public void write(Width w, uint addr, uint value) {
            addr -= memOffset;

            switch (w) {
                case Width.BYTE: mem[addr] = (byte)value; break;
                case Width.HALF: mem[addr] = (byte)value; mem[addr + 1] = (byte)(value >> 8); break;
                case Width.WORD:
                    mem[addr] = (byte)value; mem[addr + 1] = (byte)(value >> 8);
                    mem[addr + 2] = (byte)(value >> 16); mem[addr + 3] = (byte)(value >> 24); break;
            }
        }

        public uint load32(uint addr) {
            addr -= memOffset;
            return (uint)(mem[addr + 3] << 24 | mem[addr + 2] << 16 | mem[addr + 1] << 8 | mem[addr]);
        }

        public void write32(uint addr, uint value) {
            addr -= memOffset;
            mem[addr] = (byte)value; mem[addr + 1] = (byte)(value >> 8);
            mem[addr + 2] = (byte)(value >> 16); mem[addr + 3] = (byte)(value >> 24);
        }
    }
}
