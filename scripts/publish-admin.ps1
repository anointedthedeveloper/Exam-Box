# Builds the self-contained ExamBox.exe (no .NET install needed on the target PC).
# Output: dist\ExamBox.exe
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
dotnet publish "$root\src\ExamBox.Admin" -c Release -o "$root\dist"
Remove-Item "$root\dist\*.pdb" -ErrorAction SilentlyContinue
Write-Host "Done: $root\dist\ExamBox.exe"
