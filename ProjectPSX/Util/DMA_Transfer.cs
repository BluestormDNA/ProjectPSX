namespace ProjectPSX {

    public interface DMA_Transfer {

        void toGPU(uint value);

        uint fromRAM(Width w, uint addr);

        void toRAM(Width w, uint addr, uint value);

        uint fromGPU();

        uint fromCD();
    }
}