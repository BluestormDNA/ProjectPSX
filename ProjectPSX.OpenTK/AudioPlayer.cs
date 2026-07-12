using System;
using System.Collections.Generic;
using OpenTK.Audio.OpenAL;

namespace ProjectPSX.OpenTK {

    public class AudioPlayer : IDisposable {
        private const int SampleRate = 44100;
        private const int BufferCount = 5;

        private ALDevice audioDevice;
        private ALContext audioContext;
        private int audioSource;
        private readonly Stack<int> freeBuffers = new Stack<int>(BufferCount);

        public bool fastForward;
        private readonly bool audioDisabled;

        public AudioPlayer() {
            audioDevice = ALC.OpenDevice(null);
            if (audioDevice == ALDevice.Null) {
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

            //Drop the samples if the queue is full (or on fast forward) instead of stalling the emulator
            if (fastForward || freeBuffers.Count == 0) return;

            int alBuffer = freeBuffers.Pop();
            AL.BufferData(alBuffer, ALFormat.Stereo16, samples, SampleRate);
            AL.SourceQueueBuffer(audioSource, alBuffer);

            if (GetSourceState(audioSource) != ALSourceState.Playing) {
                AL.SourcePlay(audioSource);
            }
        }

        private void ReclaimProcessedBuffers() {
            AL.GetSource(audioSource, ALGetSourcei.BuffersProcessed, out int processed);
            while (processed-- > 0) {
                freeBuffers.Push(AL.SourceUnqueueBuffer(audioSource));
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

            ALC.MakeContextCurrent(ALContext.Null);
            ALC.DestroyContext(audioContext);
            ALC.CloseDevice(audioDevice);
        }

        private static ALSourceState GetSourceState(int sid) {
            AL.GetSource(sid, ALGetSourcei.SourceState, out int value);
            return (ALSourceState)value;
        }
    }
}
