using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectPSX.Devices.CdRom
{
    public static class TrackBuilder
    {
        private const int BytesPerSectorRaw = 2352;

        public static List<Track> FromBin(string file)
        {
            Console.WriteLine($"[CD Track Builder] Generating CD Track from: {file}");
            var tracks = new List<Track>();

            var size = new FileInfo(file).Length;
            var lba = (int)(size / BytesPerSectorRaw);
            var lbaStart = 150; // 150 frames (2 seconds) offset from track 1
            var lbaEnd = lba;
            byte number = 1;

            tracks.Add(new Track(file, size, number, lbaStart, lbaEnd));

            Console.WriteLine($"File: {file} Size: {size} Number: {number} LbaStart: {lbaStart} LbaEnd: {lbaEnd}");

            return tracks;
        }

        [SuppressMessage("ReSharper", "RedundantJumpStatement")]
        [SuppressMessage("ReSharper", "InvertIf")]
        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
        public static List<Track> FromCue(string path)
            // NOTE: this parsing outputs exactly like IsoBuster would
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));

            Console.WriteLine($"[CD Track Builder] Generating CD Tracks for: {path}");

            var directory = Path.GetDirectoryName(path) ?? throw new NotImplementedException(); // TODO root case

            using var reader = new StreamReader(path);

            var tracks = new List<Track>();

            const RegexOptions options = RegexOptions.Singleline | RegexOptions.Compiled;

            var rf = new Regex(@"^\s*FILE\s+("".*"")\s+BINARY\s*$", options);
            var rt = new Regex(@"^\s*TRACK\s+(\d{2})\s+(MODE2/2352|AUDIO)\s*$", options);
            var ri = new Regex(@"^\s*INDEX\s+(\d{2})\s+(\d{2}):(\d{2}):(\d{2})\s*$", options);

            var files = new HashSet<string>();

            var currentFile = default(string?);
            var currentTrack = default(Track?);

            string? line;

            var lineNumber = 0;

            while ((line = reader.ReadLine()) is not null)
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fileMatch = rf.Match(line);
                if (fileMatch.Success)
                {
                    files.Add(currentFile = Path.Combine(directory, fileMatch.Groups[1].Value.Trim('"')));
                    continue;
                }

                var trackMatch = rt.Match(line);
                if (trackMatch.Success)
                {
                    if (currentFile is null)
                        throw new InvalidDataException($"TRACK at line {lineNumber} does not have a parent FILE.");

                    currentTrack = new Track
                    {
                        File = currentFile,
                        Index = Convert.ToByte(trackMatch.Groups[1].Value)
                    };

                    tracks.Add(currentTrack);

                    continue;
                }

                var indexMatch = ri.Match(line);
                if (indexMatch.Success)
                {
                    if (currentTrack is null)
                        throw new InvalidDataException($"INDEX at line {lineNumber} does not have a parent TRACK.");

                    var n = Convert.ToInt32(indexMatch.Groups[1].Value);
                    var m = Convert.ToInt32(indexMatch.Groups[2].Value);
                    var s = Convert.ToInt32(indexMatch.Groups[3].Value);
                    var f = Convert.ToInt32(indexMatch.Groups[4].Value);

                    currentTrack.Indices.Add(new TrackIndex(n, new TrackPosition(m, s, f)));

                    continue;
                }
            }

            if (files.Count is 1)
            {
                var length = new FileInfo(files.Single()).Length;

                for (var i = 0; i < tracks.Count; i++)
                {
                    var track = tracks[i];

                    track.LbaStart = track.Indices.Last().Position.ToInt32();

                    if (i == tracks.Count - 1)
                    {
                        track.LbaEnd = (int)(length / BytesPerSectorRaw - 1);
                    }
                    else
                    {
                        track.LbaEnd = tracks[i + 1].Indices.First().Position.ToInt32() - 1;
                    }

                    track.LbaLength = track.LbaEnd - track.LbaStart + 1;

                    track.FilePosition = track.LbaStart * BytesPerSectorRaw;
                }
            }
            else
            {
                var lba = 0;

                foreach (var track in tracks)
                {
                    var length = new FileInfo(track.File).Length;
                    var blocks = length / BytesPerSectorRaw;

                    track.LbaStart = lba;

                    foreach (var index in track.Indices)
                    {
                        track.LbaStart += index.Position.ToInt32(); // pre-gap
                    }

                    track.LbaEnd = (int)(track.LbaStart + blocks - 1);

                    foreach (var index in track.Indices)
                    {
                        track.LbaEnd -= index.Position.ToInt32(); // pre-gap
                    }

                    track.LbaLength = track.LbaEnd - track.LbaStart + 1;

                    track.FilePosition = track.LbaStart * BytesPerSectorRaw;

                    lba += (int)blocks;
                }
            }

            return tracks;
        }
    }
}
