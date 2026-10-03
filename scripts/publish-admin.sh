#!/usr/bin/env bash
# Builds ExamBox.exe from Linux/macOS (needs Microsoft's .NET 8 SDK, which includes the WindowsDesktop targets).
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
dotnet publish "$root/src/ExamBox.Admin" -c Release -o "$root/dist"
rm -f "$root"/dist/*.pdb
echo "Done: $root/dist/ExamBox.exe"
