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
        private const int FFT_SIZE = 2048;
        private readonly float[] _fftBuffer = new float[FFT_SIZE];
        private int _fftPos = 0;

        public event EventHandler<float>? AudioLevelUpdated;
        public event EventHandler<float[]>? SpectrumDataUpdated;

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

                // Update FFT buffer and calculate spectrum
                UpdateFFTBuffer(floatSamples);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing audio: {ex.Message}");
            }
        }

        private void UpdateFFTBuffer(float[] samples)
        {
            // Use only the left channel for FFT (every other sample in stereo)
            for (int i = 0; i < samples.Length && _fftPos < FFT_SIZE; i += _waveFormat.Channels)
            {
                _fftBuffer[_fftPos++] = samples[i];
            }

            // When buffer is full, perform FFT analysis
            if (_fftPos >= FFT_SIZE)
            {
                _fftPos = 0;
                float[] spectrum = PerformFFT(_fftBuffer);
                SpectrumDataUpdated?.Invoke(this, spectrum);
            }
        }

        private float[] PerformFFT(float[] buffer)
        {
            const int NUM_BANDS = 36;
            float[] spectrum = new float[NUM_BANDS];

            // Apply Hamming window
            float[] windowed = new float[FFT_SIZE];
            for (int i = 0; i < FFT_SIZE; i++)
            {
                windowed[i] = buffer[i] * (0.54f - 0.46f * (float)Math.Cos(2.0 * Math.PI * i / (FFT_SIZE - 1)));
            }

            // Perform FFT (simplified - using magnitude calculation)
            Complex[] fftResult = new Complex[FFT_SIZE];
            for (int i = 0; i < FFT_SIZE; i++)
            {
                fftResult[i] = new Complex(windowed[i], 0);
            }

            FFT(fftResult, FFT_SIZE);

            // Group frequencies into bands (logarithmic distribution)
            int usableSize = FFT_SIZE / 2; // Only use first half (Nyquist)
            for (int band = 0; band < NUM_BANDS; band++)
            {
                // Logarithmic frequency distribution
                float freqStart = (float)Math.Pow(2, band * 10.0 / NUM_BANDS);
                float freqEnd = (float)Math.Pow(2, (band + 1) * 10.0 / NUM_BANDS);

                int binStart = (int)(freqStart * usableSize / 1024);
                int binEnd = (int)(freqEnd * usableSize / 1024);
                binEnd = Math.Min(binEnd, usableSize);

                if (binStart >= binEnd) binEnd = binStart + 1;
                if (binEnd > usableSize) binEnd = usableSize;

                float sum = 0;
                for (int i = binStart; i < binEnd; i++)
                {
                    sum += fftResult[i].Magnitude;
                }

                float avg = sum / (binEnd - binStart);

                // Apply logarithmic scaling to compress dynamic range
                // Lower frequencies have more energy, so we compress them more
                float dbValue = 20.0f * (float)Math.Log10(Math.Max(avg, 0.00001f));
                float normalized = (dbValue + 60.0f) / 60.0f; // Map -60dB to 0dB -> 0 to 1

                spectrum[band] = Math.Max(0.0f, Math.Min(1.0f, normalized)); // Clamp to 0-1
            }

            return spectrum;
        }

        private void FFT(Complex[] data, int n)
        {
            // Bit-reversal permutation
            int j = 0;
            for (int i = 0; i < n - 1; i++)
            {
                if (i < j)
                {
                    (data[i], data[j]) = (data[j], data[i]);
                }
                int k = n / 2;
                while (k <= j)
                {
                    j -= k;
                    k /= 2;
                }
                j += k;
            }

            // Cooley-Tukey FFT
            for (int size = 2; size <= n; size *= 2)
            {
                double angle = -2.0 * Math.PI / size;
                Complex wlen = new Complex(Math.Cos(angle), Math.Sin(angle));

                for (int i = 0; i < n; i += size)
                {
                    Complex w = new Complex(1, 0);
                    for (int m = 0; m < size / 2; m++)
                    {
                        Complex u = data[i + m];
                        Complex v = w * data[i + m + size / 2];
                        data[i + m] = u + v;
                        data[i + m + size / 2] = u - v;
                        w *= wlen;
                    }
                }
            }
        }

        private struct Complex
        {
            public double Real;
            public double Imaginary;

            public Complex(double real, double imaginary)
            {
                Real = real;
                Imaginary = imaginary;
            }

            public float Magnitude => (float)Math.Sqrt(Real * Real + Imaginary * Imaginary);

            public static Complex operator +(Complex a, Complex b) =>
                new Complex(a.Real + b.Real, a.Imaginary + b.Imaginary);

            public static Complex operator -(Complex a, Complex b) =>
                new Complex(a.Real - b.Real, a.Imaginary - b.Imaginary);

            public static Complex operator *(Complex a, Complex b) =>
                new Complex(
                    a.Real * b.Real - a.Imaginary * b.Imaginary,
                    a.Real * b.Imaginary + a.Imaginary * b.Real
                );
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
