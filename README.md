# KLC-Finch 
Finch is an alternative frontend to Kaseya Live Connect (which is required to be installed to use it) written in C#. It was functional up to VSA 9.5.20 however will not receive any further VSA testing/development.

The main reason this exists is because years ago with Live Connect there was issues with keys getting stuck down and clipboard data leaking to endpoints even after closing the remote control window. Kaseya did eventually fix those issues in Live Connect, however by then Finch had additional enhancements that made returning to Live Connect difficult.

## Usage
Typically, KLC-Finch is launched by KLC-Proxy rather than directly.

![Screenshot of KLC-Finch](/Resources/KLC-Finch-Test.png?raw=true)

## Differences from Live Connect
A big advantage Finch has over Live Connect is that the Remote Control interface can show more than one screen and allows support technicians to rapidly switch between the endpoint's screens while assisting. While only one screen is only ever active for getting frame updates (limitation of how Live Connect works), the others will display a ghost of their last frame. This leads to some workflow differences to either speed up the technician's assistance or reduce disruption to the endpoint user.

Other useful functions of the Live Connect app (accessed from VSA by hovering an agent and pressing "Live Connect") are replicated in Finch within "Alternative Window". If you start a session from VSA intended for RC only, you can always bring up Alternative window later (which you cannot do in Live Connect when using RC only).

### Remote Control: Screens
- The screen switcher and overview display reflects the endpoint user's screen positioning.
  - If the user says "can you see my left screen?" you can quickly tell exactly which one that is.
- Press F1 to quickly disable your interactions and change to overview display. Pressing F2 or double-clicking will enable interactions.
  - While your interactions is disabled there are visual reminders: overlay message, inactive screens will be colour tinted, and background colour of the void will change.
  - Useful for when the endpoint user wants to show you something on a different screen but you don't want to disrupt them by you moving your mouse up to your switch screens toolbar button.
- While in overview, moving your mouse over screens will set them as the active screen to get frame updates; scanning your mouse over all the screens will quickly get frame updates for all of them. Clicking screens will focus the view on it.
- Screens on the edge of the current active screen can also be clicked to switch to them. Being able to see screen edges helps with dragging windows between screens without using the toolbar's screen switcher.
- Connecting to a Mac endpoint will add a gap to the bottom of the active screen to make it easier to access a Mac's dock that has been set to automatically hide at the bottom.
  - This is next to impossible when using Live Connect's Remote Control on a technician device with a smaller screen resolution than the Mac endpoint.
- PrtScr (or button in FPS/Latency menu) will capture the active screen's next frame and place on clipboard.
  - The intended use case for this is documenting an IT process without compromising visual fidelity, as the frame will not have been scaled up/down for your display or require cropping edges of the remote control app.

### Remote Control: other enhancements
- An alternative to "Paste Clipboard" exists to perform the keystrokes instead (Autotype).
  - Shortcuts for Autotype include: middle mouse button, Ctrl+Shift+V, and Ctrl+`.
  - Very useful for if you remote control an endpoint to access VMWare ESXi, open a guest VM's web console and then need to enter a long complex password (which "Paste Clipboard" can not do).
- When using either Paste Clipboard or Autotype there is a prompt if the clipboard is over 50 characters, and will not fire if there's a newline character within.
- Clipboard sync between local and endpoint can be set to: Off, Enabled, and Enabled only for Windows Server endpoints or last user logged in as default Administrator (however there is currently no way to specify other account names).
- Setting "Mac: Safe keys only" to prevent keys that cause Mac endpoint's Fn key to get stuck down (unfortunately makes using Terminal difficult as this includes arrow keys, home, end, delete).

### Alternative Window
- Display if the agent has any VSA badge/notes or remote control policy.
- Shortcuts for checking endpoint's VSA remote control logs:
  - In Alternative window, on the Dashboard tab, press RC Logs.
  - In Remote Control, press the toolbar FPS/Latency button then Remote Control Logs.
- Command Prompt/PowerShell/Terminal have a button to refresh the textbox with increased scrollback.
- Files - should match everything you'd want to do that Live Connect did, and also jump directly to entered paths.
- Registry - tries to be more like Windows Registry Editor than whatever Live Connect's was.
- Events - tries to be more like Windows Event Viewer, you can even access log types Live Connect can't and filter what's been loaded by text/info/warn/error.
- Services - tries to be more like Windows Services, you can even filter by text or anything that's set to Auto but Not Running.
- Processes - tries to be more like WIndows's Task Manager, you can even filter by text. When changing selection you need to press Safety before you can press End Task.
- Toolbox - Basically the same as Live Connect's.

## Required other repos to build
- LibKaseya
- LibKaseyaAuth
- LibKaseyaLiveConnect
- VP8.NET (modified)

## Required packages to build
- CredentialManagement.Standard
- Fleck
- Newtonsoft.Json
- nucs.JsonSettings
- Ookii.Dialogs.Wpf
- OpenTK, GLControl and GLWpfControl (GLControl while not WPF was better for hardware and workflows I was using in the past)
- RestSharp
- VtNetCore (this is used for the CMD/PowerShell/Mac Terminal interfaces)
