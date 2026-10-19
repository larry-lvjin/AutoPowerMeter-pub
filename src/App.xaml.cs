using System.Windows;

namespace AutoPowerMeter;

public partial class App : Application
{
    protected override void OnExit(ExitEventArgs e)
    {
        if (ServiceLocator.Current is not null)
            ServiceLocator.Current.IosRuntime.Dispose();
        base.OnExit(e);
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length == 2 && e.Args[0] == "--packaging-smoke")
        {
            Shutdown(AutoPowerMeter.Utils.PackagingSmokeCheck.Run(e.Args[1]));
            return;
        }
        ServiceLocator.Initialize();
        base.OnStartup(e);
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
