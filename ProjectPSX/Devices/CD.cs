using System.IO;
using System;

namespace ProjectPSX.Devices {
    internal class CD {

        private const int BYTES_PER_SECTOR_RAW = 2352;
        private const int BYTES_PER_SECTOR_DATA = 2048;
        private const int BYTES_PER_SECTOR_HEADER = 24;

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
                bytesToRead = BYTES_PER_SECTOR_RAW;
                sectorHeaderOffset = 0;
            } else {
                bytesToRead = BYTES_PER_SECTOR_DATA;
                sectorHeaderOffset = BYTES_PER_SECTOR_HEADER;
            }

            stream.Seek(loc * BYTES_PER_SECTOR_RAW + sectorHeaderOffset, 0);
            //Console.WriteLine("LOC = " + loc  + " " + ((loc * BYTES_PER_SECTOR_RAW) + sectorHeaderOffset).ToString("x8"));
            //Console.ReadLine();
            return reader.ReadBytes(bytesToRead);
        }

    }
}