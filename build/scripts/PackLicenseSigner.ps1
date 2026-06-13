<#
  编译签发工具到 build\artifacts\LicenseSigner（单目录：exe + PrivateKey.xml + README）

  用法（仓库根目录）:
    powershell -ExecutionPolicy Bypass -File build\scripts\PackLicenseSigner.ps1
#>
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

$proj = Join-Path $RepoRoot "src\HyCADTool.LicenseSigner\HyCADTool.LicenseSigner.csproj"
$outDir = Join-Path $RepoRoot "build\artifacts\LicenseSigner"
$privSrc = Join-Path $RepoRoot "build\keys\rsa-priv.xml"

Write-Host "==> dotnet build LicenseSigner -c Release" -ForegroundColor Cyan
& dotnet build $proj -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $outDir "HyCADTool.LicenseSigner.exe"
if (-not (Test-Path $exe)) { Write-Error "Missing $exe" }

Write-Host ""
Write-Host "OK: $outDir" -ForegroundColor Green
Get-ChildItem $outDir -File | ForEach-Object { Write-Host "  $($_.Name)" -ForegroundColor DarkGray }

if (-not (Test-Path (Join-Path $outDir "PrivateKey.xml"))) {
    Write-Host ""
    Write-Host "WARN: 未找到 PrivateKey.xml。请执行 build\scripts\_gen-rsa-keys.ps1 或手动复制 build\keys\rsa-priv.xml" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "运行: $exe" -ForegroundColor Yellow
