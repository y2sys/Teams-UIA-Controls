$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -Path (Join-Path $PSScriptRoot 'FocusReturnGuard.cs')
Add-Type -Path (Join-Path $PSScriptRoot 'TransitionNotices.cs')
$osdReferences = @('PresentationFramework','PresentationCore','WindowsBase','System.Xaml','System') | ForEach-Object { [Reflection.Assembly]::LoadWithPartialName($_).Location }
Add-Type -Path (Join-Path $PSScriptRoot 'MeetingOsd.cs') -ReferencedAssemblies $osdReferences
[MeetingOsd]::Start()
Add-Type -TypeDefinition @'
using System;
using System.Collections.Concurrent;
using System.Threading;
public static class MeetingInput {
    public static readonly ConcurrentQueue<string> Lines = new ConcurrentQueue<string>();
    public static readonly AutoResetEvent Ready = new AutoResetEvent(false);
    public static volatile bool Closed;
    public static void Start() {
        var thread = new Thread(() => {
            try { string line; while ((line = Console.ReadLine()) != null) { Lines.Enqueue(line); Ready.Set(); } }
            finally { Closed = true; Ready.Set(); }
        });
        thread.IsBackground = true;
        thread.Start();
    }
}
'@

$script:previous = @{}
$script:elements = @{}
$script:windows = @{}
$script:lastHealthy = $true
$script:refreshAt = @()
$script:focusGuard = $null
$script:notices = New-Object MeetingTransitionNotices
function Get-NoticeTime { return [long]([Diagnostics.Stopwatch]::GetTimestamp() * 1000.0 / [Diagnostics.Stopwatch]::Frequency) }
function Send-Record($value) { [Console]::WriteLine(($value | ConvertTo-Json -Depth 8 -Compress)) }
function Find-Control($window, [string]$id) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Read-Flag($element, $trueLabels, $falseLabels) {
    if ($null -eq $element) { return $null }
    $name = $element.Current.Name.Trim()
    if ($name -in $trueLabels) { return $true }
    if ($name -in $falseLabels) { return $false }
    return $null
}
function Can-Invoke($element) {
    if ($null -eq $element -or -not $element.Current.IsEnabled) { return $false }
    $pattern = $null
    return $element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)
}
function Can-Leave($element) {
    if (Can-Invoke $element) { return $true }
    if ($null -eq $element -or -not $element.Current.IsEnabled) { return $false }
    $pattern = $null
    return $element.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)
}
function Test-SharePicker($window) {
    $condition = New-Object System.Windows.Automation.OrCondition(
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'サウンドを含める')),
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Include sound')))
    return $null -ne $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Get-MeetingSnapshot {
    $rows = @()
    $next = @{}
    $script:elements = @{}
    $script:windows = @{}
    $script:lastHealthy = $true
    $pids = @(Get-Process -Name ms-teams -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    if ($pids.Count -eq 0) { $script:previous = @{}; return @() }
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    foreach ($window in $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)) {
        try {
            $info = $window.Current
            if ($info.ProcessId -notin $pids -or $info.ClassName -ne 'TeamsWebView') { continue }
            $id = 'teams:' + $info.ProcessId + ':' + $info.NativeWindowHandle
            $script:windows[$id] = $window
            $leave = Find-Control $window 'hangup-button'
            if ($null -ne $leave) {
                $mic = Find-Control $window 'microphone-button'
                $camera = Find-Control $window 'video-button'
                $share = Find-Control $window 'share-button'
                $muted = Read-Flag $mic @('マイクのミュートを解除', 'Unmute mic', 'Unmute microphone', 'Unmute') @('マイクのミュート', 'Mute mic', 'Mute microphone', 'Mute')
                $cameraOn = Read-Flag $camera @('カメラをオフにする', 'カメラをオフ', 'Turn camera off') @('カメラをオン', 'カメラをオンにする', 'Turn camera on')
                $sharing = Read-Flag $share @('共有を停止', '共有停止', 'Stop sharing') @('共有', 'Share', 'Share content')
                $row = [ordered]@{ id=$id; platform='teams'; title=$info.Name; inMeeting=$true; picker=$false; muted=$muted; cameraOn=$cameraOn; sharing=$sharing; capabilities=@{microphone=((Can-Invoke $mic) -and $null -ne $muted); camera=((Can-Invoke $camera) -and $null -ne $cameraOn); share=((Can-Invoke $share) -and $null -ne $sharing); leave=(Can-Leave $leave)} }
                $stable = $null
                if ($script:previous.ContainsKey($id)) { $stable = $script:previous[$id].stable }
                if ($null -ne $muted -and $null -ne $cameraOn -and $null -ne $sharing) { $stable = $row }
                elseif (Test-SharePicker $window) { $row.picker = $true; $row.lastKnown = $stable }
                $rows += $row
                $next[$id] = @{ row=$row; stable=$stable; seen=[DateTime]::UtcNow }
                $script:elements[$id] = @{microphone=$mic; camera=$camera; share=$share; leave=$leave}
            } elseif ($script:previous.ContainsKey($id)) {
                # A share picker hides the toolbar. Only an already detected meeting
                # can enter this state; ordinary settings windows cannot start calls.
                $isPicker = Test-SharePicker $window
                $old = $script:previous[$id]
                if ($isPicker -or ([DateTime]::UtcNow - $old.seen).TotalSeconds -lt 3) {
                    $row = [ordered]@{ id=$id; platform='teams'; title=$info.Name; inMeeting=$true; picker=$isPicker; transition=$true; lastKnown=$old.stable; muted=$null; cameraOn=$null; sharing=$null; capabilities=@{microphone=$false;camera=$false;share=$false;leave=$false} }
                    $rows += $row
                    $next[$id] = @{ row=$row; stable=$old.stable; seen=$old.seen }
                }
            }
        } catch { $script:lastHealthy = $false }
    }
    # The full and compact windows represent the same meeting. Prefer the full
    # window when both are exposed; never issue a command to every matching control.
    $full = @($rows | Where-Object { $_.title -notmatch '^(会議のコンパクト ビュー|Compact meeting view)\s*\|' })
    $rows = @($rows | Where-Object {
        $row = $_
        if ($row.title -notmatch '^(会議のコンパクト ビュー|Compact meeting view)\s*\|') { $true }
        else { @($full | Where-Object { $row.title.Contains($_.title) }).Count -eq 0 }
    })
    $script:previous = $next
    return $rows
}
function Publish-State {
    $sessions = @(Get-MeetingSnapshot)
    Send-Record @{ type='state'; sessions=$sessions; error=$(if ($script:lastHealthy) { '' } else { 'UI Automation read failed' }) }
    $now = Get-NoticeTime
    $script:notices.Prune($now)
    if ($script:lastHealthy) {
        foreach ($session in $sessions) {
            if ($session.picker -or $session.transition) { continue }
            foreach ($kind in @('microphone','camera')) {
                $observed = $(if ($kind -eq 'microphone') { $session.muted } else { $session.cameraOn })
                $notice = $script:notices.Observe($session.id, $kind, $observed, $now)
                if ($null -ne $notice) { [void][MeetingOsd]::Show($notice.Kind, $notice.State) }
            }
        }
    }
}
function Invoke-Command($command) {
    $script:focusGuard = $null
    if ($command.kind -notin @('microphone','camera','share','leave')) { throw 'Unsupported command' }
    $sessions = @(Get-MeetingSnapshot)
    $session = @($sessions | Where-Object { $_.id -eq $command.sessionId })
    if (-not $script:lastHealthy -or $session.Count -ne 1 -or $session[0].picker -or -not $session[0].capabilities[$command.kind]) { throw 'Meeting control unavailable' }
    $element = $script:elements[$command.sessionId][$command.kind]
    if ($command.kind -in @('microphone','camera')) {
        $script:focusGuard = [FocusReturnGuard]::Capture([uint32]$script:windows[$command.sessionId].Current.ProcessId)
    }
    try {
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke()
    } elseif ($command.kind -eq 'leave') {
        $expand = [System.Windows.Automation.ExpandCollapsePattern]$element.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
        $expand.Expand()
        Start-Sleep -Milliseconds 200
        $candidates = @()
        foreach ($item in $script:windows[$command.sessionId].FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
            $itemInfo = $item.Current
            if ($itemInfo.AutomationId -notin @('hangup-button','hangup-button-more-options') -and $itemInfo.Name -in @('退出','退出します','会議から退出','Leave','Leave meeting') -and (Can-Invoke $item)) { $candidates += $item }
        }
        if ($candidates.Count -ne 1) { try { $expand.Collapse() } catch {}; throw 'Unambiguous leave menu item not found' }
        $invoke = [System.Windows.Automation.InvokePattern]$candidates[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $invoke.Invoke()
    } else { throw 'Control cannot be invoked' }
    } finally {
        if ($null -ne $script:focusGuard -and -not $script:focusGuard.TryRestore()) { $script:focusGuard = $null }
    }
    if ($command.kind -in @('microphone','camera')) {
        $before = $(if ($command.kind -eq 'microphone') { $session[0].muted } else { $session[0].cameraOn })
        $script:notices.Begin($command.sessionId, $command.kind, $before, (Get-NoticeTime))
    }
    # Read immediately, then revisit asynchronously while Teams settles. Never
    # guess the new state or hold subsequent commands behind a fixed sleep.
    Publish-State
    Send-Record @{ type='result'; requestId=$command.requestId; ok=$true }
    $now = [DateTime]::UtcNow
    $script:refreshAt = @($now.AddMilliseconds(80), $now.AddMilliseconds(200), $now.AddMilliseconds(450))
}

