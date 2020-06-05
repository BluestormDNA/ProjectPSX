using System.IO;
using System;
using System.Linq;
using System.Windows.Forms;
using ProjectPSX.Util;
using System.Collections.Generic;
using static ProjectPSX.Util.TrackBuilder;

namespace ProjectPSX.Devices {
    internal class CD {

        private const int BytesPerSectorRaw = 2352;
        private const int BytesPerSectorData = 2048;
        private const int BytesPerSectorHeader = 24;
        private const int BytesPerSectorRawSyncHeader = 12;
        private const int BytesPerSubChannelInfo = 12;

        private byte[] rawSectorBuffer = new byte[BytesPerSectorRaw - BytesPerSectorRawSyncHeader];
        private byte[] dataSectorBuffer = new byte[BytesPerSectorData + BytesPerSubChannelInfo];

        public List<Track> tracks;

        public CD() {

            var cla = Environment.GetCommandLineArgs();
            if (cla.Any(s => s.EndsWith(".bin"))) {
                String file = cla.First(s => s.EndsWith(".bin"));
                tracks = TrackBuilder.fromBin(file);
            } else {
                //Show the user a dialog so they can pick the bin they want to load.
                var fileDialog = new OpenFileDialog();
                fileDialog.Filter = "BIN/CUE files (*.bin, *.cue)|*.bin;*.cue";
                fileDialog.ShowDialog();

                string file = fileDialog.FileName;
                string ext = Path.GetExtension(file);

                if(ext == ".bin") {
                    tracks = TrackBuilder.fromBin(file);
                } else if (ext == ".cue"){
                    tracks = TrackBuilder.fromCue(file);
                }
            }
        }

        public byte[] Read(bool isSectorSizeRaw, int loc) {
            string currentTrack = getTrackFromLoc(loc);
            using FileStream stream = new FileStream(currentTrack, FileMode.Open, FileAccess.Read);
            stream.Seek(loc * BytesPerSectorRaw + BytesPerSectorRawSyncHeader, SeekOrigin.Begin);
            if (isSectorSizeRaw) {
                stream.Read(rawSectorBuffer, 0, rawSectorBuffer.Length);
                return rawSectorBuffer;
            } else {
                stream.Read(dataSectorBuffer, 0, dataSectorBuffer.Length);
                return dataSectorBuffer;
            }

        }

        private string getTrackFromLoc(int loc) {
            foreach(Track track in tracks) {
                //Console.WriteLine(loc + " " + track.file + track.lbaEnd);
                if (track.lbaEnd > loc) return track.file;
            }
            Console.WriteLine("[CD] WARNING: LBA beyond tracks!");
            return tracks[0].file; //and explode ¯\_(ツ)_/¯ 
        }

        public int getLBA() {
            int lba = 150;

            foreach (Track track in tracks) {
                lba += track.lba;
            }
            Console.WriteLine($"[CD] LBA: {lba:x8}");
            return lba;
        }

    }
}
