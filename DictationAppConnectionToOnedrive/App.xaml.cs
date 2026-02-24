using System.Windows;

namespace DictationApp
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Ensure data directories exist
            Services.DataService.Initialize();
        }
    }
}
