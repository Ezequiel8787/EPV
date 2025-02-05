using System;
using System.Threading;
using System.Windows;

namespace EPVDesktopPro
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex _mutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "EPVDesktopPro"; // Change this to the name of your application
            bool createdNew;

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                // If the mutex already exists, it means that another instance of the application is running
                // You can display a message on the console or take other actions to inform the user.
                MessageBox.Show("The application is already running.");
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            base.OnExit(e);
        }
    }
}
