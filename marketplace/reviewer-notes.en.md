# Reviewer notes — Teams UIA Controls

Publisher: y2sys. Free submission candidate, version 0.2.4.
Plugin UUID: com.y2sys.teams-uia-controls.

## How to review

Install .NET 10 Desktop Runtime for Windows x64 first. Start desktop Microsoft Teams
and join a disposable test meeting. Add the four Japanese-labelled actions to keys.
Microphone is マイク, camera is カメラ, screen share is 画面共有, leave is 退出.
Toggle mic and camera and verify actual Teams state plus key feedback. Share opens
Teams' source picker; the user chooses a source. Press again while sharing to stop.
Leave is intended to leave only the current user, not end the meeting for everyone.

The helper performs UI Automation locally. It does not use the retired Teams
third-party app API or require Teams OAuth. Calls may be unavailable when the UIA
state is unknown, Teams policies prevent an action, or another UI dialog intervenes.
Mic/camera actions may briefly foreground Teams; the helper attempts to restore
the previous foreground app without overriding subsequent user input.

All four controls use shared preparation when the exact foreground window is a
full-screen Windows mstsc session. Its same-process, owner-matched connection bar
is temporarily focused; hidden bars are temporarily shown and then hidden again.
No RDP settings or keyboard shortcuts are changed. Visible flashes can occur.
Other RDP clients/unknown bar structures retain the ordinary UIA path. Physical
input or a third-app switch cancels the prepared action. Sharing keeps the picker
foreground; mic/camera restore the original RDP window and input focus.

The English Marketplace description is provided for review. The plugin interface
and on-screen notices are Japanese. This release does not claim a localized English
interface. Labels for English Teams are recognized, but English live sessions have
not been verified.

## Evidence and limits

Earlier local builds were tested by the developer with a personal Teams account
and Stream Deck Neo. Automated checks cover call selection, uncertain state,
sharing-picker retention, focus-return conditions and notice conditions. Helper
startup/communication and package validation have also been tested. These checks
are not evidence of a clean-PC installation or work/school account compatibility.

The 0.2.4 helper was additionally tested through its JSON bridge with full-screen
mstsc foreground: microphone/camera toggles and the sharing picker succeeded.
The live exit check succeeded with Teams foreground. See docs/RDP-VALIDATION.md
for timings and limits; a live full-screen-RDP exit and actual source sharing/stop
were not repeated. Final automated checks cover the delayed-activation guard.

Before submission: complete the live candidate-package test and attach a short
demonstration video. At document creation the video and clean-PC test are pending.
Do not submit these notes with pending checks described as complete.

## Privacy and dependencies

Meeting titles and control states are read locally for target selection. Physical
input occurrence is observed only to cancel focus restoration; input contents are
not recorded. See the privacy document for storage and third-party boundaries.
The separately installed Desktop Runtime is not bundled. Runtime JavaScript
dependency license texts are included in THIRD-PARTY-NOTICES.txt.

## AI assistance disclosure — confirm with the publisher

AI assistance was used to write and revise the implementation, documentation and
programmatically generated promotional graphics. The human developer supplied the
requirements and product decisions, and performed interactive device testing.
No claim of a completed independent human code review is made. Promotional images
are diagrams generated from the actual plugin icons, not hardware photographs or
live screenshots. Elgato should assess eligibility under its current AI-usage policy;
approval has not been obtained or implied.
