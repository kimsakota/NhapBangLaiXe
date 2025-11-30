using System.Windows.Media;

namespace ToolVip.Models
{
    public class LogEntry
    {
        public string Time { get; set; } = DateTime.Now.ToString("HH:mm:ss");
        public string Message { get; set; } = "";

        // Màu sắc hiển thị (VD: "Green", "Red", "Gray")
        public string Color { get; set; } = "Black";

        public string FullMessage => $"[{Time}] {Message}";
    }
}