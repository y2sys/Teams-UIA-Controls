$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '..\src\TransitionNotices.cs')
$tracker = New-Object MeetingTransitionNotices
function Check($condition, $message) { if (-not $condition) { throw $message } }
Check ($null -eq $tracker.Observe('a','microphone',$true,0)) 'Initial state must not show OSD'
$tracker.Begin('a','microphone',$true,100)
Check ($null -eq $tracker.Observe('a','microphone',$true,120)) 'Unchanged state must not show OSD'
Check ($null -eq $tracker.Observe('a','microphone',$null,140)) 'Unknown state must not show OSD'
Check ($null -eq $tracker.Observe('b','microphone',$false,150)) 'Another session cannot confirm a command'
$notice = $tracker.Observe('a','microphone',$false,160)
Check ($null -ne $notice -and -not $notice.State) 'Actual unmute must show confirmed state'
Check ($null -eq $tracker.Observe('a','microphone',$false,180)) 'Confirmation must only show once'
$tracker.Begin('a','camera',$false,200)
Check ($null -eq $tracker.Observe('a','camera',$true,3201)) 'Late unrelated state must not show OSD'
$tracker.Begin('a','share',$false,3300)
Check ($null -eq $tracker.Observe('a','share',$true,3400)) 'Sharing must not show microphone/camera OSD'
Write-Output 'Transition notice behavior checks passed'
