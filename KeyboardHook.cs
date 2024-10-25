using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace BorderlessMinecraft
{
    public class KeyboardHook
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        
        
        private static bool _isHooked;
        
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        
        private static LowLevelKeyboardProc _proc;
        private static IntPtr _hookID;
        
        private const int WH_KEYBOARD_LL = 13;  // Low-level keyboard hook constant
        private const int WM_KEYDOWN = 0x0100;  // Key down message constant
        
        
        public KeyboardHook()
        {
            _proc = HookCallback;
        }
        
        // Start/Stop KeyboardHook
        public void SetHook()
        {
            if (_isHooked)
                return;
            _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);
            _isHooked = true;
            Console.WriteLine("Hook set!");
        }

        public void RemoveHook()
        {
            if (!_isHooked)
                return;
            UnhookWindowsHookEx(_hookID);
            _isHooked = false;
            Console.WriteLine("Hook removed!");
        }


        
        // Action when KeyboardHook catches an input
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if ((Keys)Marshal.ReadInt32(lParam) != Keys.F11)
                return CallNextHookEx(_hookID, nCode, wParam, lParam);
            
            if (!(nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN))
                return CallNextHookEx(_hookID, nCode, wParam, lParam);
            
            IntPtr handle = GetForegroundWindow();
            StringBuilder windowText = new StringBuilder(256);
            GetWindowText(handle, windowText, windowText.Capacity);
                
            string windowTitle = windowText.ToString().Trim();
            Regex regexTitle = new Regex("^Minecraft(?!.*(?i)server).*$");

            if (!(IsJavaProcess(handle) && regexTitle.IsMatch(windowTitle)))
                return CallNextHookEx(_hookID, nCode, wParam, lParam);
            
            WindowManager.ToggleBorderless(handle);
            return (IntPtr)1;
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
        
    }
}