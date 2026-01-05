using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using NAudio.Wave;

namespace AudioPitchShifter
{
    public class PercentageConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is double percentage && values[1] is double totalHeight)
            {
                return percentage * totalHeight;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SpectrumColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3) return System.Windows.Media.Colors.White;

            string? colorStyle = values[0] as string;
            int totalBars = values[1] is int total ? total : 36;
            int barIndex = values[2] is int index ? index : 0;

            float position = totalBars > 1 ? (float)barIndex / (totalBars - 1) : 0;

            return colorStyle switch
            {
                "Rainbow" => GetRainbowColor(position),
                "Monochrome" => GetMonochromeColor(position),
                "Fire" => GetFireColor(position),
                "Ocean" => GetOceanColor(position),
                _ => GetRainbowColor(position)
            };
        }

        private System.Windows.Media.Color GetRainbowColor(float position)
        {
            // HSV to RGB for rainbow effect
            float hue = position * 300; // 0 to 300 degrees (red to blue)
            return HsvToRgb(hue, 1.0f, 1.0f);
        }

        private System.Windows.Media.Color GetMonochromeColor(float position)
        {
            // Purple gradient
            byte intensity = (byte)(100 + position * 155);
            return System.Windows.Media.Color.FromRgb((byte)(intensity * 0.545f), (byte)(intensity * 0.36f), (byte)(intensity * 0.96f));
        }

        private System.Windows.Media.Color GetFireColor(float position)
        {
            // Black -> Red -> Orange -> Yellow -> White
            if (position < 0.25f)
            {
                float t = position / 0.25f;
                return System.Windows.Media.Color.FromRgb((byte)(t * 255), 0, 0);
            }
            else if (position < 0.5f)
            {
                float t = (position - 0.25f) / 0.25f;
                return System.Windows.Media.Color.FromRgb(255, (byte)(t * 140), 0);
            }
            else if (position < 0.75f)
            {
                float t = (position - 0.5f) / 0.25f;
                return System.Windows.Media.Color.FromRgb(255, (byte)(140 + t * 75), (byte)(t * 0));
            }
            else
            {
                float t = (position - 0.75f) / 0.25f;
                return System.Windows.Media.Color.FromRgb(255, (byte)(215 + t * 40), (byte)(t * 255));
            }
        }

        private System.Windows.Media.Color GetOceanColor(float position)
        {
            // Dark blue -> Cyan -> Light blue
            byte r = (byte)(position * 100);
            byte g = (byte)(100 + position * 155);
            byte b = (byte)(200 + position * 55);
            return System.Windows.Media.Color.FromRgb(r, g, b);
        }

        private System.Windows.Media.Color HsvToRgb(float h, float s, float v)
        {
            int hi = (int)(h / 60) % 6;
            float f = h / 60 - (int)(h / 60);

            byte vByte = (byte)(v * 255);
            byte p = (byte)(v * (1 - s) * 255);
            byte q = (byte)(v * (1 - f * s) * 255);
            byte t = (byte)(v * (1 - (1 - f) * s) * 255);

            return hi switch
            {
                0 => System.Windows.Media.Color.FromRgb(vByte, t, p),
                1 => System.Windows.Media.Color.FromRgb(q, vByte, p),
                2 => System.Windows.Media.Color.FromRgb(p, vByte, t),
                3 => System.Windows.Media.Color.FromRgb(p, q, vByte),
                4 => System.Windows.Media.Color.FromRgb(t, p, vByte),
                _ => System.Windows.Media.Color.FromRgb(vByte, p, q)
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public partial class MainWindow : Window
    {
        private AudioProcessor? _audioProcessor;
        private int _selectedInputDevice = 0;
        private int _selectedOutputDevice = 0;
        private readonly LatencyPreset _lowLatencyPreset = new LatencyPreset("Low", 20, 100, "Optimized for quality");
        private System.Windows.Threading.DispatcherTimer? _uiUpdateTimer;
        private int _currentPitchSemitones = 0;
        private ObservableCollection<double> _spectrumData = new ObservableCollection<double>();
        private string _spectrumColorStyle = "Rainbow";

        public string SpectrumColorStyle
        {
            get => _spectrumColorStyle;
            set
            {
                _spectrumColorStyle = value;
                // Force refresh of spectrum analyzer (only if it's initialized)
                if (SpectrumAnalyzer != null)
                {
                    var temp = SpectrumAnalyzer.ItemsSource;
                    SpectrumAnalyzer.ItemsSource = null;
                    SpectrumAnalyzer.ItemsSource = temp;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            LoadAudioDevices();

            // Set DataContext for bindings
            DataContext = this;

            // Initialize spectrum analyzer with 36 bars
            for (int i = 0; i < 36; i++)
            {
                _spectrumData.Add(0.0);
            }
            SpectrumAnalyzer.ItemsSource = _spectrumData;
            SpectrumAnalyzer.AlternationCount = 36;

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
                _audioProcessor.SpectrumDataUpdated += AudioProcessor_SpectrumDataUpdated;
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

        private void AudioProcessor_SpectrumDataUpdated(object? sender, float[] spectrum)
        {
            Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < spectrum.Length && i < _spectrumData.Count; i++)
                {
                    _spectrumData[i] = spectrum[i];
                }
            });
        }

        private void StopProcessing()
        {
            _uiUpdateTimer?.Stop();

            if (_audioProcessor != null)
            {
                _audioProcessor.AudioLevelUpdated -= AudioProcessor_AudioLevelUpdated;
                _audioProcessor.SpectrumDataUpdated -= AudioProcessor_SpectrumDataUpdated;
                _audioProcessor.Stop();
                _audioProcessor.Dispose();
                _audioProcessor = null;
            }

            InputDeviceComboBox.IsEnabled = true;
            OutputDeviceComboBox.IsEnabled = true;
            AudioLevelBar.Value = 0;

            // Reset spectrum analyzer
            for (int i = 0; i < _spectrumData.Count; i++)
            {
                _spectrumData[i] = 0.0;
            }

            StatusText.Text = "Ready";
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            StopProcessing();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeButton_Click(sender, e);
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                MaximizeButton.Content = "□";
            }
            else
            {
                WindowState = WindowState.Maximized;
                MaximizeButton.Content = "□";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SpectrumColorStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpectrumColorStyleComboBox?.SelectedItem is ComboBoxItem item)
            {
                SpectrumColorStyle = item.Content.ToString() ?? "Rainbow";
            }
        }
    }
}
