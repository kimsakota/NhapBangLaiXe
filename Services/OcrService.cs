using System.Drawing;
using System.IO;
using System.Windows;
using Tesseract;
// Bỏ using MessageBox để tránh hiện popup làm gián đoạn auto

namespace ToolVip.Services
{
    public interface IOcrService
    {
        string GetTextFromScreen();
        string GetTextFromRegion(int x, int y, int w, int h);

        // [CẬP NHẬT] Hàm Init trả về kết quả chi tiết (Thành công?, Thông báo lỗi)
        (bool Success, string Message) Init(string lang);
    }

    public class OcrService : IOcrService
    {
        private TesseractEngine? _engine;
        private readonly string _tessDataPath;

        public OcrService()
        {
            _tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
        }

        public (bool Success, string Message) Init(string lang)
        {
            try
            {
                if (_engine != null)
                {
                    _engine.Dispose();
                    _engine = null;
                }

                // 1. Kiểm tra thư mục tessdata
                if (!Directory.Exists(_tessDataPath))
                {
                    return (false, $"Không tìm thấy thư mục 'tessdata' tại: {_tessDataPath}");
                }

                // 2. Kiểm tra file ngôn ngữ cụ thể (ví dụ: vie.traineddata)
                string langFile = Path.Combine(_tessDataPath, $"{lang}.traineddata");
                if (!File.Exists(langFile))
                {
                    return (false, $"Thiếu file ngôn ngữ '{lang}.traineddata' trong thư mục tessdata.");
                }

                // 3. Khởi tạo Engine
                _engine = new TesseractEngine(_tessDataPath, lang, EngineMode.Default);
                return (true, $"Khởi tạo OCR ({lang}) thành công. Sẵn sàng quét!");
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi khởi tạo Tesseract: {ex.Message}");
            }
        }

        public string GetTextFromScreen()
        {
            int w = (int)SystemParameters.PrimaryScreenWidth;
            int h = (int)SystemParameters.PrimaryScreenHeight;
            return GetTextFromRegion(0, 0, w, h);
        }

        public string GetTextFromRegion(int x, int y, int w, int h)
        {
            if (_engine == null) return "";
            if (w <= 0 || h <= 0) return "";

            try
            {
                using (var bitmap = new Bitmap(w, h))
                {
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(x, y, 0, 0, bitmap.Size);
                    }

                    using (var stream = new MemoryStream())
                    {
                        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
                        using (var pix = Pix.LoadFromMemory(stream.ToArray()))
                        {
                            using (var page = _engine.Process(pix))
                            {
                                if (page == null) return "";
                                // [Mẹo] Trim() để xóa khoảng trắng thừa đầu đuôi
                                return page.GetText()?.Trim() ?? "";
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}