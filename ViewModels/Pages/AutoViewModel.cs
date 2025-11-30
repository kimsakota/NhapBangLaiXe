using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ToolVip.Models;
using ToolVip.Views.UseControls;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ToolVip.ViewModels.Pages
{
    public partial class AutoViewModel : ObservableObject
    {
        private readonly IContentDialogService _contentDialogService;
        // --- Dữ liệu cho Vùng Quét (Zones) ---
        [ObservableProperty]
        private ObservableCollection<ScanZone> _scanZones = new();

        [ObservableProperty]
        private ScanZone? _selectedZone;

        [ObservableProperty]
        private string _zoneKeyword = "";

        [ObservableProperty]
        private string _zoneX1 = "0";
        [ObservableProperty]
        private string _zoneY1 = "0";
        [ObservableProperty]
        private string _zoneX2 = "100";
        [ObservableProperty]
        private string _zoneY2 = "50";

        // --- Dữ liệu cho Kịch bản (Actions) ---
        [ObservableProperty]
        private ObservableCollection<AutomationStep> _actions = new();

        [ObservableProperty]
        private AutomationStep? _selectedAction;

        [ObservableProperty]
        private int _actionTypeIndex = 0; // 0: Mouse, 1: Key

        [ObservableProperty]
        private string _actionX = "0";
        [ObservableProperty]
        private string _actionY = "0";
        [ObservableProperty]
        private string _actionKey = "S";
        [ObservableProperty]
        private string _actionDelay = "1000";

        // --- Trạng thái ---
        [ObservableProperty]
        private bool _isRunning = false;

        [ObservableProperty]
        private string _logText = "Sẵn sàng...";

        [ObservableProperty]
        private bool _isLoop = true;

        public AutoViewModel(IContentDialogService contentDialogService)
        {
            _contentDialogService = contentDialogService;
            // Dữ liệu mẫu để test giao diện
            ScanZones.Add(new ScanZone { Keyword = "Hoàn thành", X1 = 100, Y1 = 200, X2 = 300, Y2 = 250, IsExactMatch = true });
            ScanZones.Add(new ScanZone { Keyword = "Xác nhận", X1 = 500, Y1 = 200, X2 = 700, Y2 = 250 });
        }

        partial void OnSelectedZoneChanged(ScanZone? value)
        {
            if (SelectedZone == null) return;
            var dialogControl = new ConfigDialog(this);
            
            var dialog = new ContentDialog
            {
                Title = "Cấu hình",
                Content = dialogControl,
                //PrimaryButtonText = "Lưu & Chuyển",
                CloseButtonText = "Đóng",
                DefaultButton = ContentDialogButton.Close,
            };

            _contentDialogService.ShowAsync(dialog, CancellationToken.None);


        }

        // Các Command (Logic sẽ làm sau)
        [RelayCommand] private void AddZone() { }
        [RelayCommand] private void UpdateZone() { }
        [RelayCommand] private void DeleteZone() { }

        [RelayCommand] private void AddAction() { }
        [RelayCommand] private void UpdateAction() { }
        [RelayCommand] private void DeleteAction() { }

        [RelayCommand] private void ToggleRun() { IsRunning = !IsRunning; }
        [RelayCommand] private void ClearLog() { LogText = ""; }
    }
}