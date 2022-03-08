using System;
using System.Collections.Generic;
using System.IO;

namespace ProjectPSX.Devices.CdRom
{
    public class CD {
        private const int BYTES_PER_SECTOR_RAW = 2352;
        private const int PRE_GAP = 150;

        private readonly byte[] rawSectorBuffer = new byte[BYTES_PER_SECTOR_RAW];

        private readonly FileStream[] streams;

        public bool isTrackChange;

        public List<Track> tracks;

        public CD(string diskFilename) {
            var ext = Path.GetExtension(diskFilename);

            if (ext == ".bin") {
                tracks = TrackBuilder.FromBin(diskFilename);
            } else if (ext == ".cue") {
                tracks = TrackBuilder.FromCue(diskFilename);
            } else if (ext == ".exe") {
                // TODO: THERES NOT ONLY NO CD BUT ANY ACCESS TO THE CDROM WILL THROW.
                // EXES THAT ACCES THE CDROM WILL CURRENTLY CRASH.
                return;
            }

            streams = new FileStream[tracks.Count];

            for (var i = 0; i < tracks.Count; i++) {
                streams[i] = new FileStream(tracks[i].File, FileMode.Open, FileAccess.Read);
                Console.WriteLine(
                    $"Track {i} size: {tracks[i].FileLength} lbaStart: {tracks[i].LbaStart} lbaEnd: {tracks[i].LbaEnd}");
            }
        }

        public byte[] Read(int loc)
        {
            // BUG we can still hear some garbage during audio track transitions, should the buffer be cleared when we're in a pre-gap?

            var track = getTrackFromLoc(loc);

            //Console.WriteLine("Loc: " + loc + " TrackLbaStart: " + currentTrack.lbaStart);
            //Console.WriteLine("readPos = " + (loc - currentTrack.lbaStart));

            var position = loc - track.LbaStart;
            if (position < 0) 
                position = 0;

            var stream = streams[track.Index - 1];

            // NOTE: because we now support INDEX in .CUE and consolidated .BIN, we need to adjust position

            position -= PRE_GAP; 

            if (track.Indices.Count > 1)
            {
                position += PRE_GAP; // assuming .CUE is compliant, i.e. two INDEX for an audio track
            }

            position = (int)(position * BYTES_PER_SECTOR_RAW + track.FilePosition); // correct seek for any .BIN flavor

            stream.Seek(position, SeekOrigin.Begin);

            var size = rawSectorBuffer.Length;
            var read = stream.Read(rawSectorBuffer, 0, size);

            if (read != size)
            {
                Console.WriteLine($"[CD] ERROR: Could only read {read} of {size} bytes from {stream.Name}.");
            }

            return rawSectorBuffer;
        }

        public Track getTrackFromLoc(int loc) {
            foreach (var track in tracks) {
                isTrackChange = loc == track.LbaEnd;
                //Console.WriteLine(loc + " " + track.number + " " + track.lbaEnd + " " + isTrackChange);
                if (track.LbaEnd >= loc) return track;
            }

            Console.WriteLine("[CD] WARNING: LBA beyond tracks!");
            return tracks[0]; //and explode ¯\_(ツ)_/¯ 
        }

        public int getLBA() {
            var lba = 150; // BUG see if this is still needed because of the new way of .CUE is being parsed

            foreach (var track in tracks) {
                lba += track.LbaLength;
            }

            Console.WriteLine($"[CD] LBA: {lba:x8}");
            return lba;
        }
    }
}
