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

        // Thuộc tính phụ trợ để hiển thị đẹp trên UI
        public string CoordinateString => $"({X1}, {Y1}) - ({X2}, {Y2})";
        public string MatchTypeString => IsExactMatch ? "Chính xác" : "Gần đúng";
    }
}