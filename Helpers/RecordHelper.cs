using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ToolVip.Helpers
{
    public class RecordHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // X bên trái
            public int Top;         // Y bên trên
            public int Right;       // X bên phải
            public int Bottom;      // Y bên dưới
        }

        // Hàm lấy tọa độ cửa sổ
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    }


}
