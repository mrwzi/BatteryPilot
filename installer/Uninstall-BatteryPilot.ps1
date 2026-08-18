#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
$destination = 'C:\Program Files\BatteryPilot'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',$PSCommandPath)
    exit
}
Get-Process BatteryPilot -ErrorAction SilentlyContinue | Stop-Process -Force
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'BatteryPilot.lnk'
if (Test-Path -LiteralPath $desktopShortcut -PathType Leaf) { Remove-Item -LiteralPath $desktopShortcut -Force }
if (Test-Path -LiteralPath $destination -PathType Container) {
    $resolved = (Resolve-Path -LiteralPath $destination -ErrorAction Stop).Path
    if ($resolved -ne 'C:\Program Files\BatteryPilot') { throw "Unexpected uninstall path: $resolved" }
    $item = Get-Item -LiteralPath $resolved -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Refusing to remove a reparse point.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
