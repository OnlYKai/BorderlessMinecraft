using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BorderlessMinecraft
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Context());
        }
    }
    
    
    
    internal class Context : ApplicationContext
    {
        public static bool autoborderless;
        
        private static bool _autostart;
        
        private readonly NotifyIcon _trayIcon;
        private readonly WindowMonitor _windowMonitor;
        public static KeyboardHook _keyboardHook;
        
        public static CancellationTokenSource cts = new CancellationTokenSource();
        
        public Context()
        {
            // Create Tray Icon context menu and it's items
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            
            contextMenu.Items.Add("Autoborderless", null, OnAutoborderless);
            contextMenu.Items.Add("Autostart", null, OnAutostart);
            contextMenu.Items.Add("Exit", null, OnExit);
            
            // Item settings and set state (bool defaults to false)
            if (contextMenu.Items[0] is ToolStripMenuItem item0)
            {
                item0.CheckOnClick = true;
                item0.Checked = IsAutoborderlessEnabled();
            }
            
            if (contextMenu.Items[1] is ToolStripMenuItem item1)
            {
                item1.CheckOnClick = true;
                item1.Checked = IsAutostartEnabled();
            }
            
            // Create Tray Icon
            _trayIcon = new NotifyIcon()
            {
                Text = "BorderlessMinecraft",
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
                ContextMenuStrip = contextMenu,
                Visible = true,
            };
            
            // Setup KeyboardHook
            _keyboardHook = new KeyboardHook();
            // Start in WindowMonitor, Stop in Cleaner (and Exit ofc)
            
            // Setup Window Monitor
            _windowMonitor = new WindowMonitor();
            _windowMonitor.Start();
            
            // Start Cleaner
            CleanProcesses();
        }
        
        // Action when clicking 'Autoborderless'
        private void OnAutoborderless(Object sender, EventArgs e)
        {
            autoborderless = !autoborderless;
            
            if (autoborderless)
                Registry.CurrentUser.CreateSubKey(@"Software\AutoBorderlessMinecraft");
            else
                Registry.CurrentUser.DeleteSubKey(@"Software\AutoBorderlessMinecraft", false);
        }
        
        // Action when clicking 'Autostart'
        private void OnAutostart(Object sender, EventArgs e)
        {
            _autostart = !_autostart;
            
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) 
            {
                if (_autostart)
                    key.SetValue("BorderlessMinecraft", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BorderlessMinecraft.exe"));
                else
                    key.DeleteValue("BorderlessMinecraft", false);
            }
        }
        
        // Action when clicking 'Exit'
        private void OnExit(Object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            cts.Cancel();
            _windowMonitor.Stop();
            _keyboardHook.RemoveHook();
            Application.Exit();
        }
        
        
        // Check if 'Autoborderless' is enabled
        private bool IsAutoborderlessEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\AutoBorderlessMinecraft", false))
            {
                autoborderless = key != null;
                return key != null;
            }
        }
        
        // Check if 'Autostart' is enabled
        private bool IsAutostartEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
            {
                String currentPath = key?.GetValue("BorderlessMinecraft") as string;
                if (currentPath == null || !currentPath.Equals(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BorderlessMinecraft.exe")))
                    return false;
                _autostart = true;
                return true;
            }
        }
        
        

        // Cleaner for PIDs got in WindowMonitor and WindowManager (the Token stuff makes it cancellable when the program exits)
        private static void CleanProcesses()
        {
            Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    while (WindowMonitor.processesDetected.Count > 0 && !cts.Token.IsCancellationRequested)
                    {
                        List<int> processesTerminated = new List<int>();

                        foreach (int pid in WindowMonitor.processesDetected)
                        {
                            try
                            {
                                Process.GetProcessById(pid);
                            }
                            catch (ArgumentException)
                            {
                                processesTerminated.Add(pid);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"Error checking PID {pid}: {e.Message}");
                            }
                        }
                        
                        foreach (int pid in processesTerminated)
                        {
                            WindowMonitor.processesDetected.Remove(pid);
                            WindowManager.windowPropertiesByPID.Remove(pid);
                        }
                        
                        // Remove KeyboardHook if no minecraft process is left
                        if (WindowMonitor.processesDetected.Count == 0)
                            _keyboardHook.RemoveHook();
                        
                        cts.Token.WaitHandle.WaitOne(10000);
                    }

                    cts.Token.WaitHandle.WaitOne(10000);
                }
            }, cts.Token);
        }
        
    }
}