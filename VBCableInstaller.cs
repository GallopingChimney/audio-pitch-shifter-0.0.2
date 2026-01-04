using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace AudioPitchShifter
{
    public static class VBCableInstaller
    {
        private const string DriverName = "VB-Audio Virtual Cable";
        private const string DriverRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string DriverRegistryPath64 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

        public static bool IsVBCableInstalled()
        {
            try
            {
                // Check both 32-bit and 64-bit registry locations
                return CheckRegistryPath(Registry.LocalMachine, DriverRegistryPath) ||
                       CheckRegistryPath(Registry.LocalMachine, DriverRegistryPath64);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking VB-Cable installation: {ex.Message}");
                return false;
            }
        }

        private static bool CheckRegistryPath(RegistryKey baseKey, string subKeyPath)
        {
            try
            {
                using var key = baseKey.OpenSubKey(subKeyPath);
                if (key == null) return false;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var displayName = subKey.GetValue("DisplayName") as string;
                    var publisher = subKey.GetValue("Publisher") as string;

                    if (displayName != null &&
                        (displayName.Contains("VB-CABLE", StringComparison.OrdinalIgnoreCase) ||
                         displayName.Contains("VB-Audio Cable", StringComparison.OrdinalIgnoreCase)) ||
                        (publisher != null && publisher.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading registry path {subKeyPath}: {ex.Message}");
            }

            return false;
        }

        public static bool CheckAndInstall()
        {
            if (IsVBCableInstalled())
            {
                return true;
            }

            var result = MessageBox.Show(
                "VB-Audio Virtual Cable driver is required for this application to work.\n\n" +
                "Would you like to install it now?\n\n" +
                "Note: Administrator privileges are required for installation.",
                "VB-Audio Cable Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                MessageBox.Show(
                    "VB-Audio Cable is required. The application will now exit.",
                    "Installation Cancelled",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return InstallVBCable();
        }

        private static bool InstallVBCable()
        {
            try
            {
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string driverFolder = Path.Combine(appDirectory, "VBCABLE_Driver_Pack45");

                if (!Directory.Exists(driverFolder))
                {
                    MessageBox.Show(
                        $"VB-Audio Cable installer not found at:\n{driverFolder}\n\n" +
                        "Please ensure the VBCABLE_Driver_Pack45 folder is in the application directory.",
                        "Installer Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                // Determine which setup to run based on system architecture
                string setupFileName = Environment.Is64BitOperatingSystem ? "VBCABLE_Setup_x64.exe" : "VBCABLE_Setup.exe";
                string setupPath = Path.Combine(driverFolder, setupFileName);

                if (!File.Exists(setupPath))
                {
                    MessageBox.Show(
                        $"VB-Audio Cable setup file not found:\n{setupPath}",
                        "Setup File Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                // Run the installer with admin privileges
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = setupPath,
                    UseShellExecute = true,
                    Verb = "runas" // Request admin privileges
                };

                var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    MessageBox.Show(
                        "Failed to start the VB-Audio Cable installer.",
                        "Installation Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                // Wait for installation to complete
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    MessageBox.Show(
                        "VB-Audio Cable has been installed successfully.\n\n" +
                        "The application will now start.",
                        "Installation Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return true;
                }
                else
                {
                    MessageBox.Show(
                        $"VB-Audio Cable installation failed with exit code: {process.ExitCode}\n\n" +
                        "The application will now exit.",
                        "Installation Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // User cancelled the UAC prompt
                MessageBox.Show(
                    "Administrator privileges are required to install VB-Audio Cable.\n\n" +
                    "The application will now exit.",
                    "Installation Cancelled",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An error occurred during installation:\n{ex.Message}\n\n" +
                    "The application will now exit.",
                    "Installation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }
    }
}
