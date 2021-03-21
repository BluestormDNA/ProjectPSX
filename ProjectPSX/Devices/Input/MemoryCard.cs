using System;
using System.IO;

namespace ProjectPSX {
    public class MemoryCard {
        //emulating a 3rd party one as it seems easier to and 0x3FF bad address than to handle the
        //original memcard badAddress 0xFFFF error and the IdCommand
        private const byte MEMORY_CARD_ID_1 = 0x5A;
        private const byte MEMORY_CARD_ID_2 = 0x5D;
        private const byte MEMORY_CARD_COMMAND_ACK_1 = 0x5C;
        private const byte MEMORY_CARD_COMMAND_ACK_2 = 0x5D;
        private byte[] memory = new byte[128 * 1024]; //Standard memcard 128KB
        public bool ack = false;

        //FLAG
        //only bit 2 (isError) and 3 (isNotReaded) seems documented
        //bit 5 is useless for non sony memcards, default value is 0x80
        private const byte FLAG_ERROR = 0x4;
        private const byte FLAG_NOT_READED = 0x8;
        private byte flag = 0x8;

        private byte addressMSB;
        private byte addressLSB;
        private ushort address;

        private byte checksum;
        private int readPointer;
        private byte endTransfer;

        private string memCardFilePath = "./memcard.mcr";

        private enum Mode {
            Idle,
            Transfer
        }
        Mode mode = Mode.Idle;

        private enum TransferMode {
            Read,
            Write,
            Id,
            Undefined
        }
        TransferMode transferMode = TransferMode.Undefined;

        public MemoryCard() {
            try {
                memory = File.ReadAllBytes(memCardFilePath);

                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine("[MemCard] File found. Contents Loaded.");
                Console.ResetColor();
            } catch (Exception e) {
                Console.WriteLine("[MemCard] No Card found. Will try to generate a new one on save.");
            }
        }

        //This should be handled with some early response and post address queues but atm its easier to handle as a state machine
        internal byte process(byte value) {
            //Console.WriteLine($"[MemCard] rawProcess {value:x2} previous ack {ack}");
            switch (transferMode) {
                case TransferMode.Read: return readMemory(value);
                case TransferMode.Write: return writeMemory(value);
                case TransferMode.Id: return 0xFF;
            }

            switch (mode) {
                case Mode.Idle:
                    switch (value) {
                        case 0x81:
                            //Console.WriteLine("[MemCard] Idle Process 0x81");
                            mode = Mode.Transfer;
                            ack = true;
                            return 0xFF;
                        default:
                            //Console.WriteLine("[MemCard] Idle value WARNING " + value);
                            ack = false;
                            return 0xFF;
                    }

                case Mode.Transfer:
                    switch (value) {
                        case 0x52: //Read
                            //Console.WriteLine("[MemCard] Read Process 0x52");
                            transferMode = TransferMode.Read;
                            break;
                        case 0x57: //Write
                            //Console.WriteLine("[MemCard] Write Process 0x57");
                            transferMode = TransferMode.Write;
                            break;
                        case 0x53: //ID
                            //Console.WriteLine("[MemCard] ID Process 0x53");
                            transferMode = TransferMode.Undefined;
                            break;
                        default:
                            //Console.WriteLine($"[MemCard] Unhandled Transfer Process {value:x2}");
                            transferMode = TransferMode.Undefined;
                            ack = false;
                            return 0xFF;
                    }
                    byte prevFlag = flag;
                    ack = true;
                    flag &= unchecked((byte)~FLAG_ERROR);
                    return prevFlag;

                default:
                    //Console.WriteLine("[[MemCard]] Unreachable Mode Warning");
                    ack = false;
                    return 0xFF;
            }
        }

        internal void resetToIdle() {
            readPointer = 0;
            transferMode = TransferMode.Undefined;
            mode = Mode.Idle;
        }

        /*  Reading Data from Memory Card
            Send Reply Comment
            81h  N/A   Memory Card Access (unlike 01h=Controller access), dummy response
            52h  FLAG  Send Read Command (ASCII "R"), Receive FLAG Byte

            00h  5Ah   Receive Memory Card ID1
            00h  5Dh   Receive Memory Card ID2
            MSB  (00h) Send Address MSB  ;\sector number (0..3FFh)
            LSB  (pre) Send Address LSB  ;/
            00h  5Ch   Receive Command Acknowledge 1  ;<-- late /ACK after this byte-pair
            00h  5Dh   Receive Command Acknowledge 2
            00h  MSB   Receive Confirmed Address MSB
            00h  LSB   Receive Confirmed Address LSB
            00h  ...   Receive Data Sector (128 bytes)
            00h  CHK   Receive Checksum (MSB xor LSB xor Data bytes)
            00h  47h   Receive Memory End Byte (should be always 47h="G"=Good for Read)
        */
        private byte readMemory(byte value) {
            //Console.WriteLine($"[MemCard] readMemory pointer: {readPointer} value: {value:x2} ack {ack}");
            ack = true;
            switch (readPointer++) {
                case 0: return MEMORY_CARD_ID_1;
                case 1: return MEMORY_CARD_ID_2;
                case 2:
                    addressMSB = (byte)(value & 0x3);
                    return 0;
                case 3:
                    addressLSB = value;
                    address = (ushort)(addressMSB << 8 | addressLSB);
                    checksum = (byte)(addressMSB ^ addressLSB);
                    return 0;
                case 4: return MEMORY_CARD_COMMAND_ACK_1;
                case 5: return MEMORY_CARD_COMMAND_ACK_2;
                case 6: return addressMSB;
                case 7: return addressLSB;
                //from here handle the 128 bytes of the read sector frame
                case int index when (readPointer - 1) >= 8 && (readPointer - 1) < 8 + 128:
                    //Console.WriteLine($"Read readPointer {readPointer - 1} index {index}");
                    byte data = memory[(address * 128) + (index - 8)];
                    checksum ^= data;
                    return data;
                //sector frame ended after 128 bytes, handle checksum and finish
                case 8 + 128:
                    return checksum;
                case 9 + 128:
                    transferMode = TransferMode.Undefined;
                    mode = Mode.Idle;
                    readPointer = 0;
                    ack = false;
                    return 0x47;
                default:
                    Console.WriteLine($"[MemCard] Unreachable! {readPointer}");
                    transferMode = TransferMode.Undefined;
                    mode = Mode.Idle;
                    readPointer = 0;
                    ack = false;
                    return 0xFF;
            }
        }

