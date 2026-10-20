param([Parameter(Mandatory=$true)][int]$Handle, [switch]$Invoke)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '..\.diagnostics\uia3\interop\lib\net45\Interop.UIAutomationClient.dll')
Add-Type -ReferencedAssemblies (Join-Path $PSScriptRoot '..\.diagnostics\uia3\interop\lib\net45\Interop.UIAutomationClient.dll') -TypeDefinition @'
using System;
using Interop.UIAutomationClient;
public sealed class NativeMeetingProbe {
    readonly IUIAutomation automation;
    readonly IUIAutomationElement window;
    readonly IUIAutomationElement button;
    public NativeMeetingProbe(int handle) {
        automation = new CUIAutomation8Class();
        window = automation.ElementFromHandle(new IntPtr(handle));
        button = window.FindFirst(TreeScope.TreeScope_Descendants, automation.CreatePropertyCondition(30011, "microphone-button"));
        if (button == null) throw new InvalidOperationException("Microphone control unavailable");
    }
    public int ProcessId { get { return window.CurrentProcessId; } }
    public string Name { get { return button.CurrentName; } }
    public string ProviderDescription { get { return button.CurrentProviderDescription; } }
    public bool HasLegacyAction { get { return button.GetCurrentPattern(10018) != null; } }
    public void Invoke() { ((IUIAutomationInvokePattern)button.GetCurrentPattern(10000)).Invoke(); }
}
'@
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
$probe = New-Object NativeMeetingProbe($Handle)
$teamsPid = $probe.ProcessId
if ((Get-Process -Id $teamsPid).ProcessName -ne 'ms-teams') { throw 'Target is not Teams' }
$beforeName = $probe.Name
$afterRead = Record-Front 'after-read'
Start-Sleep -Milliseconds 300
$beforeInvoke = Record-Front 'before-invoke'
$invoked = $false
if ($Invoke -and $script:steps[0].processId -ne $teamsPid -and $initial -eq $afterRead -and $initial -eq $beforeInvoke) {
    $probe.Invoke()
    $invoked = $true
    [void](Record-Front 'after-invoke')
    Start-Sleep -Milliseconds 80
    [void](Record-Front 'after-80ms')
    Start-Sleep -Milliseconds 250
    [void](Record-Front 'after-330ms')
}
[pscustomobject]@{backend='native-uia3';invoked=$invoked;provider=$probe.ProviderDescription;hasLegacyAction=$probe.HasLegacyAction;teamsProcessId=$teamsPid;beforeMic=$beforeName;afterMic=$probe.Name;steps=$script:steps} | ConvertTo-Json -Depth 4
