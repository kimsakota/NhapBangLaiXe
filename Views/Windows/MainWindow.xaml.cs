using System.Runtime.InteropServices;
using System.Windows.Interop;
using ToolVip.ViewModels.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace ToolVip.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }

        // Property static để các ViewModel khác truy cập
        public static MainWindow? Instance { get; private set; }

        public MainWindow(
            MainWindowViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService,
            IContentDialogService contentDialogService
        )
        {
            ViewModel = viewModel;
            DataContext = this;
            Instance = this;

            InitializeComponent();
            SetPageService(navigationViewPageProvider);
            navigationService.SetNavigationControl(RootNavigation);
            contentDialogService.SetDialogHost(RootContentDialog);
        }

        #region INavigationWindow methods
        public INavigationView GetNavigation() => RootNavigation;
        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);
        public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) => RootNavigation.SetPageProviderService(navigationViewPageProvider);
        public void ShowWindow() => Show();
        public void CloseWindow() => Close();
        #endregion

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Instance = null;
            System.Windows.Application.Current.Shutdown();
        }

        public void SetServiceProvider(IServiceProvider serviceProvider) => throw new NotImplementedException();
    }
}