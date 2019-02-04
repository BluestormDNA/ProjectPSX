
namespace ProjectPSX.Devices {

    public class Device {

        protected byte[] registers;
        protected uint memOffset;

        public uint load(Width w, uint addr) {
            uint load = 0;
            addr -= memOffset;

            for(int i = 0; i < (byte)w; i++) {
                load |= (uint)(registers[addr + i] << (8 * i));
            }
            return load;
        }
        
        public void write(Width w, uint addr, uint value) {
            addr -= memOffset;

            for (int i = 0; i < (byte)w; i++) {
                registers[addr + i] = (byte)(value >> (8 * i));
            }
        }
    }
}
