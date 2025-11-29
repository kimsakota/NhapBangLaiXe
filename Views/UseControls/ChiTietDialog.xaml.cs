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
using ToolVip.Models;
using ToolVip.ViewModels.Pages;

namespace ToolVip.Views.UseControls
{
    /// <summary>
    /// Interaction logic for ChiTietDialog.xaml
    /// </summary>
    public partial class ChiTietDialog : System.Windows.Controls.UserControl
    {
        public ChiTietDialog()
        {
            
            InitializeComponent();
        }
        private void OnButtonDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Ngăn sự kiện click đơn lan ra (nếu cần)
            e.Handled = true;

            if (sender is System.Windows.Controls.Button btn && btn.DataContext is DriverProfile profile)
            {
                // 1. Lấy nội dung cần dán (Ví dụ lấy Biển số xe)
                // Bạn có thể đổi thành profile.FullName hoặc chuỗi bất kỳ
                //string textToPaste = profile.LicensePlate ?? "";
                string textToPaste = "";
                switch (btn.Uid)
                {
                    case "1": 
                        textToPaste = profile.FullName;
                        break;
                    case "2":
                        textToPaste = profile.Cccd;
                        break;
                    case "3":
                        textToPaste = profile.IssueDate;
                        break;
                    case "4":
                        textToPaste = profile.PhoneNumber;
                        break;  
                    case "5":
                        textToPaste = profile.Address;
                        break;
                    case "6":
                        textToPaste = profile.WardCommune;
                        break;
                    case "7":
                        textToPaste = profile.WardCommune;
                        break;
                    case "8":
                        textToPaste = profile.LicensePlate;
                        break;
                    case "9":
                        textToPaste = profile.EngineNumber;
                        break;
                     case "10":
                        textToPaste = profile.ChassisNumber;
                        break;
                    default:
                        break;


                }

                if (!string.IsNullOrEmpty(textToPaste))
                {
                    // 2. Copy vào Clipboard
                    System.Windows.Clipboard.SetText(textToPaste);

                    // 3. Giả lập bấm Ctrl + V để dán
                    // "^{v}" nghĩa là giữ Ctrl (^) và nhấn v
                    SendKeys.SendWait("^{v}");
                }
            }
        }

    }
}
