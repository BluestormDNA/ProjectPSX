using System.IO;
using System;
using System.Linq;
using System.Windows.Forms;

namespace ProjectPSX.Devices {
    internal class CD {

        private const int BytesPerSectorRaw = 2352;
        private const int BytesPerSectorData = 2048;
        private const int BytesPerSectorHeader = 24;
        private const int BytesPerSectorRawSyncHeader = 12;

        private FileStream stream;
        private BinaryReader reader;

        private string CdFilePath;

        private int lba;

        public CD() {

            var cla = Environment.GetCommandLineArgs();
            if (cla.Any(s => s.EndsWith(".bin"))) {
                CdFilePath = cla.First(s => s.EndsWith(".bin"));
            } else {
                //Show the user a dialog so they can pick the bin they want to load.
                var file = new OpenFileDialog();
                file.Filter = "Image files (*.bin, *.iso)|*.bin;*.iso";
                file.ShowDialog();
                CdFilePath = file.FileName;
            }

            stream = new FileStream(CdFilePath, FileMode.Open, FileAccess.Read);
            reader = new BinaryReader(stream);

            lba = (int)(stream.Length / BytesPerSectorRaw);

            Console.WriteLine($"[CD] LBA: {lba:x8}");
        }

        public byte[] Read(bool isSectorSizeRaw, int loc) {
            int bytesToRead;
            int sectorHeaderOffset;
            if (isSectorSizeRaw) {
                bytesToRead = BytesPerSectorRaw - BytesPerSectorRawSyncHeader;
                sectorHeaderOffset = BytesPerSectorRawSyncHeader;
                //Console.WriteLine("[CD] [Warning] RAW READ");
                //Console.ReadLine();
            } else {
                bytesToRead = BytesPerSectorData;
                sectorHeaderOffset = BytesPerSectorHeader;
            }

            long read = stream.Seek(loc * BytesPerSectorRaw + sectorHeaderOffset, 0);

            //Console.WriteLine("LOC = " + loc  + " " + ((loc * BYTES_PER_SECTOR_RAW) + sectorHeaderOffset).ToString("x8"));

            byte[] ret = reader.ReadBytes(bytesToRead);
            if (ret.Length != 0) return ret;

            //Console.WriteLine("[CD] [Warning] READ BEYOND LBA: Returning 0");
            return new byte[bytesToRead];
        }

        public int getLBA() {
            return lba;
        }

    }
}
