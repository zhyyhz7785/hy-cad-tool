<# 
  一键将 Production 输出打成 Autodesk ApplicationPlugins 用 HyCAD.bundle（ZIP）。
  用法（仓库根目录）:
    powershell -ExecutionPolicy Bypass -File build\scripts\PackBundle.ps1
    powershell -File build\scripts\PackBundle.ps1 -Version 1.0.1
  部署：将 build\artifacts\HyCAD.bundle 整个目录复制到
    $env:APPDATA\Autodesk\ApplicationPlugins\HyCAD.bundle
#>
param(
    [string]$Version = "1.0.0",
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

$refactored = Join-Path $RepoRoot "src\HyCADTool\HyCADTool.csproj"
$outDir = Join-Path $RepoRoot "src\HyCADTool\bin\Production"
$distRoot = Join-Path $RepoRoot "build\artifacts"
$bundleName = "HyCAD.bundle"
$bundlePath = Join-Path $distRoot $bundleName
$win64 = Join-Path $bundlePath "Contents\Win64"
$packageSrc = Join-Path $RepoRoot "build\installer\PackageContents.xml"

Write-Host "==> dotnet build -c Production" -ForegroundColor Cyan
& dotnet build $refactored -c Production --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not (Test-Path (Join-Path $outDir "HyCADTool.dll"))) {
    Write-Error "Missing $outDir\HyCADTool.dll"
}

if (-not (Test-Path $packageSrc)) { Write-Error "Missing $packageSrc" }

if (Test-Path $bundlePath) { Remove-Item $bundlePath -Recurse -Force }
New-Item -ItemType Directory -Path $win64 -Force | Out-Null

$pkg = [System.IO.File]::ReadAllText($packageSrc, [System.Text.Encoding]::UTF8)
$pkg = $pkg -replace 'AppVersion="[^"]+"', "AppVersion=`"$Version`""
$pkg = $pkg -replace 'Version="1\.0\.0"', "Version=`"$Version`""
$destPkg = Join-Path $bundlePath "PackageContents.xml"
[System.IO.File]::WriteAllText($destPkg, $pkg, (New-Object System.Text.UTF8Encoding $false))

$allowedDlls = "HyCADTool.dll", "HyCAD.BlenderUI.dll", "HyCADTool.TextLayout.dll", "HyCADTool.Licensing.dll", "Newtonsoft.Json.dll", "Autofac.dll", "Clipper2Lib.dll", "Markdig.dll", "NetTopologySuite.dll", "QRCoder.dll", "DocumentFormat.OpenXml.dll", "DocumentFormat.OpenXml.Framework.dll", "Microsoft.Bcl.AsyncInterfaces.dll", "System.Buffers.dll", "System.Diagnostics.DiagnosticSource.dll", "System.Memory.dll", "System.Numerics.Vectors.dll", "System.Runtime.CompilerServices.Unsafe.dll", "System.Threading.Tasks.Extensions.dll"
foreach ($n in $allowedDlls) {
    $f = Join-Path $outDir $n
    if (Test-Path $f) { Copy-Item $f -Destination $win64 -Force }
}
Get-ChildItem -Path $outDir -Filter "HyCAD*.pdb" -File -ErrorAction SilentlyContinue | ForEach-Object { Copy-Item $_.FullName -Destination $win64 -Force }
Get-ChildItem -Path $outDir -Filter "*.json" -File -ErrorAction SilentlyContinue | ForEach-Object { Copy-Item $_.FullName -Destination $win64 -Force }
Get-ChildItem -Path $outDir -Filter "*.config" -File -ErrorAction SilentlyContinue | ForEach-Object { Copy-Item $_.FullName -Destination $win64 -Force }
foreach ($dir in @("_libraries", "Resources")) {
    $src = Join-Path $outDir $dir
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $win64 $dir) -Recurse -Force
    }
}

$zip = Join-Path $distRoot "HyCAD-v$Version-bundle.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $bundlePath -DestinationPath $zip -Force

Write-Host "OK: $bundlePath" -ForegroundColor Green
Write-Host "ZIP: $zip" -ForegroundColor Green
Write-Host "Install: copy folder to `$env:APPDATA\Autodesk\ApplicationPlugins\HyCAD.bundle" -ForegroundColor Yellow
