using System.Windows;
using BigWalkVRInstaller.Services;

namespace BigWalkVRInstaller
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            SelfUpdater.CleanupOldExe();
            base.OnStartup(e);
        }
    }
}
