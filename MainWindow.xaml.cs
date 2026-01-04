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
        private System.Windows.Threading.DispatcherTimer? _uiUpdateTimer;

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
                int value = (int)e.NewValue;
                PitchValueText.Text = value >= 0 ? $"+{value}" : value.ToString();

                _audioProcessor?.SetPitchSemiTones((float)value);
            }
        }

        private void LatencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LatencyValueText != null)
            {
                int value = (int)e.NewValue;
                LatencyValueText.Text = $"{value} ms";
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int latencyMs = (int)LatencySlider.Value;

                _audioProcessor = new AudioProcessor();
                _audioProcessor.AudioLevelUpdated += AudioProcessor_AudioLevelUpdated;
                _audioProcessor.Initialize(_selectedInputDevice, _selectedOutputDevice, latencyMs);
                _audioProcessor.SetPitchSemiTones((float)PitchSlider.Value);
                _audioProcessor.Start();

                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                InputDeviceComboBox.IsEnabled = false;
                OutputDeviceComboBox.IsEnabled = false;
                LatencySlider.IsEnabled = false;

                StatusText.Text = $"Processing audio (Pitch: {PitchSlider.Value:+0;-0} semitones, Latency: {latencyMs}ms)";

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
            LatencySlider.IsEnabled = true;
            AudioLevelBar.Value = 0;

            StatusText.Text = "Ready";
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            StopProcessing();
        }
    }
}
