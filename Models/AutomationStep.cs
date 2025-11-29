namespace ToolVip.Models
{
    public class AutomationStep
    {
        public string Type { get; set; } = "Mouse"; // "Mouse" hoặc "Key"

        // Dữ liệu cho Mouse
        public int X { get; set; }
        public int Y { get; set; }

        // Dữ liệu cho Key
        public string Key { get; set; } = "";

        public int Delay { get; set; } = 1000;

        // Hiển thị trên UI
        public string Description
        {
            get
            {
                if (Type == "Mouse")
                    return $"Click ({X}, {Y}) - Chờ {Delay}ms";
                else
                    return $"Nhấn phím [{Key}] - Chờ {Delay}ms";
            }
        }
    }
}