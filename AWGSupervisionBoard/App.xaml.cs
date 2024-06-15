using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Pavlo.AWGSupervisionBoard
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var window = new MainWindow();
            var viewModel = new Viewmodel.Viewmodel();

            window.DataContext = viewModel;
            window.Closing += viewModel.Window_Closing;

            window.Show();

            base.OnStartup(e);
        }
    }
}
