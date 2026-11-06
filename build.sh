#!/usr/bin/env bash
# Builds RevisionTracker.exe and the Windows installer on Linux (Mono + NSIS).
# On Windows, open RevisionTracker.sln in Visual Studio instead, or run build.ps1.
set -euo pipefail
cd "$(dirname "$0")"

VERSION="${VERSION:-0.1.0}"
OUT=build
rm -rf "$OUT" && mkdir -p "$OUT" dist

REFS="-r:System.dll -r:System.Core.dll -r:System.Xml.dll"

mcs -nologo -codepage:utf8 -optimize+ -sdk:4.5 -target:library \
    -out:"$OUT/RevisionTracker.Core.dll" $REFS -r:System.IO.Compression.dll \
    src/RevisionTracker.Core/*.cs

mcs -nologo -codepage:utf8 -optimize+ -sdk:4.5 -target:winexe \
    -out:"$OUT/RevisionTracker.exe" -win32icon:assets/app.ico \
    $REFS -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:"$OUT/RevisionTracker.Core.dll" \
    src/RevisionTracker.App/*.cs

cp src/RevisionTracker.App/App.config "$OUT/RevisionTracker.exe.config"

makensis -V2 -DVERSION="$VERSION" -DSRCDIR="$(pwd)/$OUT" installer/RevisionTracker.nsi
echo "Installer: dist/RevisionTracker-Setup-$VERSION.exe"
