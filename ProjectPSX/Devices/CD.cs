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
        private const int BytesPerSubChannelInfo = 12;

        private byte[] rawSectorBuffer = new byte[BytesPerSectorRaw - BytesPerSectorRawSyncHeader];
        private byte[] dataSectorBuffer = new byte[BytesPerSectorData + BytesPerSubChannelInfo];

        private FileStream stream;
        private string CdFilePath;
        private int lba;

        public CD() {

            var cla = Environment.GetCommandLineArgs();
            if (cla.Any(s => s.EndsWith(".bin"))) {
                CdFilePath = cla.First(s => s.EndsWith(".bin"));
            } else {
                //Show the user a dialog so they can pick the bin they want to load.
                var file = new OpenFileDialog();
                file.Filter = "BIN files (*.bin)|*.bin";
                file.ShowDialog();
                CdFilePath = file.FileName;
            }

            stream = new FileStream(CdFilePath, FileMode.Open, FileAccess.Read);

            lba = (int)(stream.Length / BytesPerSectorRaw);

            Console.WriteLine($"[CD] LBA: {lba:x8}");
        }

        public byte[] Read(bool isSectorSizeRaw, int loc) {
            stream.Seek(loc * BytesPerSectorRaw + BytesPerSectorRawSyncHeader, SeekOrigin.Begin);
            if (isSectorSizeRaw) {
                stream.Read(rawSectorBuffer, 0, rawSectorBuffer.Length);
                return rawSectorBuffer;
            } else {
                stream.Read(dataSectorBuffer, 0, dataSectorBuffer.Length);
                return dataSectorBuffer;
            }
        }

        public int getLBA() {
            return lba;
        }

    }
}
