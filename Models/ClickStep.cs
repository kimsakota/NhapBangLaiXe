namespace ToolVip.Models
{
    public class ClickStep
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int DelayMs { get; set; }

        // [QUAN TRỌNG] Phải là public để giao diện đọc được
        public string DisplayText => $"👉 Click ({X}, {Y}) ➔ Chờ {DelayMs}ms";
    }
}