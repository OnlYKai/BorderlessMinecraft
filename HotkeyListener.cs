// CURRENTLY NOT USED!!!
// Kept in case I need to use HotKey instead of KeyboardHook

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace BorderlessMinecraft
{
    public class HotkeyListener : NativeWindow, IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, int vk);
        
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        
        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        
        private static bool _isRegistered;
        
        
        // Create Handle to register Hotkeys on
        public HotkeyListener()
        {
            CreateHandle(new CreateParams());
        }
        
        // Register/Unregister Hotkey
        public void RegisterHotKey()
        {
            if (_isRegistered)
                return;
            RegisterHotKey(Handle, 1, 0, (int)Keys.F11);
            _isRegistered = true;
            Console.WriteLine("Hotkey registered!");
        }
        
        public void UnregisterHotKey()
        {
            if (!_isRegistered)
                return;
            UnregisterHotKey(Handle, 1);
            _isRegistered = false;
            Console.WriteLine("Hotkey unregistered!");
        }

        
        // Action when HotkeyListener catches the input
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == 1)
            {
                IntPtr handle = GetForegroundWindow();
                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(handle, windowText, windowText.Capacity);
                
                string windowTitle = windowText.ToString().Trim();
                Regex regexTitle = new Regex("^Minecraft(?!.*(?i)server).*$");
                
                // Check if it's a Minecraft window, otherwise send "F11"
                if (IsJavaProcess(handle) && regexTitle.IsMatch(windowTitle) && WindowManager.IsNotFullscreen(handle))
                    WindowManager.ToggleBorderless(handle);
                else
                {
                    PostMessage(handle, 0x0100, (IntPtr)0x7A, IntPtr.Zero);
                    PostMessage(handle, 0x0101, (IntPtr)0x7A, IntPtr.Zero);
                    Console.WriteLine("Posted F11 Message.");
                }
            }
            base.WndProc(ref m);
        }
        
        private bool IsJavaProcess(IntPtr handle)
        {
            try
            {
                GetWindowThreadProcessId(handle, out uint processId);
                Process process = Process.GetProcessById((int)processId);
                return process.ProcessName.Equals("javaw", StringComparison.OrdinalIgnoreCase) || process.ProcessName.Equals("java", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
        
        
        
        public void Dispose()
        {
            UnregisterHotKey();
            DestroyHandle();
        }
        
    }
}