        /*  Writing Data to Memory Card
            Send Reply Comment
            81h  N/A   Memory Card Access (unlike 01h=Controller access), dummy response
            57h  FLAG  Send Write Command (ASCII "W"), Receive FLAG Byte

            00h  5Ah   Receive Memory Card ID1
            00h  5Dh   Receive Memory Card ID2
            MSB  (00h) Send Address MSB  ;\sector number (0..3FFh)
            LSB  (pre) Send Address LSB  ;/
            ...  (pre) Send Data Sector (128 bytes)
            CHK  (pre) Send Checksum (MSB xor LSB xor Data bytes)
            00h  5Ch   Receive Command Acknowledge 1
            00h  5Dh   Receive Command Acknowledge 2
            00h  4xh   Receive Memory End Byte (47h=Good, 4Eh=BadChecksum, FFh=BadSector)
        */
        private byte writeMemory(byte value) {
            //Console.WriteLine($"[MemCard] writeMemory pointer: {readPointer} value: {value:x2} ack {ack}");
            switch (readPointer++) {
                case 0: return MEMORY_CARD_ID_1;
                case 1: return MEMORY_CARD_ID_2;
                case 2:
                    addressMSB = value;
                    return 0;
                case 3:
                    addressLSB = value;
                    address = (ushort)(addressMSB << 8 | addressLSB);
                    endTransfer = 0x47; //47h=Good

                    if (address > 0x3FF) {
                        flag |= FLAG_ERROR;
                        endTransfer = 0xFF; //FFh = BadSector
                        address &= 0x3FF;
                        addressMSB &= 0x3;
                    }
                    checksum = (byte)(addressMSB ^ addressLSB);
                    return 0;
                //from here handle the 128 bytes of the read sector frame
                case int index when (readPointer - 1) >= 4 && (readPointer - 1) < 4 + 128:
                    //Console.WriteLine($"Write readPointer {readPointer - 1} index {index} value {value:x2}");
                    memory[(address * 128) + (index - 4)] = value;
                    checksum ^= value;
                    return 0;
                //sector frame ended after 128 bytes, handle checksum and finish
                case 4 + 128:
                    if (checksum != value) {
                        //Console.WriteLine($"MemCard Write CHECKSUM WRONG was: {checksum:x2} expected: {value:x2}");
                        flag |= FLAG_ERROR;
                    } else {
                        //Console.WriteLine($"MemCard Write CHECKSUM OK was: {checksum:x2} expected: {value:x2}");
                    }
                    return 0;
                case 5 + 128: return MEMORY_CARD_COMMAND_ACK_1;
                case 6 + 128: return MEMORY_CARD_COMMAND_ACK_2;
                case 7 + 128:
                    //Console.WriteLine($"End WRITE Transfer with code {endTransfer:x2}");
                    transferMode = TransferMode.Undefined;
                    mode = Mode.Idle;
                    readPointer = 0;
                    ack = false;
                    flag &= unchecked((byte)~FLAG_NOT_READED);
                    handleSave();
                    return endTransfer;

                default:
                    //Console.WriteLine($"WARNING DEFAULT Write Memory readpointer ws {readPointer}");
                    transferMode = TransferMode.Undefined;
                    mode = Mode.Idle;
                    readPointer = 0;
                    ack = false;
                    return 0xFF;
            }
        }

        private void handleSave() {
            try {
                File.WriteAllBytes(memCardFilePath, memory);
                Console.WriteLine("[MemCard] Saved");
            } catch (Exception e) {
                Console.WriteLine("[MemCard] Error trying to save memCard file\n" + e);
            }
        }

        /*  Get Memory Card ID Command
            Send Reply Comment
            81h  N/A   Memory Card Access (unlike 01h=Controller access), dummy response
            53h  FLAG  Send Get ID Command (ASCII "S"), Receive FLAG Byte

            00h  5Ah   Receive Memory Card ID1
            00h  5Dh   Receive Memory Card ID2
            00h  5Ch   Receive Command Acknowledge 1
            00h  5Dh   Receive Command Acknowledge 2
            00h  04h   Receive 04h
            00h  00h   Receive 00h
            00h  00h   Receive 00h
            00h  80h   Receive 80h
        */
        private byte idMemory(byte value) {
            Console.WriteLine("[MEMORY CARD] WARNING Id UNHANDLED COMMAND");
            //Console.ReadLine();
            transferMode = TransferMode.Undefined;
            mode = Mode.Idle;
            return 0xFF;
        }

    }
}
