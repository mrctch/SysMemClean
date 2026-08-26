using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MemoryCleanerApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }

    public partial class Form1 : Form
    {
        private const string AppRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string ConfigRegistryKey = @"SOFTWARE\SysMemClean";
        private const string AppName = "SysMemClean";

        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;
        private System.Windows.Forms.Timer timerMonitor;
        private NumericUpDown numThreshold;
        private NumericUpDown numInterval;
        private CheckBox chkAlwaysClean;
        private CheckBox chkAutoStart;
        private Button btnCleanNow;
        private Label lblStatus;
        private Icon appIcon;

        // Banderas de traducción
        private bool isEnglish;
        private ToolStripMenuItem menuOpenItem;
        private ToolStripMenuItem menuCleanItem;
        private ToolStripMenuItem menuExitItem;

        public Form1()
        {
            // Detectar si el idioma principal de la UI del sistema es Inglés
            isEnglish = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase);

            CheckAdministratorPrivileges();
            appIcon = GenerateTechnicalIcon();
            this.Icon = appIcon;
            
            InitializeCustomComponents();
            LoadSavedSettings();
            SetupTrayIcon();
            NativeMemoryOptimizer.TrimProcessMemory();
        }

        private Icon GenerateTechnicalIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(24, 28, 36)))
                    {
                        g.FillRectangle(bgBrush, 2, 2, 28, 28);
                    }

                    using (Pen borderPen = new Pen(Color.FromArgb(0, 150, 255), 1.5f))
                    {
                        g.DrawRectangle(borderPen, 2, 2, 27, 27);
                    }

                    using (SolidBrush pinBrush = new SolidBrush(Color.FromArgb(0, 200, 255)))
                    {
                        g.FillRectangle(pinBrush, 0, 8, 2, 3);
                        g.FillRectangle(pinBrush, 0, 21, 2, 3);
                        g.FillRectangle(pinBrush, 30, 8, 2, 3);
                        g.FillRectangle(pinBrush, 30, 21, 2, 3);
                        g.FillRectangle(pinBrush, 8, 0, 3, 2);
                        g.FillRectangle(pinBrush, 21, 0, 3, 2);
                        g.FillRectangle(pinBrush, 8, 30, 3, 2);
                        g.FillRectangle(pinBrush, 21, 30, 3, 2);
                    }

                    Point[] boltPoints = new Point[]
                    {
                        new Point(18, 5),
                        new Point(10, 16),
                        new Point(16, 16),
                        new Point(14, 27),
                        new Point(22, 14),
                        new Point(16, 14)
                    };

                    using (SolidBrush boltBrush = new SolidBrush(Color.FromArgb(50, 205, 50)))
                    {
                        g.FillPolygon(boltBrush, boltPoints);
                    }
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        private void InitializeCustomComponents()
        {
            this.Text = "SysMemClean - By marco_tch";
            this.Size = new Size(380, 290);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            Label lblThreshold = new Label();
            lblThreshold.Text = isEnglish ? "RAM Threshold (%):" : "Umbral de RAM (%):";
            lblThreshold.Location = new Point(20, 20);
            lblThreshold.AutoSize = true;

            numThreshold = new NumericUpDown();
            numThreshold.Location = new Point(200, 18);
            numThreshold.Width = 130;
            numThreshold.Minimum = 1;
            numThreshold.Maximum = 99;
            numThreshold.Value = 80;
            numThreshold.ValueChanged += new EventHandler(SettingChanged);

            Label lblInterval = new Label();
            lblInterval.Text = isEnglish ? "Interval (Seconds):" : "Intervalo (Segundos):";
            lblInterval.Location = new Point(20, 55);
            lblInterval.AutoSize = true;

            numInterval = new NumericUpDown();
            numInterval.Location = new Point(200, 53);
            numInterval.Width = 130;
            numInterval.Minimum = 1;
            numInterval.Maximum = 86400;
            numInterval.Value = 30;
            numInterval.ValueChanged += new EventHandler(numInterval_ValueChanged);

            chkAlwaysClean = new CheckBox();
            chkAlwaysClean.Text = isEnglish ? "Always clean by interval (ignore threshold)" : "Limpiar siempre por intervalo (ignorar umbral)";
            chkAlwaysClean.Location = new Point(20, 90);
            chkAlwaysClean.AutoSize = true;
            chkAlwaysClean.CheckedChanged += new EventHandler(SettingChanged);

            chkAutoStart = new CheckBox();
            chkAutoStart.Text = isEnglish ? "Start with Windows" : "Iniciar con Windows";
            chkAutoStart.Location = new Point(20, 118);
            chkAutoStart.AutoSize = true;
            chkAutoStart.Checked = IsAutoStartEnabled();
            chkAutoStart.CheckedChanged += new EventHandler(chkAutoStart_CheckedChanged);

            btnCleanNow = new Button();
            btnCleanNow.Text = isEnglish ? "⚡ Clean RAM Now" : "⚡ Limpiar RAM Ahora";
            btnCleanNow.Location = new Point(20, 150);
            btnCleanNow.Width = 310;
            btnCleanNow.Height = 35;
            btnCleanNow.BackColor = Color.LightSteelBlue;
            btnCleanNow.Click += new EventHandler(btnCleanNow_Click);

            lblStatus = new Label();
            lblStatus.Text = isEnglish ? "Status: Idle" : "Estado: En espera";
            lblStatus.Location = new Point(20, 200);
            lblStatus.AutoSize = true;
            lblStatus.ForeColor = Color.Gray;

            this.Controls.Add(lblThreshold);
            this.Controls.Add(numThreshold);
            this.Controls.Add(lblInterval);
            this.Controls.Add(numInterval);
            this.Controls.Add(chkAlwaysClean);
            this.Controls.Add(chkAutoStart);
            this.Controls.Add(btnCleanNow);
            this.Controls.Add(lblStatus);

            timerMonitor = new System.Windows.Forms.Timer();
            timerMonitor.Interval = (int)numInterval.Value * 1000;
            timerMonitor.Tick += new EventHandler(TimerMonitor_Tick);
            timerMonitor.Start();

            this.Load += new EventHandler(Form1_Load);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Hide();
        }

        private void numInterval_ValueChanged(object sender, EventArgs e)
        {
            timerMonitor.Interval = (int)numInterval.Value * 1000;
            SaveSettings();
        }

        private void SettingChanged(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void chkAutoStart_CheckedChanged(object sender, EventArgs e)
        {
            SetAutoStart(chkAutoStart.Checked);
        }

        private void btnCleanNow_Click(object sender, EventArgs e)
        {
            ExecuteCleanSequence();
        }

        private void SetupTrayIcon()
        {
            contextMenu = new ContextMenuStrip();
            menuOpenItem = new ToolStripMenuItem(isEnglish ? "Open Settings" : "Abrir Configuración", null, new EventHandler(MenuRestore_Click));
            menuCleanItem = new ToolStripMenuItem(isEnglish ? "Clean RAM Now" : "Limpiar RAM Ahora", null, new EventHandler(MenuClean_Click));
            menuExitItem = new ToolStripMenuItem(isEnglish ? "Exit" : "Salir", null, new EventHandler(MenuExit_Click));

            contextMenu.Items.Add(menuOpenItem);
            contextMenu.Items.Add(menuCleanItem);
            contextMenu.Items.Add("-");
            contextMenu.Items.Add(menuExitItem);

            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = appIcon;
            notifyIcon.Visible = true;
            notifyIcon.Text = "SysMemClean - By marco_tch";
            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.DoubleClick += new EventHandler(MenuRestore_Click);
        }

        private void MenuRestore_Click(object sender, EventArgs e) { RestoreWindow(); }
        private void MenuClean_Click(object sender, EventArgs e) { ExecuteCleanSequence(); }
        private void MenuExit_Click(object sender, EventArgs e) { ExitApp(); }

        private void TimerMonitor_Tick(object sender, EventArgs e)
        {
            float ramUsage = NativeMemoryOptimizer.GetSystemRamUsagePercentage();
            
            if (chkAlwaysClean.Checked || ramUsage >= (float)numThreshold.Value)
            {
                ExecuteCleanSequence();
            }
            else
            {
                NativeMemoryOptimizer.TrimProcessMemory();
            }
        }

        private void ExecuteCleanSequence()
        {
            try
            {
                NativeMemoryOptimizer.CleanAllSystemMemory();
                lblStatus.Text = string.Format(isEnglish ? "Last cleanup: {0:HH:mm:ss}" : "Última limpieza: {0:HH:mm:ss}", DateTime.Now);
                
                string title = "SysMemClean";
                string message = isEnglish ? "RAM memory purged successfully." : "Memoria RAM purgada con éxito.";
                notifyIcon.ShowBalloonTip(2000, title, message, ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                string title = isEnglish ? "Error" : "Error";
                string msgFormat = isEnglish ? "Failed to purge memory: {0}" : "Error al purgar memoria: {0}";
                MessageBox.Show(string.Format(msgFormat, ex.Message), title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                NativeMemoryOptimizer.TrimProcessMemory();
            }
        }

        private void RestoreWindow()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.BringToFront();
        }

        private void ExitApp()
        {
            notifyIcon.Visible = false;
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Hide();
                NativeMemoryOptimizer.TrimProcessMemory();
            }
            base.OnFormClosing(e);
        }

        private void LoadSavedSettings()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ConfigRegistryKey, false))
            {
                if (key != null)
                {
                    object thresholdVal = key.GetValue("Threshold");
                    object intervalVal = key.GetValue("Interval");
                    object alwaysCleanVal = key.GetValue("AlwaysClean");

                    if (thresholdVal != null) numThreshold.Value = Convert.ToDecimal(thresholdVal);
                    if (intervalVal != null) numInterval.Value = Convert.ToDecimal(intervalVal);
                    if (alwaysCleanVal != null) chkAlwaysClean.Checked = Convert.ToBoolean(alwaysCleanVal);
                }
            }
        }

        private void SaveSettings()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(ConfigRegistryKey))
            {
                if (key != null)
                {
                    key.SetValue("Threshold", (int)numThreshold.Value);
                    key.SetValue("Interval", (int)numInterval.Value);
                    key.SetValue("AlwaysClean", chkAlwaysClean.Checked.ToString());
                }
            }
        }

        private bool IsAutoStartEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppRegistryKey, false))
            {
                return key != null && key.GetValue(AppName) != null;
            }
        }

        private void SetAutoStart(bool enable)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppRegistryKey, true))
            {
                if (key == null) return;

                if (enable)
                    key.SetValue(AppName, string.Format("\"{0}\"", Application.ExecutablePath));
                else
                    key.DeleteValue(AppName, false);
            }
        }

        private void CheckAdministratorPrivileges()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    string title = isEnglish ? "Admin Privileges Required" : "Privilegios Requeridos";
                    string msg = isEnglish 
                        ? "Warning: Administrator rights are required to purge system RAM." 
                        : "Atención: Esta aplicación requiere derechos de Administrador para purgar la RAM.";
                    MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }

    public static class NativeMemoryOptimizer
    {
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSetSystemInformation(int SystemInformationClass, ref int SystemInformation, int SystemInformationLength);

        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwnd);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES { public LUID Luid; public uint Attributes; }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES { public uint PrivilegeCount; public LUID_AND_ATTRIBUTES Privileges; }

        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint SE_PRIVILEGE_ENABLED = 0x0002;

        public enum MemoryListCommand
        {
            EmptyWorkingSets = 2,
            EmptySystemWorkingSet = 3,
            EmptyModifiedPageList = 4,
            EmptyStandbyList = 5,
            EmptyPriority0StandbyList = 6
        }

        public static float GetSystemRamUsagePercentage()
        {
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                return memStatus.dwMemoryLoad;
            }
            return 0;
        }

        public static void CleanAllSystemMemory()
        {
            EnablePrivilege("SeIncreaseQuotaPrivilege");
            EnablePrivilege("SeProfileSingleProcessPrivilege");

            ExecuteCommand(MemoryListCommand.EmptyWorkingSets);
            ExecuteCommand(MemoryListCommand.EmptySystemWorkingSet);
            ExecuteCommand(MemoryListCommand.EmptyModifiedPageList);
            ExecuteCommand(MemoryListCommand.EmptyStandbyList);
            ExecuteCommand(MemoryListCommand.EmptyPriority0StandbyList);
        }

        public static void TrimProcessMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }

        private static void ExecuteCommand(MemoryListCommand command)
        {
            int commandValue = (int)command;
            NtSetSystemInformation(80, ref commandValue, sizeof(int));
        }

        private static void EnablePrivilege(string privilegeName)
        {
            IntPtr tokenHandle;
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out tokenHandle))
                return;

            LUID luid;
            if (LookupPrivilegeValue(null, privilegeName, out luid))
            {
                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                tp.PrivilegeCount = 1;
                tp.Privileges = new LUID_AND_ATTRIBUTES();
                tp.Privileges.Luid = luid;
                tp.Privileges.Attributes = SE_PRIVILEGE_ENABLED;

                AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }
}