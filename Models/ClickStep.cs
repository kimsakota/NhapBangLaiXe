namespace ToolVip.Models
{
    public class ClickStep
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int DelayMs { get; set; }

        public string DisplayText => $"👉 Click ({X}, {Y}) ➔ Chờ {DelayMs}ms";
    }
}