$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider 2048
$pub = $rsa.ToXmlString($false)
$pri = $rsa.ToXmlString($true)
# $PSScriptRoot = <repo>\build\scripts → 仓库根 = 再上两级
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Content -LiteralPath (Join-Path $root "build\keys\rsa-pub.xml") -Value $pub -Encoding UTF8
Set-Content -LiteralPath (Join-Path $root "build\keys\rsa-priv.xml") -Value $pri -Encoding UTF8
Write-Host "Written build/keys/rsa-pub.xml and build/keys/rsa-priv.xml"
Write-Host "下一步: 把 rsa-pub.xml 内容更新进 src/HyCADTool.Licensing/EmbeddedPublicKey.cs 并重编译主工程与 LicenseSigner"
