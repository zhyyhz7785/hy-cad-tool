$ErrorActionPreference = "Stop"
$p = Join-Path $PSScriptRoot "..\HyCADTool\Production\ProductionCommandFacade.cs"
$t = [IO.File]::ReadAllText($p)
$re = 'public void (Cmd_[A-Za-z0-9_]+)\(\)\s*=>\s*ProductionDispatcher\.Invoke\("([^"]+)"\);'
$n = [regex]::Replace($t, $re, {
  param($m)
  $a = $m.Groups[1].Value; $k = $m.Groups[2].Value
  "public void $a() => LicenseGate.RunGated(`"$k`", () => ProductionDispatcher.Invoke(`"$k`"));"
}, [Text.RegularExpressions.RegexOptions]::None)
[IO.File]::WriteAllText($p, $n, [Text.UTF8Encoding]::new($false))
Write-Host "OK"
