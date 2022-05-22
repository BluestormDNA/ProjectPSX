using System;
using System.Collections.Generic;
using System.IO;

namespace ProjectPSX.Devices.CdRom {
    public class TrackBuilder {

        private const int BytesPerSectorRaw = 2352;

        public class Track {

            public String file { get; private set; }
            public long size { get; private set; }
            public byte number { get; private set; }
            public int lba { get; private set; }
            public int lbaStart { get; private set; }
            public int lbaEnd { get; private set; }
            public bool isAudio { get; private set; }

            public Track(String file, long size, byte number, int lba, int lbaStart, int lbaEnd, bool isAudio) {
                this.file = file;
                this.size = size;
                this.number = number;
                this.lba = lba;
                this.lbaStart = lbaStart;
                this.lbaEnd = lbaEnd;
                this.isAudio = isAudio;
            }
        }

        public static List<Track> fromCue(String cue) {
            Console.WriteLine($"[CD Track Builder] Generating CD Tracks from: {cue}");
            List<Track> tracks = new List<Track>();
            String dir = Path.GetDirectoryName(cue);
            String line;
            int lbaCounter = 0;
            byte number = 0;
            using StreamReader cueFile = new StreamReader(cue);
            while ((line = cueFile.ReadLine()) != null) {
                if (line.StartsWith("FILE")) {
                    String[] splittedSring = line.Split("\"");

                    String file = dir + Path.DirectorySeparatorChar + splittedSring[1];
                    long size = new FileInfo(file).Length;
                    int lba = (int)(size / BytesPerSectorRaw);
                    int lbaStart = lbaCounter + 150;
                    number++;
                    //hardcoding :P
                    if (tracks.Count > 0) {
                        lbaStart += 150;
                    }

                    int lbaEnd = lbaCounter + lba;

                    lbaCounter += lba;

                    string trackTypeLine = cueFile.ReadLine();
                    bool isAudio = trackTypeLine.Contains("AUDIO");

                    tracks.Add(new Track(file, size, number, lba, lbaStart, lbaEnd, isAudio));

                    Console.WriteLine($"File: {file} Size: {size} Number: {number} LbaStart: {lbaStart} LbaEnd: {lbaEnd} isAudio {isAudio}");
                }
            }


            return tracks;
        }

        public static List<Track> fromBin(string file) {
            Console.WriteLine($"[CD Track Builder] Generating CD Track from: {file}");
            List<Track> tracks = new List<Track>();

            long size = new FileInfo(file).Length;
            int lba = (int)(size / BytesPerSectorRaw);
            int lbaStart = 150; // 150 frames (2 seconds) offset from track 1
            int lbaEnd = lba;
            byte number = 1;
            bool isAudio = false;

            tracks.Add(new Track(file, size, number, lba, lbaStart, lbaEnd, isAudio));

            Console.WriteLine($"File: {file} Size: {size} Number: {number} LbaStart: {lbaStart} LbaEnd: {lbaEnd} isAudio {isAudio}");

            return tracks;
        }
    }
}
