using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
using MessageBoxButton = System.Windows.MessageBoxButton; // Thêm namespace này
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace ToolVip.ViewModels.Pages
{
    // Class hỗ trợ hiển thị thời gian
    public class TimeoutOption
    {
        public string Display { get; set; } = "";
        public int Value { get; set; }
    }

    public partial class AutoViewModel : ObservableObject
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        private const int VK_CONTROL = 0x11;
        private const int VK_S = 0x53;

        private readonly IContentDialogService _contentDialogService;
        private readonly IRecordService _recordService;
        private readonly IOcrService _ocrService;
        private readonly string _configPath;

        // [MỚI] Danh sách tùy chọn thời gian chuẩn để Binding
        public List<TimeoutOption> TimeoutOptions { get; } = new List<TimeoutOption>
        {
            new TimeoutOption { Display = "Không giới hạn (Theo Record)", Value = 0 },
            new TimeoutOption { Display = "1 giây", Value = 1 },
            new TimeoutOption { Display = "3 giây", Value = 3 },
            new TimeoutOption { Display = "5 giây", Value = 5 },
            new TimeoutOption { Display = "10 giây", Value = 10 },
            new TimeoutOption { Display = "15 giây", Value = 15 },
            new TimeoutOption { Display = "30 giây", Value = 30 },
            new TimeoutOption { Display = "60 giây", Value = 60 }
        };

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

            if (ScanZones.Count == 0)
            {
                ScanZones.Add(new ScanZone
                {
                    Keyword = "Hoàn thành",
                    X1 = 100,
                    Y1 = 200,
                    X2 = 300,
                    Y2 = 250,
                    IsExactMatch = true
                });

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
                OnPropertyChanged(nameof(SelectedZone));
            }
            else
            {
                if (_recordService.IsRecording) return;
                _recordService.StartRecording();
                IsRecordingFound = true;
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
                OnPropertyChanged(nameof(SelectedZone));
            }
            else
            {
                if (_recordService.IsRecording) return;
                _recordService.StartRecording();
                IsRecordingNotFound = true;
                CheckStopKey(false);
            }
        }

        private void CheckStopKey(bool isFoundRecording)
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    bool recording = isFoundRecording ? IsRecordingFound : IsRecordingNotFound;
                    if (!recording) break;

                    bool isCtrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                    bool isS = (GetAsyncKeyState(VK_S) & 0x8000) != 0;

                    if (isCtrl && isS)
                    {
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
                OnPropertyChanged(nameof(SelectedZone));
                System.Windows.MessageBox.Show("Đã xóa bản ghi thành công.");
            }
        }

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
                OnPropertyChanged(nameof(SelectedZone));
                System.Windows.MessageBox.Show("Đã xóa bản ghi thành công.");
            }
        }

        [RelayCommand] private void AddZone() { ScanZones.Add(new ScanZone { Keyword = "New Zone" }); }

        [RelayCommand] private void DeleteZone(object? parameter)
        {
            if (SelectedZone == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa vùng '{SelectedZone.Keyword}' không?",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if(result == MessageBoxResult.Yes)
            {
                ScanZones.Remove(SelectedZone);
                if(parameter is ContentDialog dialog)
                    dialog.Hide();

                SelectedZone = null;
            }
        }
    }
}