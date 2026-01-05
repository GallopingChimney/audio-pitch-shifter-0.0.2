# Real-Time Audio Pitch Shifter

A Windows application for real-time pitch shifting and tempo adjustment for guitar practice and audio playback.

## Features

- Real-time pitch shifting in semitone intervals (-12 to +12 semitones)
- Automatic tempo adjustment to maintain original tempo
- Support for VB-Audio Virtual Cable
- Visual audio level monitoring
- Low-latency audio processing using NAudio and SoundTouch

## Requirements

- Windows 10 or later
- .NET 8.0 Runtime
- VB-Audio Virtual Cable (optional, for routing audio)

## Building the Application

1. Ensure you have .NET 8.0 SDK installed
2. Open a terminal in the project directory
3. Run:
   ```
   dotnet restore AudioPitchShifter.csproj
   dotnet build AudioPitchShifter.csproj -c Release
   ```

## Running the Application

Run the built executable:
```
.\bin\Release\net8.0-windows\AudioPitchShifter.exe
```

Or use:
```
dotnet run --project AudioPitchShifter.csproj
```

## How to Use

1. **Select Input Device**: Choose your audio input (microphone, line-in, or VB-Audio Cable)
2. **Select Output Device**: Choose your audio output (speakers, headphones, or VB-Audio Cable)
3. **Adjust Pitch**: Use the slider to shift pitch in semitone intervals
   - Range: -12 to +12 semitones (one octave down to one octave up)
   - Each semitone is equivalent to one fret on a guitar
4. **Adjust Latency**: Set the latency slider to control responsiveness
   - Range: 20ms to 200ms
   - Lower values = faster response, but may cause audio glitches on slower systems
   - Higher values = more stable, but noticeable delay
   - Recommended starting point: 50ms for most systems
5. **Start Processing**: Click "Start" to begin real-time audio processing
6. **Monitor Levels**: Watch the audio level meter to ensure proper signal

## Common Use Cases

### Guitar Practice with Pitch Shift
1. Connect your guitar to your audio interface
2. Select your audio interface as the input device
3. Select your speakers/headphones as the output device
4. Adjust pitch to match the desired tuning
5. Click Start and play along

### Using VB-Audio Virtual Cable
You can route audio from media players through the pitch shifter:

1. Set your Windows sound output to "CABLE / VB-Audio Cable Input"
2. In the application, select "CABLE / VB-Audio Cable Output" as input
3. Select your speakers/headphones as output
4. Adjust pitch as desired
5. Click Start and play your media.

## Pitch Shift Reference

- -12 semitones: One octave down
- -7 semitones: Perfect fifth down
- -5 semitones: Perfect fourth down
- -2 semitones: Whole step down (Drop D equivalent)
- -1 semitone: Half step down
- 0 semitones: No change
- +1 semitone: Half step up
- +2 semitones: Whole step up
- +12 semitones: One octave up

## Troubleshooting

### No Audio Output
- Check that your audio devices are properly connected
- Ensure the correct input/output devices are selected
- Check Windows audio settings and volume levels

### Audio Distortion
- Lower the input volume on your audio interface
- Check the audio level meter - it should not constantly max out
- Ensure your audio drivers are up to date

## Technical Details

- **Audio Processing**: SoundTouch library for pitch shifting
- **Audio I/O**: NAudio library (WaveIn/WaveOut)
- **Sample Rate**: 44.1 kHz
- **Bit Depth**: 16-bit
- **Channels**: Stereo (2 channels)
- **Buffer Size**: 50ms input, 100ms output

## License

This application uses:
- SoundTouch Audio Processing Library (LGPL v2.1)
- NAudio (MIT License)
