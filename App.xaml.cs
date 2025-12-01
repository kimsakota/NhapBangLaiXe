using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Reflection;
using System.Windows.Threading;
using ToolVip.Helpers;
using ToolVip.Services;
using ToolVip.ViewModels.Pages;
using ToolVip.ViewModels.Windows;
using ToolVip.Views.Pages;
using ToolVip.Views.UseControls;
using ToolVip.Views.Windows;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.DependencyInjection;

namespace ToolVip
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory)); })
            .ConfigureServices((context, services) =>
            {
                services.AddNavigationViewPageProvider();

                services.AddHostedService<ApplicationHostService>();

                // Dịch vụ quản lý Dialog
                services.AddSingleton<IContentDialogService, ContentDialogService>();
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<ITaskBarService, TaskBarService>();

                services.AddSingleton<IRecordService, RecordService>();
                services.AddSingleton<IDataService, DataService>();
                services.AddSingleton<IOcrService, OcrService>();

                // [MỚI] Đăng ký ApiService
                services.AddSingleton<IApiService, ApiService>();

                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<INavigationWindow, MainWindow>();
                services.AddSingleton<MainWindowViewModel>();

                services.AddSingleton<DashboardPage>();
                services.AddSingleton<DashboardViewModel>();

                services.AddSingleton<SettingsPage>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<ChiTietDialog>();

                services.AddSingleton<ImportPage>();
                services.AddSingleton<ImportViewModel>();

                services.AddSingleton<SavedDataPage>();
                services.AddSingleton<SavedDataViewModel>();

                services.AddSingleton<Views.Pages.AutoPage>();
                services.AddSingleton<ViewModels.Pages.AutoViewModel>();
                services.AddSingleton<ConfigDialog>();
                services.AddSingleton<SavedDetailDialog>();
            }).Build();

        public static IServiceProvider Services
        {
            get { return _host.Services; }
        }

        private async void OnStartup(object sender, StartupEventArgs e)
        {
            ApplicationThemeManager.Apply(ApplicationTheme.Light);
            await _host.StartAsync();
        }

        private async void OnExit(object sender, ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
        }
    }
}