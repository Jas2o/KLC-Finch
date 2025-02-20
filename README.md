# KLC-Finch 
Finch is an alternative frontend to Kaseya Live Connect (which is required to be installed to use it) written in C#. It was functional up to VSA 9.5.20 however will not receive any further VSA testing/development.

The main reason this exists is because years ago with Live Connect there was issues with keys getting stuck down and clipboard data leaking to endpoints even after closing the remote control window.

## Usage
Typically, KLC-Finch is launched by KLC-Proxy rather than directly.

![Screenshot of KLC-Finch](/Resources/KLC-Finch-Test.png?raw=true)

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
