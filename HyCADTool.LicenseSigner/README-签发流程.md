# HyCAD 离线签发（LicenseSigner.exe）

1. 生成密钥对（仅首次、换密钥时）  
   在仓库根目录执行: `powershell -ExecutionPolicy Bypass -File tools\_gen-rsa-keys.ps1`  
   得到 `tools/rsa-pub.xml` 与 **本地** `tools/rsa-priv.xml`（私钥，勿提交；已加入 .gitignore）。  
   **重要**：新生成后必须把 `tools/rsa-pub.xml` 的**整行内容**复制进 `HyCADTool.Licensing/EmbeddedPublicKey.cs` 的 `RsaKeyXml` 常量，并**重新编译 HyCADTool.Refactored**（否则 LicenseSigner 签出的 `license.lic` 与插件内公钥不一致，导入会提示「签名校验失败」）。

2. 编译: `dotnet build HyCADTool.LicenseSigner\HyCADTool.LicenseSigner.csproj -c Release`  
   若存在 `tools\rsa-priv.xml`，会链接复制为输出目录的 `PrivateKey.xml`。

3. 向客户要机器码: AutoCAD 内执行 `hyLicense`，或让客户复制 5 段 `XXXX-XXXX-...`。

4. 运行 `HyCADTool.LicenseSigner.exe`，填机器码、客户名、选档（standard/professional/enterprise）与 1 年/3 年/终身，生成 `license.lic`，微信发回客户。

5. 客户: `hyLicense` 窗口中「从文件导入」，或手拷到 `%ProgramData%\HyCAD\license.lic`。

6. 发错/作废: 无在线吊销；需重新发一份新 license 覆盖；旧 file 可保留在台账备查（同一机器码上总是以后覆盖为准）。
