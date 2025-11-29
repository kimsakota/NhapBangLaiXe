using ToolVip.Models;
using ToolVip.Services;
using Wpf.Ui.Controls;

namespace ToolVip.ViewModels.Pages
{
    public partial class AutoConfigViewModel : ObservableObject
    {
        // Giả lập lưu cấu hình đơn giản (biến static hoặc singleton service)
        // Trong thực tế bạn nên lưu xuống file JSON
        public static AutoConfig CurrentConfig { get; } = new AutoConfig();

        [ObservableProperty]
        private string _keyword = CurrentConfig.StopKeyword;

        [ObservableProperty]
        private bool _isOcrEnabled = CurrentConfig.IsOcrEnabled;

        partial void OnKeywordChanged(string value) => CurrentConfig.StopKeyword = value;
        partial void OnIsOcrEnabledChanged(bool value) => CurrentConfig.IsOcrEnabled = value;
    }
}