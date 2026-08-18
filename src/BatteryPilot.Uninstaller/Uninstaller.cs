using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class BatteryPilotUninstall
{
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool MoveFileEx(string existing, string destination, int flags);
    const int MOVEFILE_DELAY_UNTIL_REBOOT = 4;
    static readonly string InstallFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BatteryPilot");

    [STAThread] static void Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "--cleanup") { Cleanup(args[1], args[2]); return; }
        try
        {
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) throw new InvalidOperationException("Administrator permission is required.");
            if (MessageBox.Show("Remove BatteryPilot from this computer?", "Uninstall BatteryPilot", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (Process p in Process.GetProcessesByName("BatteryPilot")) try { p.Kill(); p.WaitForExit(3000); } catch { }
            string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "BatteryPilot.lnk");
            if (File.Exists(shortcut)) File.Delete(shortcut);
            Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\BatteryPilot", false);
            string helper = Path.Combine(Path.GetTempPath(), "BatteryPilot-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(Application.ExecutablePath, helper, true);
            Process.Start(new ProcessStartInfo(helper, "--cleanup \"" + InstallFolder + "\" " + Process.GetCurrentProcess().Id) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "BatteryPilot Uninstall", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    static void Cleanup(string folder, string parentId)
    {
        try
        {
            int pid; if (int.TryParse(parentId, out pid)) try { Process.GetProcessById(pid).WaitForExit(10000); } catch { }
            string expected = Path.GetFullPath(InstallFolder).TrimEnd(Path.DirectorySeparatorChar);
            string actual = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected uninstall path.");
            for (int i=0;i<20 && Directory.Exists(actual);i++) { try { Directory.Delete(actual, true); } catch (IOException) { Thread.Sleep(250); } catch (UnauthorizedAccessException) { Thread.Sleep(250); } }
            if (Directory.Exists(actual)) throw new IOException("The BatteryPilot program folder could not be removed.");
            MoveFileEx(Application.ExecutablePath, null, MOVEFILE_DELAY_UNTIL_REBOOT);
            MessageBox.Show("BatteryPilot was completely uninstalled.", "BatteryPilot", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "BatteryPilot Uninstall", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
