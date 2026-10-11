# MacOSDock

A Windows desktop shell inspired by macOS Tahoe and the user's MyDockFinder layout. It has a side or bottom dock, a transparent Finder style menu bar, running app indicators, app pinning, drag and drop shortcuts, a full Launchpad, Control Center, and a tray menu. This is a new implementation; it does not bundle MyDockFinder executables or assets.

The Liquid Glass surfaces use a custom HLSL pixel shader in [`Shaders/LiquidGlass.hlsl`](Shaders/LiquidGlass.hlsl). It samples the desktop wallpaper behind each surface, blurs and bends the image at the edges, offsets color channels, and adds a highlight. The compiled shader is embedded in the executable, so users need no shader compiler or Windows acrylic API.

## Download and run

Open [Actions](https://github.com/cosmictaserdev-creator/MacOS/actions/workflows/windows.yml), open the latest successful **Windows portable build**, and download `MacOSDock-Windows-x64`. Extract the ZIP and run `MacOSDock.exe` on Windows 10/11 x64. Windows may show a SmartScreen prompt because the executable is unsigned. No .NET installation or build tool is needed on the PC.

## Use

- Click a dock icon to launch its app or bring its window forward.
- Drag a file, folder, or shortcut onto the dock, or use **Add app** in the dock menu.
- Right click a dock icon to pin or remove it, or open its file location.
- The Launchpad icon opens a searchable grid of installed Start Menu apps.
- The menu bar opens Control Center, search, and the running window list.
- Settings offers dock position, icon size, running apps, automatic hiding, and optional Windows taskbar hiding.
- **Import MyDockFinder layout** reads `ico.ini` and icon paths from a folder you choose. On a new install, `E:\MyDockFinder` is imported automatically when present. No personal paths or images are uploaded to this repository.
- The tray icon offers **Settings**, **Show dock**, **Show menu bar**, and **Exit**.
- Settings are saved under `%LOCALAPPDATA%\MacOSDock\settings.json`.

On PCs without a MyDockFinder installation, the dock starts with File Explorer, Settings, and Terminal shortcuts. Terminal uses the Windows `wt.exe` command; if Windows Terminal is unavailable, remove that shortcut and add another app. The app currently uses the primary monitor. The taskbar is hidden only when you enable that setting and is restored on exit.

## Building

GitHub Actions builds the self contained Windows executable automatically on pushes to `main`, pull requests, or manual dispatch. The project uses the .NET desktop SDK and no third party NuGet packages. A local build is optional:

```powershell
dotnet publish MacOSDock.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

The shader bytecode is committed at `Shaders/LiquidGlass.ps`. To regenerate it after changing the HLSL source on a Windows development machine, run `tools/CompileShader.ps1`. End users and GitHub Actions do not need this step.
