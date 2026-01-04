using System.Windows;

namespace AudioPitchShifter
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check if VB-Audio Cable is installed before starting the application
            if (!VBCableInstaller.CheckAndInstall())
            {
                // User declined installation or installation failed
                Shutdown();
                return;
            }

            // Continue with normal startup - MainWindow will be shown automatically
        }
    }
}
