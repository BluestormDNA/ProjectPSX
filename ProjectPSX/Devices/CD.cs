using System.IO;
using System;
using System.Windows.Forms;

namespace ProjectPSX.Devices {
    internal class CD {

        private const int BytesPerSectorRaw = 2352;
        private const int BytesPerSectorData = 2048;
        private const int BytesPerSectorHeader = 24;
        private const int BytesPerSectorRawSyncHeader = 12;

        private FileStream stream;
        private BinaryReader reader;

        public CD() {
            //Show the user a dialog so they can pick the bin they want to load.
            var file = new OpenFileDialog();
            file.Filter = "BIN files (*.bin)|*.bin";
            file.ShowDialog();
            stream = new FileStream(file.FileName, FileMode.Open, FileAccess.Read);
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