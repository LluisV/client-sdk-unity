using System;
using System.Collections;
using UnityEngine;

namespace LiveKit
{
    sealed public class MicrophoneSource : RtcAudioSource
    {
        private readonly GameObject _sourceObject;
        private readonly string _deviceName;
        private readonly uint _sampleRate;

        public override event Action<float[], int, int> AudioRead;

        private bool _disposed = false;
        private bool _started = false;

        public MicrophoneSource(string deviceName, GameObject sourceObject, int channels = 2, uint sampleRate = 48000) 
            : base(channels, RtcAudioSourceType.AudioSourceMicrophone, sampleRate)
        {
            _deviceName = deviceName;
            _sourceObject = sourceObject;
            _sampleRate = sampleRate;
        }

        public override void Start()
        {
            base.Start();
            if (_started) return;

            if (!Application.HasUserAuthorization(mode: UserAuthorization.Microphone))
                throw new InvalidOperationException("Microphone access not authorized");

            MonoBehaviourContext.OnApplicationPauseEvent += OnApplicationPause;
            MonoBehaviourContext.RunCoroutine(StartMicrophone());

            _started = true;
        }

        private IEnumerator StartMicrophone()
        {
            var clip = Microphone.Start(
                _deviceName,
                loop: true,
                lengthSec: 1,
                frequency: (int)_sampleRate  // <-- Now uses the actual detected sample rate
            );
            if (clip == null)
                throw new InvalidOperationException("Microphone start failed");

            var source = _sourceObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;

            var probe = _sourceObject.AddComponent<AudioProbe>();
            probe.ClearAfterInvocation();
            probe.AudioRead += OnAudioRead;

            var waitUntilReady = new WaitUntil(() => Microphone.GetPosition(_deviceName) > 0);
            yield return waitUntilReady;
            source.Play();
        }

        public override void Stop()
        {
            base.Stop();
            MonoBehaviourContext.RunCoroutine(StopMicrophone());
            MonoBehaviourContext.OnApplicationPauseEvent -= OnApplicationPause;
            _started = false;
        }

        private IEnumerator StopMicrophone()
        {
            if (Microphone.IsRecording(_deviceName))
                Microphone.End(_deviceName);

            var probe = _sourceObject.GetComponent<AudioProbe>();
            probe.AudioRead -= OnAudioRead;
            UnityEngine.Object.Destroy(probe);

            var source = _sourceObject.GetComponent<AudioSource>();
            UnityEngine.Object.Destroy(source);
            yield return null;
        }

        private void OnAudioRead(float[] data, int channels, int sampleRate)
        {
            AudioRead?.Invoke(data, channels, sampleRate);
        }

        private void OnApplicationPause(bool pause)
        {
            if (!pause && _started)
                MonoBehaviourContext.RunCoroutine(RestartMicrophone());
        }

        private IEnumerator RestartMicrophone()
        {
            yield return StopMicrophone();
            yield return StartMicrophone();
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing) Stop();
            _disposed = true;
            base.Dispose(disposing);
        }

        ~MicrophoneSource()
        {
            Dispose(false);
        }
    }
}