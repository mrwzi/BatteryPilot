using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class BatteryPilotSetup
{
    [STAThread]
    private static void Main()
    {
        try
        {
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                throw new InvalidOperationException("Administrator permission is required.");

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BatteryPilot");
            string app = Path.Combine(folder, "BatteryPilot.exe");
            string uninstaller = Path.Combine(folder, "UNINSTALL BatteryPilot.exe");

            foreach (Process process in Process.GetProcessesByName("BatteryPilot"))
                try { process.Kill(); process.WaitForExit(3000); } catch { }

            Directory.CreateDirectory(folder);
            Extract("BatteryPilot.Payload.App", app);
            Extract("BatteryPilot.Payload.Uninstaller", uninstaller);

            string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "BatteryPilot.lnk");
            string command = "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('" + shortcut.Replace("'", "''") +
                "');$s.TargetPath='" + app.Replace("'", "''") + "';$s.WorkingDirectory='" + folder.Replace("'", "''") +
                "';$s.Description='BatteryPilot';$s.Save()";
            Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + command + "\"")
            { UseShellExecute = false, CreateNoWindow = true }).WaitForExit();

            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\BatteryPilot"))
            {
                key.SetValue("DisplayName", "BatteryPilot");
                key.SetValue("DisplayVersion", "0.1.0");
                key.SetValue("Publisher", "BatteryPilot contributors");
                key.SetValue("DisplayIcon", app);
                key.SetValue("InstallLocation", folder);
                key.SetValue("UninstallString", "\"" + uninstaller + "\"");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", 256, RegistryValueKind.DWord);
            }

            Process.Start(app);
            MessageBox.Show("BatteryPilot is installed and running.", "BatteryPilot Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "BatteryPilot Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void Extract(string resourceName, string destination)
    {
        using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
        {
            if (input == null) throw new InvalidOperationException("The installer payload is missing: " + resourceName);
            using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None)) input.CopyTo(output);
        }
    }
}
