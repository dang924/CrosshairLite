# Runs Crosshair Lite without a compiled exe (works with Smart App Control).
# Usage: powershell -STA -ExecutionPolicy Bypass -File CrosshairLite.ps1 [-hidden]
param([switch]$hidden)
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$env:CROSSHAIR_ICON = Join-Path $dir 'icon.ico'
$env:CROSSHAIR_SCRIPT = $MyInvocation.MyCommand.Path
Add-Type -ReferencedAssemblies System.Windows.Forms, System.Drawing -TypeDefinition (Get-Content -Raw (Join-Path $dir 'Crosshair.cs'))
$t = [AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object { $_.GetType('Overlay') } | Where-Object { $_ } | Select-Object -First 1
$t.GetMethod('Run', [Reflection.BindingFlags]'NonPublic,Static').Invoke($null, @([bool]$hidden))
