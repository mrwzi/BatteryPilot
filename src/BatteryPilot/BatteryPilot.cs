using System;
using System.Diagnostics;
using System.Drawing;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using Microsoft.Win32;

internal sealed class SaverForm : Form
{
    const int WM_POWERBROADCAST = 0x218, PBT_APMPOWERSTATUSCHANGE = 0xA, WM_DISPLAYCHANGE = 0x7E, WM_SETTINGCHANGE = 0x1A;
    readonly Label header = new Label(), summary = new Label(), health = new Label(), windowsHeader = new Label(), hardwareHeader = new Label();
    readonly Label silent = new Label(), gpu = new Label(), refresh = new Label(), energy = new Label(), cpu = new Label(), plan = new Label(), power = new Label();
    readonly RadioButton auto = new RadioButton(), battery = new RadioButton(), off = new RadioButton();
    readonly Button fix = new Button(), details = new Button(); readonly NotifyIcon tray = new NotifyIcon();
    bool busy, quietAttempted, ecoAttempted; string ecoReason=""; readonly IOemPowerProvider oem;

    public SaverForm() {
        Text = "BatteryPilot"; ClientSize = new Size(470, 535); FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(25, 27, 31);
        ForeColor = Color.White; StartPosition = FormStartPosition.CenterScreen;
        Add(header, 18, 15, 435, 42, 18, FontStyle.Bold); Add(summary, 18, 58, 435, 27, 11, FontStyle.Bold);
        auto.Text="AUTO  (apply on battery, restore on AC)"; battery.Text="BATTERY  (apply now)"; off.Text="OFF  (monitor only)";
        Add(auto, 20, 94, 430, 25); Add(battery, 20, 120, 430, 25); Add(off, 20, 146, 430, 25); auto.Checked=true;
        Add(windowsHeader, 20, 180, 430, 22, 10, FontStyle.Bold); Add(refresh, 20, 205, 430, 25); Add(energy, 20, 232, 430, 25); Add(cpu, 20, 259, 430, 25); Add(plan, 20, 286, 430, 25);
        Add(hardwareHeader, 20, 316, 430, 22, 10, FontStyle.Bold); Add(silent, 20, 341, 430, 25); Add(gpu, 20, 368, 430, 25); Add(power, 20, 397, 430, 25);
        fix.Text="TEST BATTERY MODE"; fix.FlatStyle=FlatStyle.Flat; fix.BackColor=Color.FromArgb(0,112,192); fix.ForeColor=Color.White;
        fix.Font=new Font(Font,FontStyle.Bold); Add(fix, 20, 430, 300, 43); details.Text="Details"; details.FlatStyle=FlatStyle.Flat; Add(details, 330, 430, 120, 43); Add(health, 20, 492, 430, 30, 9, FontStyle.Regular);
        fix.Click += delegate { if (battery.Checked || SystemInformation.PowerStatus.PowerLineStatus==PowerLineStatus.Offline) ApplyBattery(); else RestoreAc(); };
        auto.CheckedChanged += delegate { if (auto.Checked) EvaluateAuto(); };
        battery.CheckedChanged += delegate { if (battery.Checked) ApplyBattery(); };
        off.CheckedChanged += delegate { if (off.Checked) RefreshStatus(); };
        details.Click += delegate { ShowDisplayDetails(); };
        tray.Icon=SystemIcons.Information; tray.Text="BatteryPilot"; tray.Visible=true; tray.DoubleClick += delegate { Show(); WindowState=FormWindowState.Normal; Activate(); };
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Open", null, delegate { Show(); Activate(); });
        trayMenu.Items.Add("Apply battery mode", null, delegate { ApplyBattery(); });
        trayMenu.Items.Add("Exit", null, delegate { tray.Visible=false; Application.Exit(); });
        tray.ContextMenuStrip=trayMenu;
        oem=CreateOemProvider();
        RefreshStatus();
    }
    void Add(Control c,int x,int y,int w,int h,float size=10,FontStyle style=FontStyle.Regular) { c.SetBounds(x,y,w,h); c.ForeColor=Color.White; c.BackColor=BackColor; c.Font=new Font("Segoe UI",size,style); Controls.Add(c); }
    protected override void OnShown(EventArgs e) { base.OnShown(e); EvaluateAuto(); }
    protected override void OnFormClosing(FormClosingEventArgs e) { if(e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; Hide(); tray.ShowBalloonTip(1000,"BatteryPilot","Still monitoring power changes in the tray.",ToolTipIcon.Info); } else base.OnFormClosing(e); }
    protected override void WndProc(ref Message m) { base.WndProc(ref m); if(m.Msg==WM_POWERBROADCAST && m.WParam.ToInt32()==PBT_APMPOWERSTATUSCHANGE) OnPowerChanged(); if(m.Msg==WM_DISPLAYCHANGE || m.Msg==WM_SETTINGCHANGE) BeginInvoke((MethodInvoker)RefreshStatus); }
    void OnPowerChanged() { BeginInvoke((MethodInvoker)EvaluateAuto); }
    void EvaluateAuto() { if(!auto.Checked) { RefreshStatus(); return; } if(SystemInformation.PowerStatus.PowerLineStatus==PowerLineStatus.Offline) ApplyBattery(); else RestoreAc(); }
    void ApplyBattery() { if(busy || off.Checked) return; busy=true; summary.Text="Applying battery optimization…"; Application.DoEvents();
        quietAttempted=false; ecoAttempted=false; ecoReason="";
        if(oem!=null && oem.IsAvailable) { quietAttempted=oem.RequestQuietMode(); if(ExternalDisplayDetected()) ecoReason="Blocked: an external display is active"; else { ecoAttempted=oem.RequestGpuEco(); if(!ecoAttempted) ecoReason="Request failed"; for(int i=0;ecoAttempted && i<6 && NvidiaDetected();i++) Thread.Sleep(1000); if(ecoAttempted&&NvidiaDetected()) ecoReason="NVIDIA still active after timeout"; } }
        DisplayInfo display=GetActivePanel(); if(!BatteryProfileActive() && display.CurrentHz>0) SaveAcRefresh(display.CurrentHz); SetRefresh(display, display.LowestHz); SetBatteryProfileActive(true); EnableEnergySaver(); Run("powercfg", "/setactive SCHEME_BALANCED");
        Run("powercfg", "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 70"); Run("powercfg", "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 5"); Run("powercfg", "/setactive SCHEME_CURRENT");
        Thread.Sleep(1800); busy=false; RefreshStatus(); tray.ShowBalloonTip(1200,"Battery mode applied","Settings were applied and rechecked.",ToolTipIcon.Info);
    }
    void RestoreAc() { if(busy) return; busy=true; if(oem!=null&&oem.IsAvailable) oem.RequestNormalGpuMode(); quietAttempted=false; ecoAttempted=false; ecoReason=""; DisplayInfo display=GetActivePanel(); int target=AcDesired(display); SetRefresh(display,target); DisableEnergySaver(); Run("powercfg", "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100"); Run("powercfg", "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 5"); Run("powercfg", "/setactive SCHEME_CURRENT"); Thread.Sleep(800); DisplayInfo verified=GetActivePanel(); if(verified.CurrentHz==target) SetBatteryProfileActive(false); busy=false; RefreshStatus(); }
    void RefreshStatus() { bool ac=SystemInformation.PowerStatus.PowerLineStatus==PowerLineStatus.Online; int pct=(int)(SystemInformation.PowerStatus.BatteryLifePercent*100);
        bool effectiveBattery=battery.Checked || (auto.Checked&&!ac);
        header.Text=(off.Checked?"MONITOR ONLY — automatic switching disabled":(battery.Checked?"FORCED BATTERY MODE — "+(ac?"AC connected":"on battery"):"🔋 BatteryPilot"))+"                                      "+pct+"%"; DisplayInfo display=GetActivePanel(); int hz=display.CurrentHz; bool nvidia=NvidiaDetected();
        bool providerAvailable=oem!=null&&oem.IsAvailable; bool cpuLimit=CpuLimitIs70(); bool saver=EnergySaverOn(); bool balanced=IsBalancedPlan(); string activePlan=ActivePowerPlan();
        if(!effectiveBattery) {
            windowsHeader.Text="WINDOWS OPTIMIZATION — AC profile"; hardwareHeader.Text=(oem==null?"OEM":""+oem.Manufacturer)+" HARDWARE OPTIMIZATION";
            silent.Text="○ Quiet profile: not needed on AC"; silent.ForeColor=Color.LightGray;
            gpu.Text=nvidia?"✓ NVIDIA GPU available / enabled":"○ NVIDIA GPU unavailable"; gpu.ForeColor=nvidia?Color.LightGreen:Color.LightGray;
            int desired=AcDesired(display); bool match=hz>0 && hz==desired;
            refresh.Text=off.Checked?"• Display: "+hz+" Hz (informational)":(match?"✓ Display: "+hz+" Hz":"✕ Requested "+desired+" Hz, actual "+hz+" Hz"); refresh.ForeColor=off.Checked?Color.LightGray:(match?Color.LightGreen:Color.Salmon);
            energy.Text=!saver?"✓ Energy Saver: off":"✕ Energy Saver should be off"; energy.ForeColor=!saver?Color.LightGreen:Color.Salmon;
            cpu.Text=!cpuLimit?"✓ CPU: normal / unrestricted":"✕ CPU battery limit still active"; cpu.ForeColor=!cpuLimit?Color.LightGreen:Color.Salmon;
            plan.Text=balanced?"✓ Balanced power plan":"✕ Active power plan: "+activePlan; plan.ForeColor=balanced?Color.LightGreen:Color.Salmon;
            bool acHealthy=nvidia&&match&&!saver&&!cpuLimit&&balanced; power.Text="✓ Power: plugged in — idle in tray"; summary.Text=off.Checked?"MONITOR ONLY — settings are informational; no automatic changes":(acHealthy?"BATTERY OPTIMIZATION — waiting for unplug":"AC configuration needs attention"); fix.Text=off.Checked?"MONITORING ONLY":"TEST BATTERY MODE";
        } else {
            windowsHeader.Text="WINDOWS OPTIMIZATION";
            string providerName=oem==null?"OEM":oem.Manufacturer; hardwareHeader.Text=providerName+" HARDWARE OPTIMIZATION";
            if(!providerAvailable) { silent.Text="○ Silent profile: "+providerName+" controller unavailable"; silent.ForeColor=Color.LightGray; gpu.Text="○ GPU Eco: "+providerName+" controller unavailable — G-Helper unavailable"; gpu.ForeColor=Color.LightGray; hardwareHeader.Text+=" — unavailable"; }
            else { silent.Text=quietAttempted?"• Silent profile: requested (provider has no verification API)":"○ Silent profile: not requested"; silent.ForeColor=Color.Gold; bool blocked=ecoReason.StartsWith("Blocked:"); gpu.Text=!nvidia?"✓ GPU Eco: NVIDIA dGPU disabled":(blocked?"⚠ GPU Eco blocked — "+ecoReason:(!ecoAttempted?"✕ GPU Eco request failed":"✕ GPU Eco failed — "+NvidiaName()+" still enabled — "+ecoReason)); gpu.ForeColor=!nvidia?Color.LightGreen:(blocked?Color.Gold:Color.Salmon); hardwareHeader.Text+=" — "+(!nvidia?"1":"0")+"/2"; }
            bool match=hz>0 && hz==display.LowestHz;
            refresh.Text=off.Checked?"• Display: "+hz+" Hz (informational)":(match?"✓ Display: "+hz+" Hz — battery target":"✕ Requested "+display.LowestHz+" Hz, actual "+hz+" Hz"); refresh.ForeColor=off.Checked?Color.LightGray:(match?Color.LightGreen:Color.Salmon);
            energy.Text=saver?"✓ Energy Saver: enabled":"✕ Energy Saver: not enabled"; energy.ForeColor=saver?Color.LightGreen:Color.Salmon;
            cpu.Text=cpuLimit?"✓ CPU maximum: 70%":"✕ CPU maximum: not 70%"; cpu.ForeColor=cpuLimit?Color.LightGreen:Color.Salmon;
            plan.Text=balanced?"✓ Balanced power plan":"✕ Active power plan: "+activePlan; plan.ForeColor=balanced?Color.LightGreen:Color.Salmon; power.Text=ac?"✓ Physical power: AC connected":"✓ Physical power: on battery"; int score=(hz==display.LowestHz?1:0)+(saver?1:0)+(cpuLimit?1:0)+(balanced?1:0); windowsHeader.Text="WINDOWS OPTIMIZATION — "+score+"/4"; bool windowsOnlyDone=score==4&&!providerAvailable; summary.Text=off.Checked?"MONITOR ONLY — settings are informational; no automatic changes":(windowsOnlyDone?"Windows battery optimization is fully active. ASUS hardware controls unavailable.":"BATTERY OPTIMIZATION — "+score+"/4 Windows settings verified"); fix.Text=off.Checked?"MONITORING ONLY":(windowsOnlyDone?"WINDOWS OPTIMIZED ✓":"FIX BATTERY SETTINGS"); fix.Enabled=!off.Checked&&!windowsOnlyDone;
        }
        if(!effectiveBattery) fix.Enabled=!off.Checked;
        health.Text=BatteryHealth();
    }
    static string BatteryHealth() { try { double design=0,full=0; using(var s=new ManagementObjectSearcher("root\\WMI","SELECT * FROM BatteryStaticData")) foreach(ManagementObject o in s.Get()) design=Convert.ToDouble(o["DesignedCapacity"]); using(var s=new ManagementObjectSearcher("root\\WMI","SELECT * FROM BatteryFullChargedCapacity")) foreach(ManagementObject o in s.Get()) full=Convert.ToDouble(o["FullChargedCapacity"]); if(design>0&&full>0) return string.Format("Battery health: ~{0:0}%  ({1:0.0} Wh / {2:0.0} Wh)",full/design*100,full/1000,design/1000); } catch {} return "Battery health: unavailable"; }
    static bool NvidiaDetected() { try { using(var s=new ManagementObjectSearcher("SELECT Name,Status FROM Win32_VideoController")) foreach(ManagementObject o in s.Get()) if((o["Name"]+"").IndexOf("NVIDIA",StringComparison.OrdinalIgnoreCase)>=0 && (o["Status"]+"").Equals("OK",StringComparison.OrdinalIgnoreCase)) return true; } catch {} return false; }
    static string NvidiaName() { try { using(var s=new ManagementObjectSearcher("SELECT Name,Status FROM Win32_VideoController")) foreach(ManagementObject o in s.Get()) if((o["Name"]+"").IndexOf("NVIDIA",StringComparison.OrdinalIgnoreCase)>=0 && (o["Status"]+"").Equals("OK",StringComparison.OrdinalIgnoreCase)) return o["Name"]+""; } catch {} return "NVIDIA GPU"; }
    interface IOemPowerProvider { bool IsAvailable { get; } string Manufacturer { get; } bool SupportsQuietMode { get; } bool SupportsGpuEco { get; } bool RequestQuietMode(); bool RequestGpuEco(); bool RequestNormalGpuMode(); }
    sealed class AsusGHelperProvider : IOemPowerProvider { public string Manufacturer { get { return "ASUS"; } } public bool IsAvailable { get { return GHelperPath()!=null; } } public bool SupportsQuietMode { get { return IsAvailable; } } public bool SupportsGpuEco { get { return IsAvailable; } } public bool RequestQuietMode() { if(!EnsureGHelperRunning()) return false; SendChord(Keys.F16); return true; } public bool RequestGpuEco() { if(!EnsureGHelperRunning()) return false; SendChord(Keys.F14); return true; } public bool RequestNormalGpuMode() { if(!EnsureGHelperRunning()) return false; SendChord(Keys.F15); return true; } }
    static IOemPowerProvider CreateOemProvider() { return Manufacturer().IndexOf("ASUS",StringComparison.OrdinalIgnoreCase)>=0 ? new AsusGHelperProvider() : null; }
    static string Manufacturer() { try { using(var s=new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_ComputerSystem")) foreach(ManagementObject o in s.Get()) return o["Manufacturer"]+""; } catch {} return "Unknown"; }
    static string Model() { try { using(var s=new ManagementObjectSearcher("SELECT Model FROM Win32_ComputerSystem")) foreach(ManagementObject o in s.Get()) return o["Model"]+""; } catch {} return "Unknown"; }
    static string ActivePowerPlan() { string o=RunOutput("powercfg","/getactivescheme"); int a=o.IndexOf('('), b=o.IndexOf(')',a+1); return a>=0&&b>a?o.Substring(a+1,b-a-1):o.Trim(); }
    static bool IsBalancedPlan() { return RunOutput("powercfg","/getactivescheme").IndexOf("381b4222-f694-41f0-9685-ff5bb260df2e",StringComparison.OrdinalIgnoreCase)>=0; }
    static bool ExternalDisplayDetected() { return Screen.AllScreens.Length>1; }
    static bool IsGHelperRunning() { return Process.GetProcessesByName("GHelper").Length>0; }
    static string GHelperPath() { foreach(Process p in Process.GetProcessesByName("GHelper")) try { if(!string.IsNullOrEmpty(p.MainModule.FileName)&&File.Exists(p.MainModule.FileName)) return p.MainModule.FileName; } catch {} string app=Application.StartupPath, user=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); string[] paths={ Path.Combine(app,"GHelper.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"GHelper","GHelper.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"GHelper","GHelper.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GHelper","GHelper.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"GHelper","GHelper.exe"), Path.Combine(user,"GHelper","GHelper.exe"), Path.Combine(user,"Downloads","GHelper.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"GHelper.exe"), @"C:\GHelper\GHelper.exe", @"C:\Tools\GHelper\GHelper.exe" }; foreach(string path in paths) if(File.Exists(path)) return path; return null; }
    static bool EnsureGHelperRunning() { if(IsGHelperRunning()) return true; try { string path=GHelperPath(); if(path==null) return false; Process.Start(new ProcessStartInfo(path){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Minimized}); Thread.Sleep(1000); return IsGHelperRunning(); } catch { return false; } }
    static void EnableEnergySaver() { try { using(var k=Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Power\EnergySaver\EnergySaverPolicy")) k.SetValue("EnergySaverPolicy",1,Microsoft.Win32.RegistryValueKind.DWord); } catch {} }
    static void DisableEnergySaver() { try { using(var k=Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Power\EnergySaver\EnergySaverPolicy")) k.SetValue("EnergySaverPolicy",0,Microsoft.Win32.RegistryValueKind.DWord); } catch {} }
    static bool EnergySaverOn() { try { using(var k=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\EnergySaver\EnergySaverPolicy")) { object v=k==null?null:k.GetValue("EnergySaverPolicy"); return v is int && (int)v!=0; } } catch { return false; } }
    static bool CpuLimitIs70() { try { var o=RunOutput("powercfg","/q SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX"); var m=System.Text.RegularExpressions.Regex.Matches(o,"0x([0-9a-fA-F]+)"); if(m.Count==0) return false; return Convert.ToInt32(m[m.Count-1].Groups[1].Value,16)==70; } catch { return false; } }
    static void Run(string file,string args) { try { using(Process p=Process.Start(new ProcessStartInfo(file,args){CreateNoWindow=true,UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden})) p.WaitForExit(5000); } catch {} }
    static string RunOutput(string file,string args) { try { using(Process p=Process.Start(new ProcessStartInfo(file,args){CreateNoWindow=true,UseShellExecute=false,RedirectStandardOutput=true,WindowStyle=ProcessWindowStyle.Hidden})) { string s=p.StandardOutput.ReadToEnd(); p.WaitForExit(5000); return s; } } catch { return ""; } }
    static void SendChord(Keys key) { keybd_event(0x11,0,0,UIntPtr.Zero); keybd_event(0x10,0,0,UIntPtr.Zero); keybd_event(0x12,0,0,UIntPtr.Zero); keybd_event((byte)key,0,0,UIntPtr.Zero); keybd_event((byte)key,0,2,UIntPtr.Zero); keybd_event(0x12,0,2,UIntPtr.Zero); keybd_event(0x10,0,2,UIntPtr.Zero); keybd_event(0x11,0,2,UIntPtr.Zero); }
    [DllImport("user32.dll")] static extern void keybd_event(byte bVk,byte bScan,int flags,UIntPtr extra);
    sealed class DisplayInfo { public string DeviceName=""; public string FriendlyName=""; public int CurrentHz,LowestHz,HighestHz; public List<int> SupportedHz=new List<int>(); }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Auto)] struct DEVMODE { [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string dmDeviceName; public short dmSpecVersion,dmDriverVersion,dmSize,dmDriverExtra; public int dmFields; public int dmPositionX,dmPositionY,dmDisplayOrientation,dmDisplayFixedOutput; public short dmColor,dmDuplex,dmYResolution,dmTTOption,dmCollate; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string dmFormName; public short dmLogPixels; public int dmBitsPerPel,dmPelsWidth,dmPelsHeight,dmDisplayFlags,dmDisplayFrequency; }
    [StructLayout(LayoutKind.Sequential)] struct LUID { public uint LowPart; public int HighPart; }
    [StructLayout(LayoutKind.Sequential)] struct DISPLAYCONFIG_RATIONAL { public uint Numerator,Denominator; }
    [StructLayout(LayoutKind.Sequential)] struct DISPLAYCONFIG_PATH_SOURCE_INFO { public LUID adapterId; public uint id,modeInfoIdx,statusFlags; }
    [StructLayout(LayoutKind.Sequential)] struct DISPLAYCONFIG_PATH_TARGET_INFO { public LUID adapterId; public uint id,modeInfoIdx; public uint outputTechnology,rotation,scaling,scanLineOrdering; public DISPLAYCONFIG_RATIONAL refreshRate; public uint statusFlags; }
    [StructLayout(LayoutKind.Sequential)] struct DISPLAYCONFIG_PATH_INFO { public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo; public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo; public uint flags; }
    [StructLayout(LayoutKind.Sequential)] struct DISPLAYCONFIG_MODE_INFO { public uint infoType,id; public LUID adapterId; [MarshalAs(UnmanagedType.ByValArray,SizeConst=48)] public byte[] modeInfo; }
    [StructLayout(LayoutKind.Sequential)] struct DISPLAYCONFIG_DEVICE_INFO_HEADER { public uint type,size; public LUID adapterId; public uint id; }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct DISPLAYCONFIG_SOURCE_DEVICE_NAME { public DISPLAYCONFIG_DEVICE_INFO_HEADER header; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string viewGdiDeviceName; }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct DISPLAYCONFIG_TARGET_DEVICE_NAME { public DISPLAYCONFIG_DEVICE_INFO_HEADER header; public uint flags,outputTechnology,edidManufactureId,edidProductCodeId,connectorInstance; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)] public string monitorFriendlyDeviceName; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string monitorDevicePath; }
    [DllImport("user32.dll")] static extern int GetDisplayConfigBufferSizes(uint flags,out uint paths,out uint modes);
    [DllImport("user32.dll")] static extern int QueryDisplayConfig(uint flags,ref uint paths,[Out] DISPLAYCONFIG_PATH_INFO[] pathInfo,ref uint modes,[Out] DISPLAYCONFIG_MODE_INFO[] modeInfo,IntPtr topology);
    [DllImport("user32.dll")] static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME request);
    [DllImport("user32.dll")] static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME request);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern bool EnumDisplaySettings(string deviceName,int modeNum,ref DEVMODE devMode);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern int ChangeDisplaySettingsEx(string deviceName,ref DEVMODE devMode,IntPtr hwnd,int flags,IntPtr lparam);
    const uint QDC_ONLY_ACTIVE_PATHS=2, DISPLAYCONFIG_PATH_ACTIVE=1, DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME=1, DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME=2, ENUM_CURRENT_SETTINGS=unchecked((uint)-1), ENUM_REGISTRY_SETTINGS=unchecked((uint)-2), DM_DISPLAYFREQUENCY=0x400000;
    static DisplayInfo GetActivePanel() { var result=new DisplayInfo(); uint count,modes; if(GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS,out count,out modes)!=0 || count==0) return result; var paths=new DISPLAYCONFIG_PATH_INFO[count]; var infos=new DISPLAYCONFIG_MODE_INFO[modes]; if(QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS,ref count,paths,ref modes,infos,IntPtr.Zero)!=0) return result; DISPLAYCONFIG_PATH_INFO chosen=paths[0]; for(int i=0;i<count;i++) if((paths[i].flags&DISPLAYCONFIG_PATH_ACTIVE)!=0 && paths[i].targetInfo.outputTechnology==0x80000000) { chosen=paths[i]; break; }
        var src=new DISPLAYCONFIG_SOURCE_DEVICE_NAME(); src.header.type=DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME; src.header.size=(uint)Marshal.SizeOf(src); src.header.adapterId=chosen.sourceInfo.adapterId; src.header.id=chosen.sourceInfo.id; DisplayConfigGetDeviceInfo(ref src); result.DeviceName=src.viewGdiDeviceName??"";
        var target=new DISPLAYCONFIG_TARGET_DEVICE_NAME(); target.header.type=DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME; target.header.size=(uint)Marshal.SizeOf(target); target.header.adapterId=chosen.targetInfo.adapterId; target.header.id=chosen.targetInfo.id; DisplayConfigGetDeviceInfo(ref target); result.FriendlyName=string.IsNullOrWhiteSpace(target.monitorFriendlyDeviceName)?"Internal Display / Unknown":target.monitorFriendlyDeviceName;
        var d=new DEVMODE(); d.dmSize=(short)Marshal.SizeOf(d); if(EnumDisplaySettings(result.DeviceName,unchecked((int)ENUM_CURRENT_SETTINGS),ref d)) result.CurrentHz=d.dmDisplayFrequency; for(int i=0;;i++) { d=new DEVMODE(); d.dmSize=(short)Marshal.SizeOf(d); if(!EnumDisplaySettings(result.DeviceName,i,ref d)) break; if(d.dmDisplayFrequency>0&&!result.SupportedHz.Contains(d.dmDisplayFrequency)) result.SupportedHz.Add(d.dmDisplayFrequency); } result.SupportedHz.Sort(); if(result.SupportedHz.Count>0) { result.LowestHz=result.SupportedHz[0]; result.HighestHz=result.SupportedHz[result.SupportedHz.Count-1]; } return result; }
    static bool SetRefresh(DisplayInfo display,int target) { if(string.IsNullOrEmpty(display.DeviceName)||target<=0) return false; for(int i=0;;i++) { var d=new DEVMODE(); d.dmSize=(short)Marshal.SizeOf(d); if(!EnumDisplaySettings(display.DeviceName,i,ref d)) break; if(d.dmDisplayFrequency==target) { d.dmFields=(int)DM_DISPLAYFREQUENCY; return ChangeDisplaySettingsEx(display.DeviceName,ref d,IntPtr.Zero,0,IntPtr.Zero)==0; } } return false; }
    static void SaveAcRefresh(int hz) { if(hz>0) Registry.CurrentUser.CreateSubKey(@"Software\BatteryPilot").SetValue("SavedAcRefresh",hz,RegistryValueKind.DWord); }
    static int SavedAcRefresh() { try { object v=Registry.CurrentUser.OpenSubKey(@"Software\BatteryPilot")?.GetValue("SavedAcRefresh"); return v is int?(int)v:0; } catch{return 0;} }
    static bool BatteryProfileActive() { try { object v=Registry.CurrentUser.OpenSubKey(@"Software\BatteryPilot")?.GetValue("BatteryProfileActive"); return v is int && (int)v!=0; } catch{return false;} }
    static void SetBatteryProfileActive(bool active) { Registry.CurrentUser.CreateSubKey(@"Software\BatteryPilot").SetValue("BatteryProfileActive",active?1:0,RegistryValueKind.DWord); }
    static int AcDesired(DisplayInfo display) { int saved=SavedAcRefresh(); if(saved<=0 || !display.SupportedHz.Contains(saved) || (saved==display.LowestHz && display.CurrentHz>0 && display.CurrentHz!=display.LowestHz && !BatteryProfileActive())) { if(display.CurrentHz>0) { SaveAcRefresh(display.CurrentHz); return display.CurrentHz; } return display.HighestHz; } return saved; }
    void ShowDisplayDetails() { DisplayInfo d=GetActivePanel(); bool asus=Manufacturer().IndexOf("ASUS",StringComparison.OrdinalIgnoreCase)>=0, installed=GHelperPath()!=null, running=IsGHelperRunning(); string provider=oem==null?"None":oem.Manufacturer+(oem.IsAvailable?" (available)":" (unavailable)"); MessageBox.Show("Manufacturer: "+Manufacturer()+"\r\nModel: "+Model()+"\r\nWindows power control: supported\r\nDisplay control: "+(!string.IsNullOrEmpty(d.DeviceName)?"supported":"unavailable")+"\r\nASUS laptop detected: "+(asus?"yes":"no")+"\r\nASUS provider supported: "+(oem!=null?"yes":"no")+"\r\nG-Helper installed: "+(installed?"yes":"no")+"\r\nG-Helper running: "+(running?"yes":"no")+"\r\nOEM provider: "+provider+"\r\nQuiet mode capability: "+(oem!=null&&oem.SupportsQuietMode?"available":"unavailable")+"\r\nGPU Eco capability: "+(oem!=null&&oem.SupportsGpuEco?"available":"unavailable")+"\r\ndGPU detected: "+(NvidiaDetected()?"yes":"no")+"\r\nExternal displays detected: "+(Screen.AllScreens.Length>1?"yes":"no")+"\r\n\r\nActive display: "+d.DeviceName+"\r\nMonitor: "+d.FriendlyName+"\r\nCurrent refresh rate: "+d.CurrentHz+" Hz\r\nSupported refresh rates: "+string.Join(", ",d.SupportedHz)+" Hz\r\nSaved AC refresh rate: "+SavedAcRefresh()+" Hz\r\nBattery target refresh rate: "+d.LowestHz+" Hz", "Capability & display diagnostics",MessageBoxButtons.OK,MessageBoxIcon.Information); }
    [STAThread] static void Main(){ Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new SaverForm()); }
}

