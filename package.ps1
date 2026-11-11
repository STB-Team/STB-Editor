# Builds a portable, self-contained STB Editor (no .NET install needed) and zips it.
# Output: dist\STB Editor\ (folder) and dist\STBEditor-<version>.zip
param([string]$Version = "0.1.0")

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dotnet = if (Get-Command dotnet -ErrorAction SilentlyContinue) { "dotnet" } else { "C:\Program Files\dotnet\dotnet.exe" }
$stage = Join-Path $root "dist\STB Editor"
$zip = Join-Path $root "dist\STBEditor-$Version.zip"

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force (Split-Path $stage) | Out-Null

& $dotnet publish (Join-Path $root "src\Editor.App\Editor.App.csproj") -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -p:DebugType=none -p:SatelliteResourceLanguages=en -o $stage
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

Copy-Item (Join-Path $root "README.md") (Join-Path $stage "README.md")
@"
STB Editor $Version

Extract to any folder, for example under the game folder: C:\Skyrim\STB Editor\
Run STBEditor.exe (no installation needed; .NET is included).

With MO2: Executables (gears icon) -> + -> Add from file -> STBEditor.exe.
No arguments are required. The editor reads the MO2 profile and mod folders itself,
so launching through MO2 is optional; you can also run it directly.

If the editor is inside the game folder, it detects that game automatically.
Settings, cache and logs: %LOCALAPPDATA%\STBEditor
"@ | Set-Content -Encoding utf8 (Join-Path $stage "README.txt")

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal
$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
"Packed $zip ($size MB)"
