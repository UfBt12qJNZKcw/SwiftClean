# Building SwiftClean

SwiftClean is a single C# / WinForms application targeting **.NET Framework 4.8**.
The output is one framework‑dependent `SwiftClean.exe` (~60 KB) — no runtime is bundled,
because .NET Framework 4.8 ships with Windows 10/11.

## Requirements

- Windows
- One of:
  - **.NET SDK** (`dotnet` CLI), or
  - **Visual Studio** with the .NET desktop workload, or
  - the **.NET Framework compiler** `csc.exe` (already on every Windows install)

## Option 1 — dotnet SDK

```powershell
dotnet build src/SwiftClean.csproj -c Release
# → src\bin\Release\net48\SwiftClean.exe
```

## Option 2 — Visual Studio

Open `src/SwiftClean.csproj`, select the **Release** configuration, and build.

## Option 3 — csc.exe (no SDK needed)

The .NET Framework compiler is always present under `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\`:

```bat
cd src
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /codepage:65001 ^
  /target:winexe /out:SwiftClean.exe ^
  /win32manifest:app.manifest /win32icon:app.ico ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  Program.cs AssemblyInfo.cs
```

> `/codepage:65001` tells the compiler the sources are UTF‑8 (the UI strings are in Russian).

## Project layout

```
src/
  Program.cs        — the whole application (UI + cleaning logic)
  AssemblyInfo.cs   — assembly metadata / version
  app.manifest      — asInvoker execution level, per‑monitor DPI aware
  app.ico           — application icon
  SwiftClean.csproj — SDK‑style project (net48, WinForms)
```

## Code signing (optional)

The build works unsigned, but an unsigned new app triggers a one‑time SmartScreen
"Windows protected your PC → Run anyway" prompt. Signing removes it:

```powershell
signtool sign /fd SHA256 /a /tr http://timestamp.digicert.com /td SHA256 SwiftClean.exe
```
