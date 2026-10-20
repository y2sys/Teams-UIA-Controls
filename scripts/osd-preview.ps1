param([switch]$Preview)
$ErrorActionPreference = 'Stop'
$refs = @('PresentationFramework','PresentationCore','WindowsBase','System.Xaml','System') | ForEach-Object { [Reflection.Assembly]::LoadWithPartialName($_).Location }
$source = Get-Content (Join-Path $PSScriptRoot '..\src\MeetingOsd.cs') -Raw -Encoding UTF8
if ($Preview) { $source += Get-Content (Join-Path $PSScriptRoot '..\tests\OsdChecks.cs') -Raw -Encoding UTF8 }
Add-Type -TypeDefinition $source -ReferencedAssemblies $refs
Write-Output 'OSD compiled'
if ($Preview) {
    $imageDir = Join-Path $PSScriptRoot '..\.diagnostics'
    New-Item -ItemType Directory -Path $imageDir -Force | Out-Null
    [MeetingOsdChecks]::Run((Join-Path $imageDir 'osd-preview.png'))
    Write-Output 'OSD visible, centered, non-activating, click-through and auto-dismiss checks passed'
}
