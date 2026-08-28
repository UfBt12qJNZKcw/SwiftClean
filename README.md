<h1 align="center">✦ SwiftClean</h1>

<p align="center">
  <b>A tiny, free, honest junk cleaner for Windows.</b><br>
  No ads. No bundles. No telemetry. Just cleaning.
</p>

<p align="center">
  <a href="https://github.com/UfBt12qJNZKcw/SwiftClean/releases/latest"><img alt="Download" src="https://img.shields.io/badge/download-latest-2ecc71"></a>
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%207%E2%80%9311-0f1220">
  <img alt="Runtime" src="https://img.shields.io/badge/.NET%20Framework-4.8-512bd4">
  <img alt="Size" src="https://img.shields.io/badge/size-~60%20KB-blue">
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/badge/license-MIT-green"></a>
  <a href="https://t.me/windows_free_software"><img alt="Telegram" src="https://img.shields.io/badge/Telegram-@windows__free__software-229ED9?logo=telegram&logoColor=white"></a>
</p>

<p align="center">
  <img src="docs/screenshot.png" width="620" alt="SwiftClean — scan results">
</p>

<p align="center">
  🌐 <b><a href="https://swiftcleanpc.com">swiftcleanpc.com</a></b>
  &nbsp;·&nbsp;
  💬 <b><a href="https://t.me/windows_free_software">Telegram — @windows_free_software</a></b>
  &nbsp;·&nbsp;
  <a href="https://sourceforge.net/projects/swiftclean/">SourceForge</a>
</p>

---

SwiftClean frees up disk space by removing the junk Windows and your browsers pile up
over time — temporary files, caches, crash dumps, the Recycle Bin. It's a single
**Signed MSI installer**: no background service, no bundles, no telemetry
(it uses the **.NET Framework 4.8** that ships with every modern Windows).

## Features

- 🧹 **Windows temp files** — `%TEMP%` and `C:\Windows\Temp`
- 🌐 **Browser caches** — Chrome, Edge, Yandex, Firefox, Opera, Brave *(cache only — never touches logins or passwords)*
- 🗑️ **Recycle Bin** — emptied via the native `SHEmptyRecycleBin` API
- 💥 **Windows Error Reporting** — leftover crash dumps
- 📦 **Windows Update leftovers** — the `SoftwareDistribution\Download` cache
- 🚀 **Startup manager** — see and disable autostart entries *(with a registry backup, so it's reversible)*
- 📁 **Custom folder** — point it at any folder and clean it

It shows the **real amount of junk found** before, and the **real space freed** after —
no fake "1000 registry errors detected" scare tactics.

## Download

**→ [Download the latest release](https://github.com/UfBt12qJNZKcw/SwiftClean/releases/latest)**

1. Download `SwiftClean.zip`
2. Extract it (right‑click → *Extract All*)
3. Unzip and run `SwiftClean.msi` to install, then launch **SwiftClean** — click **Scan**, then **Clean**

No installation. To remove it, just delete the file.

## Why SwiftClean?

Most "PC cleaners" are bloated, show fake problems to scare you, bundle extra software,
or phone home with your data. SwiftClean is the opposite:

| | SwiftClean |
|---|---|
| **Size** | Signed `.msi` installer |
| **Ads / bundles** | None |
| **Telemetry** | None — it makes no network connections at all |
| **Fake "errors"** | None — real sizes only |
| **Registry "optimization"** | None — it never edits the registry (except the reversible startup toggle) |
| **Source code** | Fully open — right here |

## Build from source

See **[BUILD.md](BUILD.md)**. In short:

```powershell
dotnet build src/SwiftClean.csproj -c Release
```

Output is a signed `SwiftClean.msi` installer.

## A note on antivirus false positives

SwiftClean is a small, **unsigned** .NET application that lists your browsers' cache
folders in order to clean them. A few machine‑learning antivirus engines flag *any*
new unsigned .NET binary that touches those folders as "suspicious" purely by heuristic —
the signature‑based engines (Microsoft Defender, Kaspersky, ESET, Bitdefender, Avast)
report it clean.

Because the **entire source is in this repository**, you can read exactly what it does
and build it yourself. Code signing is planned, which will remove the SmartScreen prompt.
If your browser blocks the download, it's a reputation warning for a new app — not a virus.

## More free Windows software

New releases, more free tools, guides and fixes go out in our Telegram channel:
**[@windows_free_software](https://t.me/windows_free_software)** — a hub of clean, free Windows utilities with official downloads via GitHub and SourceForge.

## License

[MIT](LICENSE) — do whatever you like with it.

---

<details>
<summary><b>🇷🇺 По‑русски</b></summary>

### SwiftClean — бесплатная честная очистка компьютера

Крошечная (~60 КБ) программа, которая освобождает место на диске, удаляя мусор,
который со временем копят Windows и браузеры: временные файлы, кэш, отчёты об ошибках,
Корзину. Без установщика и без рантайма — работает на .NET Framework 4.8, который уже
есть в каждой Windows.

**Что чистит:** временные файлы Windows, кэш браузеров (Chrome/Edge/Yandex/Firefox/Opera/Brave —
только кэш, логины и пароли не трогает), Корзину, отчёты об ошибках, хвосты обновлений Windows.
Плюс менеджер автозагрузки (с бэкапом — всё обратимо).

**Честно:** без рекламы, без «встроенного» софта, без телеметрии (программа вообще не выходит
в сеть), без фейкового «найдено 1000 ошибок». Показывает реальный размер мусора и реально
освобождённое место. Реестр не трогает.

**Скачать:** [последний релиз](https://github.com/UfBt12qJNZKcw/SwiftClean/releases/latest) →
`SwiftClean.zip` → распаковать → запустить `SwiftClean.msi` → «Сканировать», затем «Очистить».

**Больше бесплатного софта для Windows** — наш Telegram-канал
**[@windows_free_software](https://t.me/windows_free_software)**: новые релизы, чистые бесплатные утилиты, официальные загрузки через GitHub и SourceForge.

</details>
