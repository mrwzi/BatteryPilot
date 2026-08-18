#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'BatteryPilot.exe'
$destination = 'C:\Program Files\BatteryPilot'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "BatteryPilot.exe was not found beside this installer." }
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',$PSCommandPath)
    exit
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $destination 'BatteryPilot.exe') -Force
$desktop = [Environment]::GetFolderPath('Desktop')
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $desktop 'BatteryPilot.lnk'))
$shortcut.TargetPath = Join-Path $destination 'BatteryPilot.exe'
$shortcut.WorkingDirectory = $destination
$shortcut.Description = 'BatteryPilot'
$shortcut.Save()
Start-Process (Join-Path $destination 'BatteryPilot.exe')
