using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ToolVip.ViewModels.Pages;

namespace ToolVip.Views.Pages
{
    /// <summary>
    /// Interaction logic for SavedDataPage.xaml
    /// </summary>
    public partial class SavedDataPage : Page
    {
        public SavedDataViewModel ViewModel { get; set; }
        public SavedDataPage(SavedDataViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }
        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            // Gọi hàm Load dữ liệu bên ViewModel
            await ViewModel.OnNavigatedToAsync();
        }
    }
}
