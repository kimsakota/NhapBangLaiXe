using System;
using System.Drawing; // Cần cài NuGet: System.Drawing.Common
using System.Drawing.Imaging;
using System.IO;
using System.Windows; // Để dùng MessageBox và SystemParameters
using Tesseract;      // Cần cài NuGet: Tesseract

namespace ToolVip.Services
{
    public interface IOcrService
    {
        string GetTextFromScreen();
        void Init(string lang);
    }

    public class OcrService : IOcrService
    {
        private TesseractEngine? _engine;
        private readonly string _tessDataPath;

        public OcrService()
        {
            // Xác định đường dẫn đến thư mục tessdata nằm cùng cấp với file chạy .exe
            _tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
        }

        public void Init(string lang)
        {
            try
            {
                // Giải phóng engine cũ nếu có
                if (_engine != null)
                {
                    _engine.Dispose();
                    _engine = null;
                }

                // Kiểm tra thư mục tessdata có tồn tại không
                if (!Directory.Exists(_tessDataPath))
                {
                    MessageBox.Show($"Không tìm thấy thư mục dữ liệu OCR tại:\n{_tessDataPath}\nVui lòng copy thư mục 'tessdata' vào đây.", "Thiếu dữ liệu");
                    return;
                }

                // Khởi tạo engine mới
                _engine = new TesseractEngine(_tessDataPath, lang, EngineMode.Default);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khởi tạo Tesseract ({lang}): {ex.Message}\nHãy kiểm tra file ngôn ngữ trong thư mục tessdata.", "Lỗi OCR");
            }
        }

        public string GetTextFromScreen()
        {
            // Nếu chưa init hoặc init lỗi thì trả về rỗng
            if (_engine == null) return "";

            try
            {
                // 1. Lấy kích thước màn hình chính (Primary Screen)
                // Lưu ý: SystemParameters trả về kích thước logical (DPI-aware), 
                // có thể cần nhân với tỉ lệ DPI nếu muốn chụp pixel chính xác tuyệt đối.
                // Ở đây dùng cách đơn giản nhất cho WPF.

                int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
                int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

                if (screenWidth <= 0 || screenHeight <= 0) return "";

                // 2. Chụp màn hình vào Bitmap
                using (var bitmap = new Bitmap(screenWidth, screenHeight))
                {
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        // Copy từ điểm (0,0) của màn hình
                        g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
                    }

                    // 3. Đưa ảnh vào Tesseract để xử lý OCR
                    using (var page = _engine.Process(bitmap))
                    {
                        // Lấy text với độ tin cậy trung bình
                        // var confidence = page.GetMeanConfidence(); 
                        return page.GetText();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log lỗi ra cửa sổ Output của Visual Studio để debug
                System.Diagnostics.Debug.WriteLine($"[OCR Error] {ex.Message}");
                return "";
            }
        }
    }
}