using System;

namespace ProjectPSX {

    public abstract class DMA_Transfer {

        public abstract void toGPU(uint value);

        public abstract void toGPU(uint[] buffer);

        public abstract uint fromRAM(uint addr);

        public abstract uint[] fromRAM(uint addr, uint size);

        public abstract void toRAM(uint addr, uint value);

        public abstract void toRAM(uint addr, byte[] buffer, uint size);

        public abstract uint fromGPU();

        public abstract uint fromCD();

        public abstract void toMDECin(uint[] load);

        public abstract uint fromMDECout();
    }
}
