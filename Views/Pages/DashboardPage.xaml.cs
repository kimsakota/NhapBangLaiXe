using System.Windows.Controls;
using ToolVip.Models;
using ToolVip.ViewModels.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ToolVip.Views.Pages
{
    public partial class DashboardPage : INavigableView<DashboardViewModel>
    {
        public DashboardViewModel ViewModel { get; }
        private readonly IContentDialogService _contentDialogService;

        public DashboardPage(DashboardViewModel viewModel,
            IContentDialogService contentDialogService)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            _contentDialogService = contentDialogService;

            InitializeComponent();
        }
    }
}
