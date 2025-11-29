using System.Collections.ObjectModel;
using ToolVip.Models;
using ToolVip.Services;
using ToolVip.Views.UseControls;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace ToolVip.ViewModels.Pages
{
    public partial class SavedDataViewModel : ObservableObject, INavigationAware
    {
        private readonly IContentDialogService _contentDialogService;
        private readonly IDataService _dataService;

        [ObservableProperty]
        private ObservableCollection<DriverProfile> _savedProfiles = new();

        [ObservableProperty]
        private DriverProfile? _selectedProfile;

        [ObservableProperty]
        private int? _count = 0;

        public SavedDataViewModel(IDataService dataService,
            IContentDialogService contentDialogService)
        {
            _dataService = dataService;
            _contentDialogService = contentDialogService;

            // [SỬA LỖI] Load dữ liệu ngay khi khởi tạo ViewModel
            LoadData();
        }

        public Task OnNavigatedToAsync()
        {
            // Load lại lần nữa khi người dùng chuyển tab để cập nhật mới nhất
            LoadData();
            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        partial void OnSelectedProfileChanged(DriverProfile? value)
        {
            if (SelectedProfile == null) return;

            var dialogControl = new ChiTietDialog { DataContext = SelectedProfile };
            var dialog = new ContentDialog
            {
                Title = "Chi tiết hồ sơ",
                Content = dialogControl,
                PrimaryButtonText = "Lưu & Chuyển",
                CloseButtonText = "Đóng",
                DefaultButton = ContentDialogButton.Close,
            };

            _contentDialogService.ShowAsync(dialog, CancellationToken.None);

            dialog.Closing += (s, e) =>
            {
                if (e.Result == ContentDialogResult.Primary)
                {
                    _dataService.MoveToSaved(SelectedProfile);
                    SavedProfiles.Remove(SelectedProfile);
                    Count--;
                }
                else
                {
                    SelectedProfile = null;
                }
            };
        }

        private void LoadData()
        {
            var data = _dataService.LoadSavedData();

            // Cập nhật lại list hiển thị
            SavedProfiles = new ObservableCollection<DriverProfile>(data);
            Count = SavedProfiles.Count;
        }
    }
}