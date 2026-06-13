# HyCAD 离线签发（LicenseSigner.exe）

1. 生成密钥对（仅首次、换密钥时）  
   在仓库根目录执行: `powershell -ExecutionPolicy Bypass -File build\scripts\_gen-rsa-keys.ps1`
   得到 `build/keys/rsa-pub.xml` 与 **本地** `build/keys/rsa-priv.xml`（私钥，勿提交；已加入 .gitignore）。
   **重要**：新生成后必须把 `build/keys/rsa-pub.xml` 的**整行内容**复制进 `HyCADTool.Licensing/EmbeddedPublicKey.cs` 的 `RsaKeyXml` 常量，并**重新编译 HyCADTool**（否则 LicenseSigner 签出的 `license.lic` 与插件内公钥不一致，导入会提示「签名校验失败」）。

2. 编译: `dotnet build HyCADTool.LicenseSigner\HyCADTool.LicenseSigner.csproj -c Release`  
   Debug 构建时，若存在 `build\keys\rsa-priv.xml`，会链接复制为输出目录的 `PrivateKey.xml`。Release 需手动放置私钥。

3. 向客户要机器码: AutoCAD 内执行 `hyLicense`，复制 **8 段 × 4 位** 机器码（`XXXX-XXXX-...`）。

4. 运行 `HyCADTool.LicenseSigner.exe`，填机器码、客户名、选档（standard/professional/enterprise）与 1 年/3 年/终身，点击「生成授权」。

5. **交付方式（二选一）**  
   - **微信/文本**：「复制 HYC1 授权码」→ 客户 `hyLicense` 粘贴 → 确定激活  
   - **文件**：「保存 license.lic」→ 客户 `hyLicense` → 从文件导入

6. 签发成功后会在 exe 同目录追加 `issued-licenses.csv` 台账（不含完整授权码）。

7. 发错/作废: 无在线吊销；需重新发一份新 license 覆盖；旧 file 可保留在台账备查（同一机器码上总是以后覆盖为准）。
