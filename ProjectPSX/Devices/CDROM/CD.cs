using System.IO;
using System;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using static ProjectPSX.Devices.CdRom.TrackBuilder;

namespace ProjectPSX.Devices.CdRom {
    internal class CD {

        private const int BytesPerSectorRaw = 2352;
        private const int BytesPerSectorData = 2048;
        private const int BytesPerSubChannelHeader = 24;

        private byte[] rawSectorBuffer = new byte[BytesPerSectorRaw];
        private byte[] dataSectorBuffer = new byte[BytesPerSectorData + BytesPerSubChannelHeader];

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

            for(int i = 0; i < tracks.Count; i++) {
                Console.WriteLine($"Track {i} size: {tracks[i].size} lbaStart: {tracks[i].lbaStart} lbaEnd: {tracks[i].lbaEnd}");
            }
        }

        public byte[] Read(bool isSectorSizeRaw, int loc) {

            Track currentTrack = getTrackFromLoc(loc);

            //Console.WriteLine("Loc: " + loc + " TrackLbaStart: " + currentTrack.lbaStart);
            //Console.WriteLine("readPos = " + (loc - currentTrack.lbaStart));

            int position = (loc - currentTrack.lbaStart);
            if (position < 0) position = 0;

            using FileStream stream = new FileStream(currentTrack.file, FileMode.Open, FileAccess.Read);
            stream.Seek(position * BytesPerSectorRaw, SeekOrigin.Begin);
            if (isSectorSizeRaw) {
                stream.Read(rawSectorBuffer, 0, rawSectorBuffer.Length);
                return rawSectorBuffer;
            } else {
                stream.Read(dataSectorBuffer, 0, dataSectorBuffer.Length);
                return dataSectorBuffer;
            }

        }

        private Track getTrackFromLoc(int loc) {
            foreach(Track track in tracks) {
                //Console.WriteLine(loc + " " + track.file + track.lbaEnd);
                if (track.lbaEnd > loc) return track;
            }
            Console.WriteLine("[CD] WARNING: LBA beyond tracks!");
            return tracks[0]; //and explode ¯\_(ツ)_/¯ 
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
