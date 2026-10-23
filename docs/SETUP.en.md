# Teams UIA Controls — Setup

Publisher: y2sys. Independent integration; not affiliated with Microsoft or Elgato.

## Requirements

- Windows 11, x64. ARM64 and macOS are not supported by this build.
- Stream Deck software 7.1 or later, with keypad actions.
- Microsoft Teams desktop app. Browser Teams is not supported.
- .NET 10 Desktop Runtime, Windows x64, installed separately:
  https://dotnet.microsoft.com/en-us/download/dotnet/10.0

Select **.NET Desktop Runtime**, **Windows x64**. The plain .NET Runtime,
ASP.NET Core Runtime and Windows' built-in .NET Framework are not substitutes.
The .NET SDK is not required to use this plugin. Restart Stream Deck after installing.

## Use

Install the plugin, then drag Microphone (マイク), Camera (カメラ), Screen Share
(画面共有), and Leave (退出) onto keys. The settings interface and on-screen notices
are Japanese. Start a Teams test meeting. One detected meeting is selected
automatically; select the target in action settings if several meetings are detected.
Target selection is shared across the four actions in the same plugin installation.

- Microphone toggles mute; camera toggles video.
- Screen Share opens Teams' source picker when idle and stops an active share.
- Leave exits the meeting for you; it does not select “End meeting for everyone”.
- Keys update from observed state; mic/camera changes confirmed after a plugin
  action show a brief central notice. Missing or uncertain state disables operations.

## Limitations and troubleshooting

Personal Teams accounts have been tested. Work/school accounts, other PCs and
English-language Teams sessions still need live validation. English state labels
are recognized in code; this is not a claim of completed English-language testing.
If no meeting is detected, check that Teams is the desktop app and that the
required runtime is installed. Run Stream Deck and Teams with ordinary user
permissions; do not elevate apps simply to bypass company policy.

During microphone/camera actions, Teams may briefly come to the foreground.
The helper attempts to restore the previous window, but will stop if the user
intervenes. UI changes in a Teams update can require a plugin update.

When Windows Remote Desktop Connection (mstsc) is foreground in full-screen mode,
all four actions share a preparation step that briefly focuses its connection bar
before operating Teams. A hidden bar is shown temporarily and hidden again;
full-screen, keyboard routing and pinning settings are not changed. A brief flash
can occur. Mic/camera restore the original RDP input focus; sharing keeps the picker
available. Physical input or a third-app switch cancels automated focus changes.
Unknown RDP clients or missing bars use the ordinary UIA path instead.

The Marketplace build uses a different identity from the old local development
build. Existing local keys do not automatically migrate. Avoid assigning both
versions to the same Multi Action or using both to send the same command.

Support: https://github.com/y2sys/Teams-UIA-Controls/issues
Provide plugin/Teams/Stream Deck versions and steps to reproduce. Remove meeting
titles, participant names, account details and confidential content from attachments.
