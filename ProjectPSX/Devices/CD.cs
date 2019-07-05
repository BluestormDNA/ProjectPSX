using System.IO;
using System;

namespace ProjectPSX.Devices {
    internal class CD {

        private const int BytesPerSectorRaw = 2352;
        private const int BytesPerSectorData = 2048;
        private const int BytesPerSectorHeader = 24;
        private const int BytesPerSectorRawSyncHeader = 12;

        private FileStream stream;
        private BinaryReader reader;

        public CD() {
            //temporary hard coded Ideally this should be set from a simple drag and drop to the emu
            stream = new FileStream("../rr.bin", FileMode.Open, FileAccess.Read);
            reader = new BinaryReader(stream);
        }

        public byte[] Read(bool isSectorSizeRaw, int loc) {
            int bytesToRead;
            int sectorHeaderOffset;
            if (isSectorSizeRaw){
                bytesToRead = BytesPerSectorRaw - BytesPerSectorRawSyncHeader;
                sectorHeaderOffset = BytesPerSectorRawSyncHeader;
                //Console.WriteLine("[CD] WARNING RAW READ !!!");
                //Console.ReadLine();
            } else {
                bytesToRead = BytesPerSectorData;
                sectorHeaderOffset = BytesPerSectorHeader;
            }

            long read = stream.Seek(loc * BytesPerSectorRaw + sectorHeaderOffset, 0);

            //Console.WriteLine("LOC = " + loc  + " " + ((loc * BYTES_PER_SECTOR_RAW) + sectorHeaderOffset).ToString("x8"));

            byte[] ret = reader.ReadBytes(bytesToRead);
            if (ret.Length != 0) return ret;

            Console.WriteLine("[CD] READ BEYOND SIZE! returning zeros");
            return new byte[bytesToRead];
        }

    }
}