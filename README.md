# MacOSDock

A Windows desktop dock inspired by the behavior of MyDockFinder. It has a floating dock, a Finder style menu bar, running app indicators, app pinning, drag and drop shortcuts, and a tray menu. This is a new implementation; it does not include MyDockFinder executables or assets.

## Download and run

Open [Actions](https://github.com/cosmictaserdev-creator/MacOS/actions/workflows/windows.yml), open the latest successful **Windows portable build**, and download `MacOSDock-Windows-x64`. Extract the ZIP and run `MacOSDock.exe` on Windows 10/11 x64. Windows may show a SmartScreen prompt because the executable is unsigned. No .NET installation or build tool is needed on the PC.

## Use

- Click a dock icon to launch its app or bring its window forward.
- Drag an `.exe` or `.lnk` onto the dock, or use **Add app** in the dock menu.
- Right click a dock icon to pin or remove it, or open its file location.
- The tray icon offers **Settings**, **Show dock**, **Show menu bar**, and **Exit**.
- Settings are saved under `%LOCALAPPDATA%\MacOSDock\settings.json`.

The dock starts with File Explorer, Settings, and Terminal shortcuts. Terminal uses the Windows `wt.exe` command; if Windows Terminal is unavailable, remove that shortcut and add another app. The app currently uses the primary monitor. It does not replace Explorer or modify the Windows taskbar.

## Building

GitHub Actions builds the self contained Windows executable automatically on pushes to `main`, pull requests, or manual dispatch. The project uses the .NET desktop SDK and no third party NuGet packages. A local build is optional:

```powershell
dotnet publish MacOSDock.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```
