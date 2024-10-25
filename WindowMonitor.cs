using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BorderlessMinecraft
{
    public class WindowMonitor
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out WindowManager.Rect lpRect);

        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);
        
        //[DllImport("gdi32.dll")]
        //private static extern uint SetPixel(IntPtr hdc, int x, int y, uint crColor);


        public static List<int> processesDetected = new List<int>();
        
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr handle, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
        
        private WinEventDelegate _winEventDelegate;
        private IntPtr _hook;
        
        private const uint EVENT_CREATED = 0x8000;
        private const uint EVENT_TITLECHANGED = 0x800C;
        
        
        // Start/Stop WindowMonitor
        public void Start()
        {
            _winEventDelegate = new WinEventDelegate(WinEventProc);
            _hook = SetWinEventHook(EVENT_CREATED, EVENT_TITLECHANGED, IntPtr.Zero, _winEventDelegate, 0, 0, 0);
        }

        public void Stop()
        {
            if (_hook != IntPtr.Zero)
                UnhookWinEvent(_hook);
        }
        
        
        
        // Action when WindowMonitor catches an event
        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr handle, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (!(eventType == EVENT_CREATED || eventType == EVENT_TITLECHANGED))
                return;
            
            if (handle == IntPtr.Zero)
                return;
            
            StringBuilder windowText = new StringBuilder(256);
            GetWindowText(handle, windowText, windowText.Capacity);
            string windowTitle = windowText.ToString().Trim();

            if (windowTitle.Length == 0)
                return;
            
            Regex regexTitle = new Regex("^Minecraft(?!.*(?i)server).*$");

            GetWindowThreadProcessId(handle, out uint processId);
            Process process = Process.GetProcessById((int)processId);
            bool isJava = process.ProcessName.Equals("javaw", StringComparison.OrdinalIgnoreCase) || process.ProcessName.Equals("java", StringComparison.OrdinalIgnoreCase);
            
            if (!(isJava && regexTitle.IsMatch(windowTitle)))
                return;
            
            if (processesDetected.Contains((int)processId))
                return;
            
            // Add PID to list to make sure autoborderless is only executes once, even if the window is recreated
            processesDetected.Add((int)processId);
            
            // Set KeyboardHook while minecraft is open
            Context._keyboardHook.SetHook();
            
            Console.WriteLine(windowTitle);
            
            if (!Context.autoborderless)
                return;
            
            // Run all the waiting stuff in its own Task, so it doesn't block the main Thread (the Token stuff makes it cancellable when the program exits)
            Task.Run(() =>
            {
                Context._cts.Token.WaitHandle.WaitOne(300);
                
                while (IsLoadingColor(handle) && !Context._cts.Token.IsCancellationRequested)
                    Context._cts.Token.WaitHandle.WaitOne(100);
                
                if (Context._cts.Token.IsCancellationRequested)
                    return;
                
                if (!WindowManager.IsNotFullscreen(handle))
                    return;

                WindowManager.Rect currentPos = WindowManager.GetWindowRect(handle);
                if (currentPos.Left == 0 && currentPos.Top == 0 && currentPos.Right == Screen.PrimaryScreen.Bounds.Width && currentPos.Bottom == Screen.PrimaryScreen.Bounds.Height)
                    return;

                WindowManager.SetBorderless(handle, (int)processId);
            }, Context._cts.Token);
        }
        
        // Checking 4 corners' color to determine if minecraft is still starting
        private bool IsLoadingColor(IntPtr handle)
        {
            IntPtr hdc = GetDC(handle);
            if (hdc == IntPtr.Zero)
                return false;

            WindowManager.Rect rect;
            GetClientRect(handle, out rect);
            
            // Check if correct numbers are retrieved
            //Console.WriteLine(rect.Left);
            //Console.WriteLine(rect.Top);
            //Console.WriteLine(rect.Right);
            //Console.WriteLine(rect.Bottom);
            
            int offset = 10;
            // Calculate corner coordinates with the specified offset
            int left = rect.Left + offset;
            int top = rect.Top + offset;
            int right = rect.Right - offset;
            int bottom = rect.Bottom - offset;
            
            // Set pixels for testing
            //SetPixel(hdc, left, top, 0x0000FF);
            //SetPixel(hdc, right, top, 0x0000FF);
            //SetPixel(hdc, left, bottom, 0x0000FF);
            //SetPixel(hdc, right, bottom, 0x0000FF);
            
            // Get the pixel colors
            Color topLeftColor = ColorTranslator.FromWin32((int)GetPixel(hdc, left, top));
            Color topRightColor = ColorTranslator.FromWin32((int)GetPixel(hdc, right, top));
            Color bottomLeftColor = ColorTranslator.FromWin32((int)GetPixel(hdc, left, bottom));
            Color bottomRightColor = ColorTranslator.FromWin32((int)GetPixel(hdc, right, bottom));

            ReleaseDC(handle, hdc);

            // Print colors for testing
            //Console.WriteLine(topLeftColor);
            //Console.WriteLine(topRightColor);
            //Console.WriteLine(bottomLeftColor);
            //Console.WriteLine(bottomRightColor);
            
            // Check if all corners have specific color
            return IsBlack(topLeftColor, topRightColor, bottomLeftColor, bottomRightColor) || 
                   IsWhite(topLeftColor, topRightColor, bottomLeftColor, bottomRightColor) || 
                   IsRed(topLeftColor, topRightColor, bottomLeftColor, bottomRightColor); 
        }
    
        private bool IsBlack(Color topLeftColor, Color topRightColor, Color bottomLeftColor, Color bottomRightColor)
        {
            return topLeftColor == Color.Black && topRightColor == Color.Black && bottomLeftColor == Color.Black && bottomRightColor == Color.Black;
        }
        
        private bool IsWhite(Color topLeftColor, Color topRightColor, Color bottomLeftColor, Color bottomRightColor)
        {
            return topLeftColor == Color.White && topRightColor == Color.White && bottomLeftColor == Color.White && bottomRightColor == Color.White;
        }
        
        private bool IsRed(Color topLeftColor, Color topRightColor, Color bottomLeftColor, Color bottomRightColor)
        {
            Color red = Color.FromArgb(255, 239, 50, 61);
            return topLeftColor == red && topRightColor == red && bottomLeftColor == red && bottomRightColor == red;
        }
        
    }
}