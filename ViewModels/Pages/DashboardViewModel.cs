using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ToolVip.Models;
using ToolVip.Views.UseControls;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ToolVip.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IContentDialogService _contentDialogService;
        [ObservableProperty]
        private ObservableCollection<DriverProfile> _profiles;

        [ObservableProperty]
        private DriverProfile? _selectedProfile;

        public DashboardViewModel(IContentDialogService contentDialogService)
        {
            _contentDialogService = contentDialogService;
            // Tạo dữ liệu giả lập (Fake data) để test
            Profiles = new ObservableCollection<DriverProfile>
            {
                new DriverProfile
                {
                    FullName = "ĐOÀN VĂN HƯƠNG",
                    Cccd = "001068010416",
                    IssueDate = "26/08/2021",
                    PhoneNumber = "0349917927",
                    WardCommune = "Thành phố Hà Nội - Xã Thuận An",
                    Address = "Thôn ĐỀ TRỤ 8",
                    LicensePlate = "29AB40206",
                    EngineNumber = "VZS139FMB58003270",
                    ChassisNumber = "RPOCCBPLSMD003270"
                },
                new DriverProfile
                {
                    FullName = "NGUYỄN VĂN A",
                    Cccd = "012345678910",
                    IssueDate = "01/01/2022",
                    PhoneNumber = "0987654321",
                    WardCommune = "TP HCM - Quận 1",
                    Address = "Số 1 Đường Lê Lợi",
                    LicensePlate = "59T1-12345",
                    EngineNumber = "HONDA123456",
                    ChassisNumber = "YAMAHA123456"
                },
                new DriverProfile
                {
                    FullName = "NGUYỄN VĂN A",
                    Cccd = "012345678910",
                    IssueDate = "01/01/2022",
                    PhoneNumber = "0987654321",
                    WardCommune = "TP HCM - Quận 1",
                    Address = "Số 1 Đường Lê Lợi",
                    LicensePlate = "59T1-12345",
                    EngineNumber = "HONDA123456",
                    ChassisNumber = "YAMAHA123456"
                },
                new DriverProfile
                {
                    FullName = "NGUYỄN VĂN A",
                    Cccd = "012345678910",
                    IssueDate = "01/01/2022",
                    PhoneNumber = "0987654321",
                    WardCommune = "TP HCM - Quận 1",
                    Address = "Số 1 Đường Lê Lợi",
                    LicensePlate = "59T1-12345",
                    EngineNumber = "HONDA123456",
                    ChassisNumber = "YAMAHA123456"
                },
                new DriverProfile
                {
                    FullName = "NGUYỄN VĂN A",
                    Cccd = "012345678910",
                    IssueDate = "01/01/2022",
                    PhoneNumber = "0987654321",
                    WardCommune = "TP HCM - Quận 1",
                    Address = "Số 1 Đường Lê Lợi",
                    LicensePlate = "59T1-12345",
                    EngineNumber = "HONDA123456",
                    ChassisNumber = "YAMAHA123456"
                },
                new DriverProfile
                {
                    FullName = "NGUYỄN VĂN A",
                    Cccd = "012345678910",
                    IssueDate = "01/01/2022",
                    PhoneNumber = "0987654321",
                    WardCommune = "TP HCM - Quận 1",
                    Address = "Số 1 Đường Lê Lợi",
                    LicensePlate = "59T1-12345",
                    EngineNumber = "HONDA123456",
                    ChassisNumber = "YAMAHA123456"
                }
            };
        }

        partial void OnSelectedProfileChanged(DriverProfile? value)
        {
            if (SelectedProfile == null) return;
            var dialogControl = new ChiTietDialog { DataContext = SelectedProfile };
            var dialog = new ContentDialog
            {
                Title = "Chi tiết hồ sơ",
                Content = dialogControl,
                PrimaryButtonText = "Lưu",
                CloseButtonText = "Đóng",
                DefaultButton = ContentDialogButton.Close,
                
            };
            _contentDialogService.ShowAsync(dialog, CancellationToken.None);

            dialog.Closing += (s, e) =>
            {
                if (e.Result == ContentDialogResult.Primary)
                    Profiles.Remove(SelectedProfile);
                else
                    SelectedProfile = null;
            };
        }

       


    }
}
