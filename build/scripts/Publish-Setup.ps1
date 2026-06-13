<#
  Publish: Production build + HyCAD.bundle + Inno Setup -> HyCAD-Setup-x.y.z.exe

  Usage (repo root):
    powershell -ExecutionPolicy Bypass -File build\scripts\Publish-Setup.ps1
    powershell -ExecutionPolicy Bypass -File build\scripts\Publish-Setup.ps1 -Version 1.0.1

  Requires Inno Setup 6 (ISCC.exe).
#>
param(
    [string]$Version = "1.0.0",
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

function Find-Iscc {
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    foreach ($p in $candidates) {
        if (Test-Path $p) { return $p }
    }
    return $null
}

Write-Host "==> PackBundle (Production + bundle)" -ForegroundColor Cyan
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "PackBundle.ps1") -Version $Version -RepoRoot $RepoRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$iscc = Find-Iscc
if (-not $iscc) {
    Write-Host ""
    Write-Host "WARN: ISCC.exe not found (install Inno Setup 6)." -ForegroundColor Yellow
    Write-Host "  Bundle ready: $RepoRoot\build\artifacts\HyCAD.bundle" -ForegroundColor Yellow
    Write-Host "  Then run:" -ForegroundColor Yellow
    Write-Host "    iscc /DRepoRoot=`"$RepoRoot`" /DMyAppVersion=$Version build\installer\HyCAD-Setup.iss" -ForegroundColor Yellow
    exit 2
}

$iss = Join-Path $RepoRoot "build\installer\HyCAD-Setup.iss"
Write-Host "==> Inno Setup: $iscc" -ForegroundColor Cyan
& $iscc "/DRepoRoot=$RepoRoot" "/DMyAppVersion=$Version" $iss
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$setupExe = Join-Path $RepoRoot "build\artifacts\HyCAD-Setup-$Version.exe"
Write-Host ""
Write-Host "OK: $setupExe" -ForegroundColor Green
Write-Host "Ship to customer: run Setup.exe (re-run to uninstall), then hyLicense in AutoCAD" -ForegroundColor Yellow
