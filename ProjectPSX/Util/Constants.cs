namespace ProjectPSX {

    public enum EX {
        LOAD_ADRESS_ERROR = 0x4,
        STORE_ADRESS_ERROR = 0x5,
        SYSCALL = 0x8,
        BREAK = 0x9,
        ILLEGAL_INSTR = 0xA,
        COPROCESSOR_ERROR = 0xB,
        OVERFLOW = 0xC
    }

    public enum Width {
        BYTE = 1,
        HALF = 2,
        WORD = 4
    }
}