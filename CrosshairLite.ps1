# Runs Crosshair Lite without a compiled exe (works with Smart App Control).
# Usage: powershell -STA -WindowStyle Hidden -ExecutionPolicy Bypass -File CrosshairLite.ps1 [-hidden]
param([switch]$hidden)
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$env:CROSSHAIR_ICON = Join-Path $dir 'icon.ico'
Add-Type -ReferencedAssemblies System.Windows.Forms, System.Drawing -TypeDefinition (Get-Content -Raw (Join-Path $dir 'Crosshair.cs'))
$t = [AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object { $_.GetType('Overlay') } | Where-Object { $_ } | Select-Object -First 1
[string[]]$sa = if ($hidden) { '-hidden' } else { @() }
$t.GetMethod('Main', [Reflection.BindingFlags]'NonPublic,Static').Invoke($null, [object[]]@(, $sa))
