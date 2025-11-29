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
    /// Interaction logic for AutoConfigPage.xaml
    /// </summary>
    public partial class AutoConfigPage : Page
    {
        public AutoConfigViewModel ViewModel { get; set; }
        public AutoConfigPage(AutoConfigViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }
    }
}
