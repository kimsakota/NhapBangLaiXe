using System.Collections.Generic;

namespace ToolVip.Models
{
    public class ScanZone
    {
        public string Keyword { get; set; } = "";
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public bool IsExactMatch { get; set; } = true;

        // [MỚI] Chiến thuật chạy: 0 = Cùng với nút Play (Mặc định), 1 = Sau khi chạy xong Record chính
        public int RunStrategy { get; set; } = 0;

        // [MỚI] Thời gian quét tối đa (giây). 0 = Vô hạn (hoặc theo Record chính)
        public int ScanTimeout { get; set; } = 0;

        public List<MacroEvent> FoundActions { get; set; } = new();
        public List<MacroEvent> NotFoundActions { get; set; } = new();

        public string CoordinateString => $"({X1}, {Y1}) - ({X2}, {Y2})";
        public string MatchTypeString => IsExactMatch ? "Chính xác" : "Gần đúng";
    }
}