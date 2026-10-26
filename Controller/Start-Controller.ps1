[CmdletBinding()]
param(
    [ValidateRange(-1,3)][int]$ControllerIndex = -1,
    [ValidateRange(0.0,0.9)][double]$Deadzone = 0.12,
    [ValidateRange(0,200)][int]$TriggerThreshold = 8,
    [ValidateRange(10,120)][int]$Hz = 120,
    [double]$Seconds = 0,
    [switch]$AllowUnfocused,
    [string]$OutputPath,
    [string]$ModPath
)
$ErrorActionPreference = 'Stop'
if (-not $OutputPath) {
    . (Join-Path $PSScriptRoot 'Resolve-InputPath.ps1')
    $OutputPath = Get-AnalogDriveInputPath -HelperRoot $PSScriptRoot -ModPath $ModPath
}
try {
    if (-not ('AnalogDrive.ControllerBridge' -as [type])) {
        Add-Type -Path (Join-Path $PSScriptRoot 'ControllerBridge.cs')
    }
    [AnalogDrive.ControllerBridge]::Run($OutputPath, $ControllerIndex, $Deadzone,
        $TriggerThreshold, $Hz, $Seconds, $AllowUnfocused.IsPresent)
} catch {
    Write-Error "Controller reader stopped: $($_.Exception.Message)"
    exit 1
}
