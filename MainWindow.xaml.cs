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
        private LatencyPreset[] _latencyPresets = Array.Empty<LatencyPreset>();
        private LatencyPreset _selectedLatencyPreset = null!;
        private System.Windows.Threading.DispatcherTimer? _uiUpdateTimer;

        public MainWindow()
        {
            InitializeComponent();
            LoadAudioDevices();
            LoadLatencyPresets();

            _uiUpdateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _uiUpdateTimer.Tick += UiUpdateTimer_Tick;
        }

        private void UiUpdateTimer_Tick(object? sender, EventArgs e)
        {
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

        private void LoadLatencyPresets()
        {
            _latencyPresets = LatencyPreset.GetPresets();
            LatencyPresetComboBox.Items.Clear();

            foreach (var preset in _latencyPresets)
            {
                LatencyPresetComboBox.Items.Add(preset);
            }

            // Select "Low" by default (index 0)
            LatencyPresetComboBox.SelectedIndex = 0;
            _selectedLatencyPreset = _latencyPresets[0];
        }

        private void InputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedInputDevice = InputDeviceComboBox.SelectedIndex;
        }

        private void OutputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedOutputDevice = OutputDeviceComboBox.SelectedIndex - 1;
        }

        private void LatencyPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LatencyPresetComboBox.SelectedIndex >= 0)
            {
                _selectedLatencyPreset = _latencyPresets[LatencyPresetComboBox.SelectedIndex];
            }
        }

        private void PitchSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PitchValueText != null)
            {
                int value = (int)e.NewValue;
                PitchValueText.Text = value >= 0 ? $"+{value}" : value.ToString();

                _audioProcessor?.SetPitchSemiTones((float)value);
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _audioProcessor = new AudioProcessor();
                _audioProcessor.AudioLevelUpdated += AudioProcessor_AudioLevelUpdated;
                _audioProcessor.Initialize(_selectedInputDevice, _selectedOutputDevice, _selectedLatencyPreset);
                _audioProcessor.SetPitchSemiTones((float)PitchSlider.Value);
                _audioProcessor.Start();

                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                InputDeviceComboBox.IsEnabled = false;
                OutputDeviceComboBox.IsEnabled = false;
                LatencyPresetComboBox.IsEnabled = false;

                StatusText.Text = $"Processing audio (Pitch: {PitchSlider.Value:+0;-0} semitones, {_selectedLatencyPreset.Name})";

                _uiUpdateTimer?.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting audio processing: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StopProcessing();
            }
        }

        private void AudioProcessor_AudioLevelUpdated(object? sender, float level)
        {
            Dispatcher.Invoke(() =>
            {
                AudioLevelBar.Value = level;
            });
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopProcessing();
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

            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            InputDeviceComboBox.IsEnabled = true;
            OutputDeviceComboBox.IsEnabled = true;
            LatencyPresetComboBox.IsEnabled = true;
            AudioLevelBar.Value = 0;

            StatusText.Text = "Ready";
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            StopProcessing();
        }
    }
}
