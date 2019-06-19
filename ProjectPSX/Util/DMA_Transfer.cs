namespace ProjectPSX {

    public interface DMA_Transfer {

        void toGPU(uint value);

        void toGPU(uint[] buffer);

        uint fromRAM(uint addr);

        uint[] fromRAM(uint addr, uint size);

        void toRAM(uint addr, uint value);

        void toRAM(uint addr, byte[] buffer, uint size);

        uint fromGPU();

        uint fromCD();

        byte[] fromCD(uint size);
    }
}