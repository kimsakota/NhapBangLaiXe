using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices; // [Thêm] Để dùng DllImport
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Windows;
using ToolVip.Helpers;
using ToolVip.Models;
using ToolVip.Services;
using ToolVip.Views.UseControls;
using Wpf.Ui;
using Wpf.Ui.Controls;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace ToolVip.ViewModels.Pages
{
    public partial class AutoViewModel : ObservableObject
    {
        // [MỚI] Import API để bắt phím Ctrl + S
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        private const int VK_CONTROL = 0x11;
        private const int VK_S = 0x53;

        private readonly IContentDialogService _contentDialogService;
        private readonly IRecordService _recordService;
        private readonly IOcrService _ocrService;
        private readonly string _configPath;

        [ObservableProperty]
        private ObservableCollection<ScanZone> _scanZones = new();

        [ObservableProperty]
        private ScanZone? _selectedZone;

        [ObservableProperty] private int _editX1;
        [ObservableProperty] private int _editY1;
        [ObservableProperty] private int _editX2;
        [ObservableProperty] private int _editY2;

        [ObservableProperty] private bool _isRecordingFound;
        [ObservableProperty] private bool _isRecordingNotFound;

        [ObservableProperty]
        private string _logText = "Sẵn sàng...";

        partial void OnLogTextChanged(string value)
        {
            if (!string.IsNullOrEmpty(value)) AddLog(value, "Gray");
        }

        [ObservableProperty]
        private ObservableCollection<LogEntry> _logs = new();

        [ObservableProperty]
        private bool _isTesting = false;

        public AutoViewModel(
            IContentDialogService contentDialogService,
            IRecordService recordService,
            IOcrService ocrService)
        {
            _contentDialogService = contentDialogService;
            _recordService = recordService;
            _ocrService = ocrService;

            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "scan_zones.json");

            LoadZones();

            // [CẬP NHẬT] Đổi từ khóa mặc định theo yêu cầu
            if (ScanZones.Count == 0)
            {
                // Từ khóa 1
                ScanZones.Add(new ScanZone
                {
                    Keyword = "Trường dữ liệu bạn nhập bị lỗi. Vui lòng kiểm tra lại",
                    X1 = 100,
                    Y1 = 200,
                    X2 = 300,
                    Y2 = 250,
                    IsExactMatch = true
                });

                // Từ khóa 2 (Đã sửa từ "Xác nhận" -> "Không tìm thấy kết quả")
                ScanZones.Add(new ScanZone
                {
                    Keyword = "Không tìm thấy kết quả",
                    X1 = 500,
                    Y1 = 200,
                    X2 = 700,
                    Y2 = 250
                });

                ScanZones.Add(new ScanZone
                {
                    Keyword = "Hoàn thành tài liệu thành công",
                    X1 = 500,
                    Y1 = 200,
                    X2 = 700,
                    Y2 = 250
                });

                SaveZones();
            }

            ScanZones.CollectionChanged += ScanZones_CollectionChanged;
        }

        private void ScanZones_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SaveZones();
        }

        private void LoadZones()
        {
            if (!File.Exists(_configPath)) return;
            try
            {
                var json = File.ReadAllText(_configPath);
                var data = JsonSerializer.Deserialize<ObservableCollection<ScanZone>>(json);
                if (data != null)
                {
                    ScanZones = data;
                }
            }
            catch (Exception ex)
            {
                AddLog($"Lỗi tải cấu hình: {ex.Message}", "Red");
            }
        }

        public void SaveZones()
        {
            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                };
                var json = JsonSerializer.Serialize(ScanZones, options);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                AddLog($"Lỗi lưu cấu hình: {ex.Message}", "Red");
            }
        }

        partial void OnSelectedZoneChanged(ScanZone? value)
        {
            if (SelectedZone == null) return;

            EditX1 = SelectedZone.X1;
            EditY1 = SelectedZone.Y1;
            EditX2 = SelectedZone.X2;
            EditY2 = SelectedZone.Y2;

            var dialogControl = new ConfigDialog(this);

            var dialog = new ContentDialog
            {
                Title = $"Cấu hình: {SelectedZone.Keyword}",
                Content = dialogControl,
                CloseButtonText = "Đóng",
                DefaultButton = ContentDialogButton.Close,
            };

            dialog.Closing += (s, e) =>
            {
                SelectedZone = null;
                SaveZones();
            };

            _contentDialogService.ShowAsync(dialog, System.Threading.CancellationToken.None);
        }

        // --- COMMANDS ---

        [RelayCommand]
        private async Task TestAllZonesAsync()
        {
            if (IsTesting) return;
            IsTesting = true;
            Logs.Clear();
            AddLog("--- BẮT ĐẦU KIỂM TRA ---", "Blue");

            var init = _ocrService.Init("vie");
            if (!init.Success)
            {
                AddLog($"Lỗi Init OCR: {init.Message}", "Red");
                IsTesting = false;
                return;
            }

            try
            {
                await Task.Run(async () =>
                {
                    if (ScanZones.Count == 0)
                    {
                        AddLog("Không có vùng quét nào!", "Orange");
                        return;
                    }

                    foreach (var zone in ScanZones)
                    {
                        AddLog($"Đang quét vùng: {zone.Keyword}...", "Black");

                        int x = Math.Min(zone.X1, zone.X2);
                        int y = Math.Min(zone.Y1, zone.Y2);
                        int w = Math.Abs(zone.X1 - zone.X2);
                        int h = Math.Abs(zone.Y1 - zone.Y2);

                        if (w <= 0 || h <= 0)
                        {
                            AddLog($"-> Lỗi: Kích thước vùng {zone.Keyword} không hợp lệ!", "Red");
                            continue;
                        }

                        string text = _ocrService.GetTextFromRegion(x, y, w, h);

                        bool isFound = !string.IsNullOrEmpty(text) &&
                                       text.Contains(zone.Keyword, StringComparison.OrdinalIgnoreCase);

                        if (isFound)
                        {
                            AddLog($"-> [THẤY] OCR đọc được: '{text}'", "Green");
                        }
                        else
                        {
                            AddLog($"-> [KHÔNG THẤY] OCR đọc được: '{text}' (Cần: {zone.Keyword})", "Red");
                        }

                        await Task.Delay(500);
                    }
                    AddLog("--- HOÀN THÀNH ---", "Blue");
                });
            }
            catch (Exception ex)
            {
                AddLog($"Lỗi ngoại lệ: {ex.Message}", "Red");
            }
            finally
            {
                IsTesting = false;
            }
        }

        public void AddLog(string msg, string color = "Black")
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Logs.Add(new LogEntry { Message = msg, Color = color });
                if (Logs.Count > 100) Logs.RemoveAt(0);
            });
        }

        [RelayCommand]
        private void ClearLog()
        {
            Logs.Clear();
            LogText = "";
        }

        // --- CÁC HÀM CŨ ---
        [RelayCommand]
        private void GetCoordinateA()
        {
            if (RecordHelper.GetCursorPos(out var p))
            {
                EditX1 = p.X; EditY1 = p.Y;
                if (SelectedZone != null)
                {
                    SelectedZone.X1 = p.X;
                    SelectedZone.Y1 = p.Y;
                }
            }
        }

        [RelayCommand]
        private void GetCoordinateS()
        {
            if (RecordHelper.GetCursorPos(out var p))
            {
                EditX2 = p.X; EditY2 = p.Y;
                if (SelectedZone != null)
                {
                    SelectedZone.X2 = p.X;
                    SelectedZone.Y2 = p.Y;
                }
            }
        }

        [RelayCommand]
        private void ToggleRecordFound()
        {
            if (SelectedZone == null) return;
            if (IsRecordingFound)
            {
                var events = _recordService.StopRecordingAndGet();
                SelectedZone.FoundActions = events;
                IsRecordingFound = false;

                SaveZones();

                System.Windows.MessageBox.Show($"Đã lưu Found ({events.Count} bước) vào cấu hình.");
                OnPropertyChanged(nameof(SelectedZone)); // Refresh UI
            }
            else
            {
                if (_recordService.IsRecording) return;
                _recordService.StartRecording();
                IsRecordingFound = true;

                // [MỚI] Bắt đầu kiểm tra Ctrl + S
                CheckStopKey(true);
            }
        }

        [RelayCommand]
        private void ToggleRecordNotFound()
        {
            if (SelectedZone == null) return;
            if (IsRecordingNotFound)
            {
                var events = _recordService.StopRecordingAndGet();
                SelectedZone.NotFoundActions = events;
                IsRecordingNotFound = false;

                SaveZones();

                System.Windows.MessageBox.Show($"Đã lưu NotFound ({events.Count} bước) vào cấu hình.");
                OnPropertyChanged(nameof(SelectedZone)); // Refresh UI
            }
            else
            {
                if (_recordService.IsRecording) return;
                _recordService.StartRecording();
                IsRecordingNotFound = true;

                // [MỚI] Bắt đầu kiểm tra Ctrl + S
                CheckStopKey(false);
            }
        }

        // [MỚI] Hàm chạy ngầm để kiểm tra phím Ctrl + S
        private void CheckStopKey(bool isFoundRecording)
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    // Kiểm tra xem còn đang record không (nếu người dùng bấm nút Dừng thì thoát loop)
                    bool recording = isFoundRecording ? IsRecordingFound : IsRecordingNotFound;
                    if (!recording) break;

                    bool isCtrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                    bool isS = (GetAsyncKeyState(VK_S) & 0x8000) != 0;

                    if (isCtrl && isS)
                    {
                        // Gọi lại ToggleRecord... trên UI thread để dừng
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (isFoundRecording) ToggleRecordFound();
                            else ToggleRecordNotFound();
                        });
                        break;
                    }
                    await Task.Delay(50);
                }
            });
        }

        // [MỚI] Lệnh xóa Record Found
        [RelayCommand]
        private void DeleteRecordFound()
        {
            if (SelectedZone == null) return;
            if (SelectedZone.FoundActions.Count == 0) return;

            var result = System.Windows.MessageBox.Show("Bạn có chắc chắn muốn xóa bản ghi 'Tìm Thấy' này?", "Xác nhận xóa", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                SelectedZone.FoundActions.Clear();
                SaveZones();
                OnPropertyChanged(nameof(SelectedZone)); // Cập nhật lại giao diện (số bước)
                System.Windows.MessageBox.Show("Đã xóa bản ghi thành công.");
            }
        }

        // [MỚI] Lệnh xóa Record Not Found
        [RelayCommand]
        private void DeleteRecordNotFound()
        {
            if (SelectedZone == null) return;
            if (SelectedZone.NotFoundActions.Count == 0) return;

            var result = System.Windows.MessageBox.Show("Bạn có chắc chắn muốn xóa bản ghi 'Không Tìm Thấy' này?", "Xác nhận xóa", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                SelectedZone.NotFoundActions.Clear();
                SaveZones();
                OnPropertyChanged(nameof(SelectedZone)); // Cập nhật lại giao diện (số bước)
                System.Windows.MessageBox.Show("Đã xóa bản ghi thành công.");
            }
        }

        [RelayCommand] private void AddZone() { ScanZones.Add(new ScanZone { Keyword = "New Zone" }); }

        [RelayCommand] private void DeleteZone() { if (SelectedZone != null) ScanZones.Remove(SelectedZone); }
    }
}