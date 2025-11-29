namespace ToolVip.Models
{
    // Loại sự kiện chuột
    public enum MacroEventType
    {
        LeftDown,   // Nhấn chuột trái
        LeftUp,     // Nhả chuột trái (Kết hợp với LeftDown sẽ tạo ra thời gian giữ chuột)
        RightDown,  // Nhấn chuột phải
        RightUp,    // Nhả chuột phải
        Scroll      // Lăn chuột
        // Nếu cần độ chính xác cực cao về đường đi của chuột, có thể thêm MouseMove, 
        // nhưng sẽ làm file rất nặng. Với auto click, thường chỉ cần tọa độ điểm click.
    }

    public class MacroEvent
    {
        public MacroEventType Type { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int MouseData { get; set; } // Dùng cho dữ liệu lăn chuột

        // Thời gian chờ so với sự kiện trước đó (mili-giây)
        // Ví dụ: Down (Delay 0) -> Chờ 100ms -> Up (Delay 100). => Thời gian giữ chuột là 100ms.
        public int Delay { get; set; }
    }
}