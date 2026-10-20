# Privacy — Teams UIA Controls

Publisher: yazirupi. Applies to the plugin code in this repository.

The plugin runs locally. It reads Teams window metadata, meeting titles, button
labels and microphone/camera/sharing state through Windows UI Automation. Meeting
titles can appear in the Stream Deck action settings so you can select a meeting.
These values are held in memory and sent to Stream Deck's local settings interface.
The selected meeting identifier is saved using Stream Deck's settings storage.

For microphone/camera focus restoration, the helper temporarily checks foreground
window handles, process ownership and whether physical input occurred. Low-level
input notifications distinguish physical from injected events. Key values, typed
text and pointer positions are not stored or transmitted by this plugin.

The plugin does not implement analytics, cloud upload, recording, microphone audio
capture, camera capture, or collection of Teams passwords or OAuth tokens. Node and
the C# helper communicate through local standard input/output; the Stream Deck
connection uses a local WebSocket. This statement does not cover data independently
processed by Microsoft Teams, Elgato Stream Deck, Marketplace, GitHub, or the runtime.

Opening setup/support links uses your browser and the relevant site's policy.
If you open a public GitHub issue, its contents may be public. Do not include
confidential meeting information or credentials.

Remove configured keys/global settings or uninstall the plugin through Stream Deck
to remove its saved target configuration. Stream Deck may retain settings backups
under its own behavior. The developer does not operate a plugin data-collection
server from which meeting data could be deleted.

Questions: https://github.com/y2sys/Teams-UIA-Controls/issues
Use the issue tracker for non-sensitive questions; do not post personal data.
