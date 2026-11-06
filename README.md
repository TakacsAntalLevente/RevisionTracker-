# RevisionTracker 0.1 (MVP, free edition)

A Windows desktop app that keeps every version of your documents, organised the way the Windchill tracking Excel was:
**Project → Board (PCBA / PCB) → Document (Schematic, eBOM, Gerber…) → Versions**.

## What it does
- **Projects and boards**: one project per release or change (e.g. `DTNA MCO1`), with boards that each have a TTN, REV and Variant.
- **Standard documents**: adding a board creates its usual documents automatically (PCBA: 3D, FabDraw, Schematic, eBOM, Pick&Place; PCB: FabDraw, Gerber, ODB, Netlist). They come pre-filled with WC type, WC name and file-name suffix.
- **Standard file names**: generated the same way as the Excel formulas, e.g. `Schematic_LogicPCBA_C2_VAR04_0127Y05196-02_e16.07-01`. Right-click a document and choose *Copy file name*.
- **Save new version**: pick a file or drag it onto a document. Each version stores REV, TAG, a label ("Sent to client", "Approved"…) and a note on what changed. You're told if the file is identical to an earlier version.
- **Timeline**: open any old version (as a read-only copy), save a copy under its standard name, or restore it. Before restoring, any unsaved changes in the current file are saved as a new version first.
- **Export to Excel**: writes a CSV with the same columns as the old sheet (Filename, TTN, REV, TAG, file, WC, WC type, WC name), plus version info.
- **Categories** (Tools → Categories): edit document types, prefixes, default WC types, suffixes, boards and labels.
- **Free edition limit**: 25 versions per document (`Core/Edition.cs`).

## Where data lives
`%LOCALAPPDATA%\RevisionTracker\`
- `catalog.xml`: projects, documents and history. It is saved after every change, and the previous copy is kept as `catalog.bak.xml`.
- `objects\`: file contents. Each version is gzip-compressed and stored under its SHA-256 hash, so identical files are stored only once. Every restore is checked against the hash.

Uninstalling keeps the data folder.

## Build
**On Windows** (Visual Studio 2022 or the .NET SDK, plus NSIS: `winget install NSIS.NSIS`):
```powershell
.\build.ps1 -Version 0.1.0          # -> dist\RevisionTracker-Setup-0.1.0.exe
```
To develop, open `RevisionTracker.sln` in Visual Studio.

**On Linux** (Mono + NSIS: `apt install mono-devel nsis`):
```bash
./build.sh                          # -> dist/RevisionTracker-Setup-0.1.0.exe
```

## Requirements for users
Windows 10 (1903+) or 11. .NET Framework 4.8 is already built in; the installer checks for it.
Installs per user under `%LOCALAPPDATA%\Programs\RevisionTracker`, so no admin rights are needed, and creates Start menu and desktop shortcuts.

## Project layout
```
src/RevisionTracker.Core   storage, versioning, naming, export (no UI; reusable by deep-search later)
src/RevisionTracker.App    WinForms UI
installer/                 NSIS script
assets/app.ico             app icon
```

## Known limitations of 0.1
- The installer and exe are not code-signed, so Windows SmartScreen will warn on first run ("More info → Run anyway").
- The app is not DPI-aware yet, so it may look slightly soft on 125–150 % display scaling.
- No automatic file watching yet (planned).
