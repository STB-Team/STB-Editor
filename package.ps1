# Builds a portable, self-contained STB Editor (no .NET install needed) and zips it.
# Output: dist\STB Editor\ (folder) and dist\STBEditor-<version>.zip
param([string]$Version)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
if (!$Version) {
    $Version = ([xml](Get-Content -LiteralPath (Join-Path $root "Directory.Build.props") -Raw)).Project.PropertyGroup.Version
}
$dotnet = if (Get-Command dotnet -ErrorAction SilentlyContinue) { "dotnet" } else { "C:\Program Files\dotnet\dotnet.exe" }
$stage = Join-Path $root "dist\STB Editor"
$payload = Join-Path $stage "app"
$zip = Join-Path $root "dist\STBEditor-$Version.zip"

if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force $payload | Out-Null
New-Item -ItemType Directory -Force (Join-Path $stage "fonts") | Out-Null

& $dotnet publish (Join-Path $root "src\Editor.App\Editor.App.csproj") -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -p:DebugType=none -p:SatelliteResourceLanguages=en -p:PortableLayout=true -o $payload
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

if (!(Test-Path -LiteralPath (Join-Path $stage "STBEditor.exe"))) { throw "portable apphost missing" }
@"
STB Editor $Version

Extract to any folder, for example under the game folder: C:\Skyrim\STB Editor\
Run STBEditor.exe (no installation needed; .NET is included).
Keep the app subfolder beside STBEditor.exe: it contains all libraries and the bundled .NET runtime.
Optional interface fonts: put your own .ttf or .otf files in fonts beside STBEditor.exe,
restart the editor, then choose a filename in View -> Settings -> Interface font.
Custom fonts use their own built-in style and weight. Code and logs stay monospace.
For updates, preserve your fonts folder and replace the application files; do not merge app with an older flat distribution.

With MO2: Executables (gears icon) -> + -> Add from file -> STBEditor.exe.
No arguments are required. The editor reads the MO2 profile and mod folders itself,
so launching through MO2 is optional; you can also run it directly.

If the editor is inside the game folder, it detects that game automatically.
Settings, cache and logs: %LOCALAPPDATA%\STBEditor
"@ | Set-Content -Encoding utf8 (Join-Path $stage "README.txt")

if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -LiteralPath $stage -DestinationPath $zip -CompressionLevel Optimal
$size = [math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 1)
"Packed $zip ($size MB)"
