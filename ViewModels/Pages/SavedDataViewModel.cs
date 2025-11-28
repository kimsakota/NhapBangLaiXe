using System.Collections.ObjectModel;
using ToolVip.Models;
using ToolVip.Services;
using Wpf.Ui.Abstractions.Controls;

namespace ToolVip.ViewModels.Pages
{
    public partial class SavedDataViewModel : ObservableObject, INavigationAware
    {
        private readonly IDataService _dataService;

        [ObservableProperty]
        private ObservableCollection<DriverProfile> _savedProfiles = new();

        [ObservableProperty]
        private int? _count = 0;

        public SavedDataViewModel(IDataService dataService)
        {
            _dataService = dataService;

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


        private void LoadData()
        {
            var data = _dataService.LoadSavedData();

            // Cập nhật lại list hiển thị
            SavedProfiles = new ObservableCollection<DriverProfile>(data);
            Count = SavedProfiles.Count;
        }
    }
}