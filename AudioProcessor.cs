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
            _waveFormat = new WaveFormat(44100, 24, 2);
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
                BufferMilliseconds = preset.InputBufferMs
            };

            _bufferedWaveProvider = new BufferedWaveProvider(_waveFormat)
            {
                BufferLength = _waveFormat.SampleRate * _waveFormat.Channels * 4 * 2,
                DiscardOnBufferOverflow = true
            };

            _waveOut = new WaveOutEvent
            {
                DeviceNumber = outputDeviceNumber,
                DesiredLatency = preset.OutputBufferMs
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
                int bytesPerSample = _waveFormat.BitsPerSample / 8;
                int samplesRecorded = e.BytesRecorded / bytesPerSample;
                float[] floatSamples = new float[samplesRecorded];

                // Convert 24-bit samples to float
                for (int i = 0; i < samplesRecorded; i++)
                {
                    int sample24 = e.Buffer[i * 3] | (e.Buffer[i * 3 + 1] << 8) | (e.Buffer[i * 3 + 2] << 16);
                    // Sign-extend from 24-bit to 32-bit
                    if ((sample24 & 0x800000) != 0)
                        sample24 |= unchecked((int)0xFF000000);
                    floatSamples[i] = sample24 / 8388608f; // 2^23
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
                            byte[] outputBytes = new byte[receivedSamples * _waveFormat.Channels * bytesPerSample];

                            for (int i = 0; i < receivedSamples * _waveFormat.Channels; i++)
                            {
                                // Convert float to 24-bit integer
                                int sample24 = (int)(Math.Clamp(outputSamples[i], -1.0f, 1.0f) * 8388607f);
                                outputBytes[i * 3] = (byte)(sample24 & 0xFF);
                                outputBytes[i * 3 + 1] = (byte)((sample24 >> 8) & 0xFF);
                                outputBytes[i * 3 + 2] = (byte)((sample24 >> 16) & 0xFF);
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
