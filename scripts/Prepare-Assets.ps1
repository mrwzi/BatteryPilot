$ErrorActionPreference = 'Stop'
$encoded = Join-Path $PSScriptRoot '..\assets\BatteryPilot.ico.b64'
$icon = Join-Path $PSScriptRoot '..\src\BatteryPilot\BatteryPilot.ico'
if (-not (Test-Path -LiteralPath $encoded -PathType Leaf)) { throw "Missing icon source: $encoded" }
[IO.File]::WriteAllBytes($icon, [Convert]::FromBase64String((Get-Content -Raw -LiteralPath $encoded)))
