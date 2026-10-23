# Full-screen Remote Desktop validation — 0.2.4

Teams runs locally; Windows Remote Desktop Connection (mstsc) is a foreground
client connected to a different PC. No RDP keyboard-routing or pinning settings
were changed. The hidden connection-bar path was tested on this machine.

## Implementation

The common helper command handler prepares all four actions with `RdpFocusScope`.
It validates the foreground mstsc executable, window class, caption style and
monitor coverage, then matches the bar by process and owner window. Unknown
clients or missing/ambiguous bars retain ordinary UIA behavior. Physical input,
held modifier keys, changed windows or an expired transition prevent unsafe focus
changes. A temporarily shown bar is hidden again. All input-thread attachments
are detached in `finally` blocks; no synthesized keystrokes are needed.

Mic/camera restore the original RDP window and input control and watch briefly for
delayed Teams activation. Share/leave retain their existing foreground behavior;
in particular, the share picker remains available for the user to choose a source.

## Checks performed

- 9 JavaScript checks and 41 C# behavior checks passed, including interruption,
  unknown clients, reused/invalid targets, hidden/visible bars, modifier keys,
  delayed activation and action-specific cleanup.
- Type checking, helper bridge smoke test and missing-runtime check passed.
- Official CLI validation passed for both development and Marketplace builds.
- Marketplace helper live checks through the JSON bridge:

| Operation | Result | Measurement |
|---|---|---|
| Mic toggle/restore, full-screen RDP | Both confirmed; RDP foreground restored | Request/result round trips 477 / 507 ms |
| Camera toggle/restore, full-screen RDP | Both confirmed; RDP foreground restored | Actual state observed after 692 / 760 ms |
| Open sharing picker, full-screen RDP | Picker confirmed through shared preparation | 780 ms |
| Leave disposable meeting | Exit confirmed | 730 ms; foreground was Teams, so the RDP-specific branch was not exercised for this live exit |

These are helper/bridge measurements, not end-to-end timing from a physical key.
The final update also monitors delayed Teams activation; this is covered by the
automated checks. A live call was no longer present after the exit check.

The final development build was loaded by restarting Stream Deck. Registration
reported 0.2.4.0 and the new helper process was running beneath that plugin's Node
process. Its helper file matched the final Marketplace candidate by SHA-256.
Existing development action identifiers and key assignments were retained.

## Remaining limits

Other mstsc versions, multi-monitor layouts, other RDP clients and clean-PC
installation need separate validation. Sharing an actual source and stopping it
were not repeated during this change; the picker was tested. Brief bar/Teams
flashes can occur. Native GUI calls can block on unresponsive applications; the
bridge watchdog remains in effect. This evidence does not imply Marketplace
approval or replace the requested demonstration video.
