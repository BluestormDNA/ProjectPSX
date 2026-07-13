using System;
using System.Collections.Generic;
using OpenTK.Audio.OpenAL;
using OpenTK.Audio.OpenAL.ALC;

namespace ProjectPSX.OpenTK {

    public class AudioPlayer : IDisposable {
        private const int SampleRate = 44100;
        private const int BufferCount = 5;

        private ALCDevice audioDevice;
        private ALCContext audioContext;
        private int audioSource;
        private readonly Stack<int> freeBuffers = new Stack<int>(BufferCount);

        private readonly bool audioDisabled;

        public AudioPlayer() {
            audioDevice = ALC.OpenDevice(null);
            if (audioDevice == ALCDevice.Null) {
                Console.WriteLine("[AUDIO] Unable to open the audio device. Audio disabled.");
                audioDisabled = true;
                return;
            }
            audioContext = ALC.CreateContext(audioDevice, (int[])null);
            ALC.MakeContextCurrent(audioContext);

            audioSource = AL.GenSource();
            for (int i = 0; i < BufferCount; i++) {
                freeBuffers.Push(AL.GenBuffer());
            }
        }

        public void UpdateAudio(byte[] samples) {
            if (audioDisabled) return;

            ReclaimProcessedBuffers();

            //Drop the samples if the queue is full instead of stalling the emulator.
            //This also plays what it can on fast forward like the WinForms NAudio discard does.
            if (freeBuffers.Count == 0) return;

            int alBuffer = freeBuffers.Pop();
            AL.BufferData(alBuffer, Format.Stereo16, samples, samples.Length, SampleRate);
            AL.SourceQueueBuffers(audioSource, 1, ref alBuffer);

            if (GetSourceState(audioSource) != SourceState.Playing) {
                AL.SourcePlay(audioSource);
            }
        }

        private void ReclaimProcessedBuffers() {
            int processed = AL.GetSourcei(audioSource, SourceGetPNameI.BuffersProcessed);
            while (processed-- > 0) {
                int alBuffer = 0;
                AL.SourceUnqueueBuffers(audioSource, 1, ref alBuffer);
                freeBuffers.Push(alBuffer);
            }
        }

        public void Dispose() {
            if (audioDisabled) return;

            AL.SourceStop(audioSource);
            ReclaimProcessedBuffers();
            AL.DeleteSource(audioSource);
            while (freeBuffers.Count > 0) {
                AL.DeleteBuffer(freeBuffers.Pop());
            }

            ALC.MakeContextCurrent(ALCContext.Null);
            ALC.DestroyContext(audioContext);
            ALC.CloseDevice(audioDevice);
        }

        private static SourceState GetSourceState(int sid) {
            return (SourceState)AL.GetSourcei(sid, SourceGetPNameI.SourceState);
        }
    }
}
