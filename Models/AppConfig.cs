namespace ToolVip.Models
{
    public class AutoConfig
    {
        public string StopKeyword { get; set; } = "STOP"; // Từ khóa để dừng
        public bool IsOcrEnabled { get; set; } = true;    // Có bật OCR không
        public int OcrInterval { get; set; } = 1000;      // Quét bao lâu 1 lần (ms)
        public string Language { get; set; } = "vie";     // Ngôn ngữ (vie/eng)
    }
}