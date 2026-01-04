using System;
using System.Linq;
using NAudio.Wave;

namespace AudioPitchShifter
{
    public class AudioProcessor : IDisposable
    {
        private WaveInEvent? _waveIn;
        private WaveOutEvent? _waveOut;
        private BufferedWaveProvider? _bufferedWaveProvider;
        private SoundTouchInterop? _soundTouch;
        private WaveFormat _waveFormat;
        private float _pitchSemiTones = 0;
        private readonly object _lockObject = new object();
        private bool _isProcessing = false;

        public event EventHandler<float>? AudioLevelUpdated;

        public AudioProcessor()
        {
            _waveFormat = new WaveFormat(44100, 16, 2);
        }

        public void Initialize(int inputDeviceNumber, int outputDeviceNumber, LatencyPreset preset)
        {
            Stop();

            _soundTouch = new SoundTouchInterop();
            _soundTouch.Initialize((uint)_waveFormat.SampleRate, (uint)_waveFormat.Channels);
            _soundTouch.SetPitchSemiTonesChange(_pitchSemiTones);

            _waveIn = new WaveInEvent
            {
                DeviceNumber = inputDeviceNumber,
                WaveFormat = _waveFormat,
                BufferMilliseconds = preset.InputBufferMs,
                NumberOfBuffers = 3 // Use more buffers to prevent glitches
            };

            // Increase buffer to 4 seconds for smoother playback
            _bufferedWaveProvider = new BufferedWaveProvider(_waveFormat)
            {
                BufferLength = _waveFormat.SampleRate * _waveFormat.Channels * sizeof(short) * 4,
                DiscardOnBufferOverflow = true,
                ReadFully = false // Don't wait for full buffer, helps reduce latency
            };

            _waveOut = new WaveOutEvent
            {
                DeviceNumber = outputDeviceNumber,
                DesiredLatency = preset.OutputBufferMs,
                NumberOfBuffers = 3 // Use more buffers for stable playback
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveOut.Init(_bufferedWaveProvider);
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (!_isProcessing || _soundTouch == null || _bufferedWaveProvider == null)
                return;

            try
            {
                int samplesRecorded = e.BytesRecorded / 2;
                float[] floatSamples = new float[samplesRecorded];

                // Convert samples outside the lock
                for (int i = 0; i < samplesRecorded; i++)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, i * 2);
                    floatSamples[i] = sample / 32768f;
                }

                // Calculate level outside the lock
                float level = 0;
                if (floatSamples.Length > 0)
                {
                    level = floatSamples.Max(Math.Abs);
                }

                // Process audio with minimal lock time
                lock (_lockObject)
                {
                    uint numSamples = (uint)(samplesRecorded / _waveFormat.Channels);
                    _soundTouch.Process(floatSamples, numSamples);

                    uint availableSamples = _soundTouch.AvailableSamples();
                    if (availableSamples > 0)
                    {
                        float[] outputSamples = new float[availableSamples * _waveFormat.Channels];
                        uint receivedSamples = _soundTouch.Receive(outputSamples, availableSamples);

                        if (receivedSamples > 0)
                        {
                            byte[] outputBytes = new byte[receivedSamples * _waveFormat.Channels * 2];
                            for (int i = 0; i < receivedSamples * _waveFormat.Channels; i++)
                            {
                                short sample = (short)(Math.Clamp(outputSamples[i], -1.0f, 1.0f) * 32767f);
                                byte[] sampleBytes = BitConverter.GetBytes(sample);
                                outputBytes[i * 2] = sampleBytes[0];
                                outputBytes[i * 2 + 1] = sampleBytes[1];
                            }

                            _bufferedWaveProvider.AddSamples(outputBytes, 0, outputBytes.Length);
                        }
                    }
                }

                // Invoke event outside the lock to prevent UI thread blocking
                AudioLevelUpdated?.Invoke(this, level);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing audio: {ex.Message}");
            }
        }

        public void SetPitchSemiTones(float semiTones)
        {
            lock (_lockObject)
            {
                _pitchSemiTones = semiTones;
                _soundTouch?.SetPitchSemiTonesChange(_pitchSemiTones);
            }
        }

        public void Start()
        {
            if (_waveIn == null || _waveOut == null)
            {
                throw new InvalidOperationException("Audio processor not initialized. Call Initialize first.");
            }

            _isProcessing = true;
            _waveIn.StartRecording();
            _waveOut.Play();
        }

        public void Stop()
        {
            _isProcessing = false;

            if (_waveIn != null)
            {
                _waveIn.StopRecording();
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.Dispose();
                _waveIn = null;
            }

            if (_waveOut != null)
            {
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }

            if (_soundTouch != null)
            {
                _soundTouch.Dispose();
                _soundTouch = null;
            }

            _bufferedWaveProvider = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
