using System.IO;
using System;

namespace ProjectPSX.Devices {
    internal class CD {

        private const int BYTES_PER_SECTOR_RAW = 2352;
        private const int BYTES_PER_SECTOR_DATA = 2048;
        private const int BYTES_PER_SECTOR_HEADER = 24;
        private const int BYTES_PER_SECTOR_RAW_SYNC_HEADER = 12;

        private FileStream stream;
        private BinaryReader reader;

        public CD() {
            //temporary hard coded
            stream = new FileStream("./crash.bin", FileMode.Open, FileAccess.Read);
            reader = new BinaryReader(stream);
        }

        public byte[] read(bool isSectorSizeRAW, int loc) {
            int bytesToRead;
            int sectorHeaderOffset;
            if (isSectorSizeRAW){
                bytesToRead = BYTES_PER_SECTOR_RAW - BYTES_PER_SECTOR_RAW_SYNC_HEADER;
                sectorHeaderOffset = BYTES_PER_SECTOR_RAW_SYNC_HEADER;
                //Console.WriteLine("[CD] WARNING RAW READ !!!");
                //Console.ReadLine();
            } else {
                bytesToRead = BYTES_PER_SECTOR_DATA;
                sectorHeaderOffset = BYTES_PER_SECTOR_HEADER;
            }

            long read = stream.Seek(loc * BYTES_PER_SECTOR_RAW + sectorHeaderOffset, 0);

            //Console.WriteLine("LOC = " + loc  + " " + ((loc * BYTES_PER_SECTOR_RAW) + sectorHeaderOffset).ToString("x8"));

            byte[] ret = reader.ReadBytes(bytesToRead);

            //TODO: Beyond size reads return 0 reads //retrite this?
            if (ret.Length == 0) {
                Console.WriteLine("[CD] READ BEYOND SIZE! returning 0");
                return new byte[bytesToRead];
            }

            return ret;
        }

    }
}