using System;
using OpenTK.Audio.OpenAL;
using OpenTK.Mathematics;

namespace ProjectPSX.OpenTK {

    public class AudioPlayer {
        int audioSource;
        ALDevice audioDevice;
        ALContext audioContext;
        int queueLength = 0;
        public bool fastForward;
        public bool audioDisabled;

        public AudioPlayer() {
            audioDevice = ALC.OpenDevice(null);
            if (audioDevice == null) {
                Console.WriteLine("Unable to create audio device");
                return;
            }
            audioContext = ALC.CreateContext(audioDevice, (int[])null);
            ALC.MakeContextCurrent(audioContext);
            audioSource = AL.GenSource();
            AL.Listener(ALListener3f.Position, 0, 0, 0);
            AL.Listener(ALListener3f.Velocity, 0, 0, 0);
            var orientation = new Vector3(0, 0, 0);
            AL.Listener(ALListenerfv.Orientation, ref orientation, ref orientation);
            fastForward = false;
            audioDisabled = false;
        }

        public unsafe void UpdateAudio(byte[] samples) {
            int processed = 0;
            int alBuffer = 0;

            while (true) {
                AL.GetSource(audioSource, ALGetSourcei.BuffersProcessed, out processed);
            
                while (processed-- > 0) {
                    AL.SourceUnqueueBuffers(audioSource, 1, ref alBuffer);
                    AL.DeleteBuffer(alBuffer);
                    queueLength--;
                }
            
                if (queueLength < 5 || fastForward) break;
            }
            
            if (queueLength < 5) {
                alBuffer = AL.GenBuffer();
                AL.BufferData(alBuffer, ALFormat.Stereo16, samples, 44100);
                AL.SourceQueueBuffer(audioSource, alBuffer);
                queueLength++;
            }
            
            if (AL.GetSourceState(audioSource) != ALSourceState.Playing)
                AL.SourcePlay(audioSource);

        }

        // Is this automatically called by GC?
        ~AudioPlayer() {
            ALC.DestroyContext(audioContext);
            ALC.CloseDevice(audioDevice);
        }
    }
}