[MeetingInput]::Start()
$nextPoll = [DateTime]::UtcNow
while (-not [MeetingInput]::Closed) {
    if ($null -ne $script:focusGuard -and -not $script:focusGuard.TryRestore()) { $script:focusGuard = $null }
    # Drain commands before periodic work. The input thread wakes this loop as
    # soon as a command arrives, including when it arrives during a UIA scan.
    $line = $null
    while ([MeetingInput]::Lines.TryDequeue([ref]$line)) {
        $command = $null
        try { $command = $line | ConvertFrom-Json; Invoke-Command $command }
        catch { Send-Record @{type='result'; requestId=$command.requestId; ok=$false; error='Meeting control unavailable'} }
    }
    $now = [DateTime]::UtcNow
    $due = @($script:refreshAt | Where-Object { $_ -le $now })
    if ($now -ge $nextPoll -or $due.Count -gt 0) {
        $script:refreshAt = @($script:refreshAt | Where-Object { $_ -gt $now })
        try { Publish-State } catch { Send-Record @{type='state'; sessions=@(); error='UI Automation unavailable'} }
        $nextPoll = [DateTime]::UtcNow.AddMilliseconds(750)
    }
    $wakeAt = $nextPoll
    foreach ($refresh in $script:refreshAt) { if ($refresh -lt $wakeAt) { $wakeAt = $refresh } }
    $wait = [Math]::Max(0, [Math]::Ceiling(($wakeAt - [DateTime]::UtcNow).TotalMilliseconds))
    [void][MeetingInput]::Ready.WaitOne([int]$wait)
}
