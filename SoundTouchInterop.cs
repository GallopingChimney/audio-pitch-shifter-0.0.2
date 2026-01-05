using System;
using System.Runtime.InteropServices;

namespace AudioPitchShifter
{
    public class SoundTouchInterop : IDisposable
    {
        private IntPtr _handle;

        [DllImport("SoundTouchDLL_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_createInstance")]
        private static extern IntPtr CreateInstance();

        [DllImport("SoundTouchDLL_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_destroyInstance")]
        private static extern void DestroyInstance(IntPtr handle);

        [DllImport("SoundTouchDLL_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setRate")]
        private static extern void SetRate(IntPtr handle, float rate);

        [DllImport("SoundTouchDLL_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setTempo")]
        private static extern void SetTempo(IntPtr handle, float tempo);

        [DllImport("SoundTouchDLL_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setPitch")]
        private static extern void SetPitch(IntPtr handle, float pitch);

        [DllImport("SoundTouchDLL_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setPitchSemiTones")]
        private static extern void SetPitchSemiTones(IntPtr handle, float semiTones);

        [DllImport("SoundTouchDLL_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setChannels")]
        private static extern void SetChannels(IntPtr handle, uint channels);

        [DllImport("SoundTouchDLL_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setSampleRate")]
        private static extern void SetSampleRate(IntPtr handle, uint sampleRate);

        [DllImport("SoundTouchDLL_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_putSamples")]
        private static extern void PutSamples(IntPtr handle, float[] samples, uint numSamples);

        [DllImport("SoundTouchDLL_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_receiveSamples")]
        private static extern uint ReceiveSamples(IntPtr handle, float[] outBuffer, uint maxSamples);

        [DllImport("SoundTouchDLL_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_flush")]
        private static extern void Flush(IntPtr handle);

        [DllImport("SoundTouchDLL_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_clear")]
        private static extern void Clear(IntPtr handle);

        [DllImport("SoundTouchDLL_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_numSamples")]
        private static extern uint NumSamples(IntPtr handle);

        [DllImport("SoundTouchDLL_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_numUnprocessedSamples")]
        private static extern uint NumUnprocessedSamples(IntPtr handle);

        [DllImport("SoundTouchDLL_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "soundtouch_setSetting")]
        private static extern int SetSetting(IntPtr handle, int settingId, int value);

        // SoundTouch setting IDs
        private const int SETTING_USE_AA_FILTER = 0;
        private const int SETTING_SEQUENCE_MS = 2;
        private const int SETTING_SEEKWINDOW_MS = 3;
        private const int SETTING_OVERLAP_MS = 4;

        public SoundTouchInterop()
        {
            _handle = CreateInstance();
            if (_handle == IntPtr.Zero)
            {
                throw new Exception("Failed to create SoundTouch instance");
            }
        }

        public void SetPitchSemiTonesChange(float semiTones)
        {
            SetPitchSemiTones(_handle, semiTones);
        }

        public void Initialize(uint sampleRate, uint channels)
        {
            SetSampleRate(_handle, sampleRate);
            SetChannels(_handle, channels);

            // Enable high-quality settings for better frequency preservation
            SetSetting(_handle, SETTING_USE_AA_FILTER, 1);     // Enable anti-alias filter for high freq preservation
            SetSetting(_handle, SETTING_SEQUENCE_MS, 82);      // Default high-quality setting
            SetSetting(_handle, SETTING_SEEKWINDOW_MS, 28);    // Default high-quality setting
            SetSetting(_handle, SETTING_OVERLAP_MS, 12);       // Default high-quality setting
        }

        public void Process(float[] samples, uint numSamples)
        {
            PutSamples(_handle, samples, numSamples);
        }

        public uint Receive(float[] outBuffer, uint maxSamples)
        {
            return ReceiveSamples(_handle, outBuffer, maxSamples);
        }

        public uint AvailableSamples()
        {
            return NumSamples(_handle);
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                DestroyInstance(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}
