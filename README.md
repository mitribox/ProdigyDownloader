# ProdigyDownloader

**ProdigyDownloader** is an open-source Windows desktop app by **[ProdigyNova](https://www.prodigynova.com)** for crawling websites and downloading images and videos with filters, concurrency control, scheduling, and organized storage.

**Version:** 1.1.0  
**License:** [GNU GPL v3](LICENSE) — Copyright © ProdigyNova ([www.prodigynova.com](https://www.prodigynova.com))  
**Repository:** [github.com/mitribox/ProdigyDownloader](https://github.com/mitribox/ProdigyDownloader)

## Download

Get the latest Windows build from **[Releases](https://github.com/mitribox/ProdigyDownloader/releases)**:

- **Installer** — `ProdigyDownloader-Setup-1.1.0.exe` (recommended)
- **Portable** — `ProdigyDownloader-win-x64.zip` (no install)

Requires Windows 10/11 (x64).

## Features

- Project wizard and full editable project settings
- Photos / videos / both with per-extension selection
- Optional minimum file size and image dimension filters
- **Exclude if** rules (file name, page title, tag, URL) with AND/OR
- Configurable simultaneous download connections
- Storage modes:
  - **Single folder** — all files in one directory
  - **By page title** — folder per page title for easier browsing
- Simple URL list or advanced URL regex filtering
- Scan options: entire-site crawl (same domain + sitemap), within starting folder, ignore home page, always scan image links
- Run modes: download once, schedule, or **Watch for new posts** (incremental checks + autodownload)
- Pause / resume / cancel active downloads; project context menu
- SQLite download archive (skip already-downloaded files)

## Screenshots

*(Add screenshots to `docs/screenshots/` and link them here after capture.)*

## Build from source

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download)

```powershell
git clone https://github.com/mitribox/ProdigyDownloader.git
cd ProdigyDownloader
dotnet build
dotnet run --project src/ClonerApp.App
```

### Publish (self-contained win-x64)

```powershell
dotnet publish src/ClonerApp.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win-x64
```

Output: `publish/win-x64/ProdigyDownloader.exe`

### Installer (optional)

1. Publish as above  
2. Install [Inno Setup](https://jrsoftware.org/isinfo.php)  
3. Compile `installer/ProdigyDownloader.iss`

## Data location

Application data and database:

`%LocalAppData%\ProdigyNova\ProdigyDownloader\`

## Notes

v1 crawls static HTML. JavaScript-heavy SPAs may not expose media in the initial markup.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

ProdigyDownloader is free software licensed under the **GNU General Public License v3.0**.  
Copyright © **ProdigyNova** — [www.prodigynova.com](https://www.prodigynova.com)

See [LICENSE](LICENSE) for the full terms.

## Links

- Website: [www.prodigynova.com](https://www.prodigynova.com)
- Issues: [GitHub Issues](https://github.com/mitribox/ProdigyDownloader/issues)
