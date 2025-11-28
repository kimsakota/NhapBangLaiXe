using System.Windows.Controls;
using System.Windows.Input;
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
        public DashboardPage(DashboardViewModel viewModel,
            IContentDialogService contentDialogService)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }

        
    }
}
