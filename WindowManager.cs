using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BorderlessMinecraft
{
    public class WindowManager
    {
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);
        
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        
        
        private const int GWL_STYLE = -16;
        
        private const int WS_BORDER = 0x00800000;
        private const int WS_RESIZE = 0x00040000;
        private const int WS_MINIMIZE = 0x00020000;
        private const int WS_MAXIMIZE = 0x00010000;
        private const int WS_CONTEXTMENU = 0x00800000;
        private const int WS_DIALOGUEBOXBORDER = 0x00400000;

        private static readonly uint SWP_NOZORDER = 0x0004;
        
        public static readonly Dictionary<int, WindowProperties> windowPropertiesByPID = new Dictionary<int, WindowProperties>();
        
        
        
        internal static void ToggleBorderless(IntPtr handle)
        {
            GetWindowThreadProcessId(handle, out uint processId);
            Rect currentPos = GetWindowRect(handle);
            if (!(currentPos.Left == 0 && currentPos.Top == 0 && currentPos.Right == Screen.PrimaryScreen.Bounds.Width && currentPos.Bottom == Screen.PrimaryScreen.Bounds.Height))
                SetBorderless(handle, (int)processId);
            else
                UnsetBorderless(handle, (int)processId);
        }
        
        
        
        internal static void SetBorderless(IntPtr handle, int processId)
        {
            windowPropertiesByPID[processId] = new WindowProperties
            {
                OriginalPos = GetWindowRect(handle),
                OriginalStyle = GetWindowLong(handle, GWL_STYLE),
            };
            
            long currentStyle = GetWindowLong(handle, GWL_STYLE);
            currentStyle &= ~(WS_BORDER | WS_RESIZE | WS_MINIMIZE | WS_MAXIMIZE | WS_CONTEXTMENU | WS_DIALOGUEBOXBORDER);
            SetWindowLong(handle, GWL_STYLE, (uint)currentStyle);
            SetWindowPos(handle, handle, 0, 0, Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, SWP_NOZORDER);
            SetForegroundWindow(handle);
        }

        private static void UnsetBorderless(IntPtr handle, int processId)
        {
            if (windowPropertiesByPID.TryGetValue(processId, out WindowProperties properties))
            {
                int originalWidth = properties.OriginalPos.Right - properties.OriginalPos.Left;
                int originalHeight = properties.OriginalPos.Bottom - properties.OriginalPos.Top;
                SetWindowLong(handle, GWL_STYLE, (uint)properties.OriginalStyle);
                SetWindowPos(handle, handle, properties.OriginalPos.Left, properties.OriginalPos.Top, originalWidth, originalHeight, SWP_NOZORDER);
                SetForegroundWindow(handle);
                
                windowPropertiesByPID.Remove(processId);
            }
        }
        
        

        public static Rect GetWindowRect(IntPtr handle)
        {
            Rect rect;
            if (GetWindowRect(handle, out rect))
                return rect;
            throw new Exception("Failed to get window rect.");
        }

        public struct Rect
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }
        
        
        
        public struct WindowProperties
        {
            public long OriginalStyle;
            public Rect OriginalPos;
        }
        
        
        
        internal static bool IsFullscreen(IntPtr handle)
        {
            return GetWindowLong(handle, GWL_STYLE) < 0;
        }
        
    }
}