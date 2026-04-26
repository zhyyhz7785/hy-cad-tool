$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider 2048
$pub = $rsa.ToXmlString($false)
$pri = $rsa.ToXmlString($true)
$root = Split-Path -Parent $PSScriptRoot
Set-Content -LiteralPath (Join-Path $root "build\keys\rsa-pub.xml") -Value $pub -Encoding UTF8
Set-Content -LiteralPath (Join-Path $root "build\keys\rsa-priv.xml") -Value $pri -Encoding UTF8
Write-Host "Written build/keys/rsa-pub.xml and build/keys/rsa-priv.xml"
