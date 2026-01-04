using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NAudio.Wave;

namespace AudioPitchShifter
{
    public partial class MainWindow : Window
    {
        private AudioProcessor? _audioProcessor;
        private int _selectedInputDevice = 0;
        private int _selectedOutputDevice = 0;
        private readonly LatencyPreset _lowLatencyPreset = new LatencyPreset("Low", 20, 100, "Optimized for quality");
        private System.Windows.Threading.DispatcherTimer? _uiUpdateTimer;
        private int _currentPitchSemitones = 0;

        public MainWindow()
        {
            InitializeComponent();
            LoadAudioDevices();

            _uiUpdateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _uiUpdateTimer.Tick += UiUpdateTimer_Tick;
        }

        private void UiUpdateTimer_Tick(object? sender, EventArgs e)
        {
            UpdateStatusText();
        }

        private void LoadAudioDevices()
        {
            InputDeviceComboBox.Items.Clear();
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var capabilities = WaveInEvent.GetCapabilities(i);
                InputDeviceComboBox.Items.Add($"{i}: {capabilities.ProductName}");
            }

            if (InputDeviceComboBox.Items.Count > 0)
            {
                InputDeviceComboBox.SelectedIndex = 0;
            }

            OutputDeviceComboBox.Items.Clear();
            for (int i = -1; i < WaveOut.DeviceCount; i++)
            {
                var capabilities = WaveOut.GetCapabilities(i);
                OutputDeviceComboBox.Items.Add($"{i}: {capabilities.ProductName}");
            }

            if (OutputDeviceComboBox.Items.Count > 0)
            {
                OutputDeviceComboBox.SelectedIndex = 1;
            }
        }


        private void InputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedInputDevice = InputDeviceComboBox.SelectedIndex;
        }

        private void OutputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedOutputDevice = OutputDeviceComboBox.SelectedIndex - 1;
        }

        private void PitchSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PitchValueText != null)
            {
                _currentPitchSemitones = (int)e.NewValue;
                PitchValueText.Text = _currentPitchSemitones >= 0 ? $"+{_currentPitchSemitones}" : _currentPitchSemitones.ToString();

                if (MusicalNotationText != null)
                {
                    MusicalNotationText.Text = GetMusicalNotation(_currentPitchSemitones);
                }

                _audioProcessor?.SetPitchSemiTones((float)_currentPitchSemitones);
                UpdateStatusText();
            }
        }

        private string GetMusicalNotation(int semitones)
        {
            // E is the default (0 semitones)
            // Notes in chromatic scale starting from E
            string[] notes = { "E", "F", "F♯/G♭", "G", "G♯/A♭", "A", "A♯/B♭", "B", "C", "C♯/D♭", "D", "D♯/E♭" };

            // Calculate the note index (handle negative values)
            int noteIndex = semitones % 12;
            if (noteIndex < 0)
                noteIndex += 12;

            return notes[noteIndex];
        }

        private void UpdateStatusText()
        {
            if (_audioProcessor != null && StatusText != null)
            {
                string notation = GetMusicalNotation(_currentPitchSemitones);
                string pitchText = _currentPitchSemitones >= 0 ? $"+{_currentPitchSemitones}" : _currentPitchSemitones.ToString();
                StatusText.Text = $"Processing audio (Pitch: {pitchText} semitones / {notation})";
            }
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                _audioProcessor = new AudioProcessor();
                _audioProcessor.AudioLevelUpdated += AudioProcessor_AudioLevelUpdated;
                _audioProcessor.Initialize(_selectedInputDevice, _selectedOutputDevice, _lowLatencyPreset);
                _audioProcessor.SetPitchSemiTones((float)PitchSlider.Value);
                _audioProcessor.Start();

                InputDeviceComboBox.IsEnabled = false;
                OutputDeviceComboBox.IsEnabled = false;

                UpdateStatusText();

                _uiUpdateTimer?.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting audio processing: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ToggleButton.IsChecked = false;
                StopProcessing();
            }
        }

        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            StopProcessing();
        }

        private void AudioProcessor_AudioLevelUpdated(object? sender, float level)
        {
            Dispatcher.Invoke(() =>
            {
                AudioLevelBar.Value = level;
            });
        }

        private void StopProcessing()
        {
            _uiUpdateTimer?.Stop();

            if (_audioProcessor != null)
            {
                _audioProcessor.AudioLevelUpdated -= AudioProcessor_AudioLevelUpdated;
                _audioProcessor.Stop();
                _audioProcessor.Dispose();
                _audioProcessor = null;
            }

            InputDeviceComboBox.IsEnabled = true;
            OutputDeviceComboBox.IsEnabled = true;
            AudioLevelBar.Value = 0;

            StatusText.Text = "Ready";
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            StopProcessing();
        }
    }
}
