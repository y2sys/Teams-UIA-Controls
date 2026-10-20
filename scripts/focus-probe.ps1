param([Parameter(Mandatory=$true)][int]$Handle, [switch]$Invoke, [switch]$Guard)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
if ($Guard) { Add-Type -Path (Join-Path $PSScriptRoot '..\src\FocusReturnGuard.cs') }
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class MeetingFocusProbe {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
}
'@
$clock = [Diagnostics.Stopwatch]::StartNew()
$script:steps = @()
function Record-Front([string]$phase) {
    $front = [MeetingFocusProbe]::GetForegroundWindow()
    [uint32]$owner = 0
    [void][MeetingFocusProbe]::GetWindowThreadProcessId($front, [ref]$owner)
    $script:steps += [pscustomobject]@{phase=$phase;elapsedMs=$clock.ElapsedMilliseconds;handle=$front.ToInt64();processId=$owner}
    return $front
}
$initial = Record-Front 'before-read'
$window = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$Handle)
$teamsPid = $window.Current.ProcessId
if ((Get-Process -Id $teamsPid).ProcessName -ne 'ms-teams') { throw 'Target is not Teams' }
$condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'microphone-button')
$button = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
if ($null -eq $button) { throw 'Microphone control unavailable' }
$beforeName = $button.Current.Name
$afterRead = Record-Front 'after-read'
Start-Sleep -Milliseconds 300
$beforeInvoke = Record-Front 'before-invoke'
$invoked = $false
$guardInstance = $null
if ($Invoke -and $script:steps[0].processId -ne $teamsPid -and $initial -eq $afterRead -and $initial -eq $beforeInvoke) {
    # Intentionally the complete action path: no SetFocus, window activation,
    # keyboard/mouse injection, UI restoration, bridge or Stream Deck callbacks.
    $pattern = [System.Windows.Automation.InvokePattern]$button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    if ($Guard) { $guardInstance = [FocusReturnGuard]::Capture([uint32]$teamsPid) }
    $pattern.Invoke()
    $invoked = $true
    [void](Record-Front 'after-invoke')
    if ($null -ne $guardInstance) { [void]$guardInstance.TryRestore(); [void](Record-Front 'after-guard') }
    Start-Sleep -Milliseconds 80
    if ($null -ne $guardInstance) { [void]$guardInstance.TryRestore() }
    [void](Record-Front 'after-80ms')
    Start-Sleep -Milliseconds 250
    if ($null -ne $guardInstance) { [void]$guardInstance.TryRestore() }
    [void](Record-Front 'after-330ms')
}
[pscustomobject]@{invoked=$invoked;guardOutcome=$(if ($null -ne $guardInstance) { $guardInstance.Outcome } else { 'NotArmed' });physicalInputVersion=$(if ($Guard) { [MeetingPhysicalInput]::Version } else { $null });injectedInputCount=$(if ($Guard) { [MeetingPhysicalInput]::InjectedCount } else { $null });teamsProcessId=$teamsPid;beforeMic=$beforeName;afterMic=$button.Current.Name;steps=$script:steps} | ConvertTo-Json -Depth 4
