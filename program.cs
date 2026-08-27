using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace MemoryCleanerApp
{
    internal static class Program
    {
        private static Mutex mutex = null;

        [STAThread]
        static void Main()
        {
            bool createdNew;
            mutex = new Mutex(true, "Global\\SysMemCleanUniqueMutexAppKey", out createdNew);

            if (!createdNew)
            {
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

            GC.KeepAlive(mutex);
        }
    }

    public partial class Form1 : Form
    {
        private const string TaskName = "SysMemCleanAutoStart";
        private string configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;
        private System.Windows.Forms.Timer timerMonitor;
        private NumericUpDown numThreshold;
        private NumericUpDown numInterval;
        private CheckBox chkEnableThreshold;
        private CheckBox chkAutoStart;
        private Button btnCleanNow;
        private Label lblStatus;

        public Form1()
        {
            CheckAdministratorPrivileges();
            InitializeCustomComponents();
            SetupTrayIcon();
            LoadConfiguration();

            // Si el inicio automático ya estaba activo, actualiza la ruta registrada
            // por si el usuario movió la carpeta o el ejecutable de lugar.
            if (IsTaskScheduled())
            {
                UpdateAutoStartTaskPath();
            }

            ExecuteCleanSequence();
        }

        private void InitializeCustomComponents()
        {
            this.Text = "SysMemClean";
            this.Size = new Size(360, 290);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            chkEnableThreshold = new CheckBox { Text = "Usar umbral de RAM (%):", Location = new Point(20, 20), AutoSize = true, Checked = false };
            chkEnableThreshold.CheckedChanged += delegate(object sender, EventArgs e) 
            { 
                numThreshold.Enabled = chkEnableThreshold.Checked;
                SaveConfiguration(); 
            };

            numThreshold = new NumericUpDown { Location = new Point(190, 18), Width = 120, Minimum = 50, Maximum = 99, Value = 80, Enabled = false };
            numThreshold.ValueChanged += delegate(object sender, EventArgs e) { SaveConfiguration(); };

            Label lblInterval = new Label { Text = "Intervalo (Segundos):", Location = new Point(20, 60), AutoSize = true };
            numInterval = new NumericUpDown { Location = new Point(190, 58), Width = 120, Minimum = 5, Maximum = 3600, Value = 30 };
            numInterval.ValueChanged += delegate(object sender, EventArgs e) 
            { 
                timerMonitor.Interval = (int)numInterval.Value * 1000; 
                SaveConfiguration();
            };

            chkAutoStart = new CheckBox { Text = "Iniciar con Windows", Location = new Point(20, 100), AutoSize = true, Checked = IsTaskScheduled() };
            chkAutoStart.CheckedChanged += delegate(object sender, EventArgs e) 
            { 
                ToggleAutoStartTask(chkAutoStart.Checked); 
            };

            btnCleanNow = new Button { Text = "Limpiar RAM Ahora", Location = new Point(20, 135), Width = 290, Height = 35, BackColor = Color.LightSteelBlue };
            btnCleanNow.Click += delegate(object sender, EventArgs e) 
            { 
                ExecuteCleanSequence(); 
            };

            lblStatus = new Label { Text = "Estado: En espera", Location = new Point(20, 185), AutoSize = true, ForeColor = Color.Gray };

            this.Controls.Add(chkEnableThreshold);
            this.Controls.Add(numThreshold);
            this.Controls.Add(lblInterval);
            this.Controls.Add(numInterval);
            this.Controls.Add(chkAutoStart);
            this.Controls.Add(btnCleanNow);
            this.Controls.Add(lblStatus);

            timerMonitor = new System.Windows.Forms.Timer();
            timerMonitor.Interval = (int)numInterval.Value * 1000;
            timerMonitor.Tick += TimerMonitor_Tick;
            timerMonitor.Start();

            this.Load += delegate(object sender, EventArgs e)
            {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Hide();
            };
        }

        private void SetupTrayIcon()
        {
            contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Abrir Configuración", null, delegate(object sender, EventArgs e) { RestoreWindow(); });
            contextMenu.Items.Add("Limpiar RAM Ahora", null, delegate(object sender, EventArgs e) { ExecuteCleanSequence(); });
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Salir", null, delegate(object sender, EventArgs e) { ExitApp(); });

            notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "SysMemClean Activo",
                ContextMenuStrip = contextMenu
            };

            notifyIcon.DoubleClick += delegate(object sender, EventArgs e) { RestoreWindow(); };
        }

        private void TimerMonitor_Tick(object sender, EventArgs e)
        {
            float ramUsage = NativeMemoryOptimizer.GetSystemRamUsagePercentage();

            if (chkEnableThreshold.Checked)
            {
                if (ramUsage >= (float)numThreshold.Value)
                {
                    ExecuteCleanSequence();
                }
                else
                {
                    lblStatus.Text = string.Format("RAM Uso: {0}% (Por debajo del {1}%)", (int)ramUsage, numThreshold.Value);
                }
            }
            else
            {
                ExecuteCleanSequence();
            }
        }

        private void ExecuteCleanSequence()
        {
            try
            {
                NativeMemoryOptimizer.CleanAllSystemMemory();
                lblStatus.Text = string.Format("Última limpieza: {0:HH:mm:ss}", DateTime.Now);
                notifyIcon.ShowBalloonTip(2000, "SysMemClean", "Se han limpiado las listas de trabajo y standby de Windows.", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Error al purgar memoria: {0}", ex.Message), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                NativeMemoryOptimizer.TrimProcessMemory();
            }
        }

        private void SaveConfiguration()
        {
            try
            {
                string[] lines = {
                    string.Format("UseThreshold={0}", chkEnableThreshold.Checked),
                    string.Format("Threshold={0}", numThreshold.Value),
                    string.Format("Interval={0}", numInterval.Value)
                };
                File.WriteAllLines(configFile, lines);
            }
            catch { }
        }

        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(configFile))
                {
                    string[] lines = File.ReadAllLines(configFile);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            if (parts[0].Trim() == "UseThreshold")
                            {
                                bool useThresh;
                                if (bool.TryParse(parts[1].Trim(), out useThresh))
                                {
                                    chkEnableThreshold.Checked = useThresh;
                                    numThreshold.Enabled = useThresh;
                                }
                            }
                            else if (parts[0].Trim() == "Threshold")
                            {
                                decimal val;
                                if (decimal.TryParse(parts[1].Trim(), out val) && val >= numThreshold.Minimum && val <= numThreshold.Maximum)
                                    numThreshold.Value = val;
                            }
                            else if (parts[0].Trim() == "Interval")
                            {
                                decimal val;
                                if (decimal.TryParse(parts[1].Trim(), out val) && val >= numInterval.Minimum && val <= numInterval.Maximum)
                                {
                                    numInterval.Value = val;
                                    timerMonitor.Interval = (int)val * 1000;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
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

        private bool IsTaskScheduled()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("schtasks", string.Format("/query /tn \"{0}\"", TaskName))
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (Process p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit();
                    return p != null && p.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private void UpdateAutoStartTaskPath()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                string cmdArgs = string.Format("/create /tn \"{0}\" /tr \"\\\"{1}\\\"\" /sc onlogon /rl highest /f", TaskName, exePath);
                
                ProcessStartInfo psi = new ProcessStartInfo("schtasks", cmdArgs)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (Process p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit();
                }
            }
            catch { }
        }

        private void ToggleAutoStartTask(bool enable)
        {
            try
            {
                string exePath = Application.ExecutablePath;
                ProcessStartInfo psi;

                if (enable)
                {
                    // Crea/sobrescribe la tarea con la ruta exacta donde se encuentra el .exe actualmente
                    string cmdArgs = string.Format("/create /tn \"{0}\" /tr \"\\\"{1}\\\"\" /sc onlogon /rl highest /f", TaskName, exePath);
                    psi = new ProcessStartInfo("schtasks", cmdArgs)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (Process p = Process.Start(psi))
                    {
                        if (p != null) p.WaitForExit();
                    }

                    // Fuerza la ejecución inicial desde la tarea programada para verificar que arranca bien
                    ProcessStartInfo runPsi = new ProcessStartInfo("schtasks", string.Format("/run /tn \"{0}\"", TaskName))
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (Process pRun = Process.Start(runPsi))
                    {
                        if (pRun != null) pRun.WaitForExit();
                    }
                }
                else
                {
                    psi = new ProcessStartInfo("schtasks", string.Format("/delete /tn \"{0}\" /f", TaskName))
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (Process p = Process.Start(psi))
                    {
                        if (p != null) p.WaitForExit();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Error al configurar el inicio automático: {0}", ex.Message), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CheckAdministratorPrivileges()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    MessageBox.Show("Atención: Esta aplicación requiere derechos de Administrador para funcionar correctamente.", "Privilegios Requeridos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);
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
                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Privileges = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED }
                };
                AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }
}