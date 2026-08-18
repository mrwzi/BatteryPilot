using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class BatteryPilotSetup
{
    [STAThread]
    static void Main()
    {
        try
        {
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                throw new InvalidOperationException("Administrator permission is required.");
            string source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BatteryPilot.exe");
            string uninstallSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UNINSTALL BatteryPilot.exe");
            if (!File.Exists(source)) throw new FileNotFoundException("Keep BatteryPilot.exe beside the installer.");
            if (!File.Exists(uninstallSource)) throw new FileNotFoundException("Keep UNINSTALL BatteryPilot.exe beside the installer.");
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BatteryPilot");
            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, "BatteryPilot.exe");
            foreach (Process p in Process.GetProcessesByName("BatteryPilot")) try { p.Kill(); p.WaitForExit(3000); } catch { }
            File.Copy(source, target, true);
            string installedUninstaller = Path.Combine(folder, "UNINSTALL BatteryPilot.exe");
            File.Copy(uninstallSource, installedUninstaller, true);
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string command = "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('" + Path.Combine(desktop,"BatteryPilot.lnk").Replace("'","''") + "');$s.TargetPath='" + target.Replace("'","''") + "';$s.WorkingDirectory='" + folder.Replace("'","''") + "';$s.Description='BatteryPilot';$s.Save()";
            Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + command + "\"") { UseShellExecute = false, CreateNoWindow = true }).WaitForExit();
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\BatteryPilot"))
            {
                key.SetValue("DisplayName", "BatteryPilot"); key.SetValue("DisplayVersion", "0.1.0");
                key.SetValue("Publisher", "BatteryPilot contributors"); key.SetValue("DisplayIcon", target);
                key.SetValue("InstallLocation", folder); key.SetValue("UninstallString", "\"" + installedUninstaller + "\"");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", 256, RegistryValueKind.DWord);
            }
            Process.Start(target);
            MessageBox.Show("BatteryPilot is installed and running.", "BatteryPilot Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "BatteryPilot Setup", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
