# HyCAD 离线签发（LicenseSigner.exe）

**交付目录**（Release 编译后）：`build/artifacts/LicenseSigner/`

```
build/artifacts/LicenseSigner/
├── HyCADTool.LicenseSigner.exe   ← 单文件（Costura 内嵌依赖）
├── PrivateKey.xml                ← 由 build/keys/rsa-priv.xml 自动链接（若存在）
├── README-LicenseSigner.md       ← 签发说明（本文件副本）
└── issued-licenses.csv           ← 签发台账（运行后自动生成，勿提交）
```

## 一键打包

仓库根目录：

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\PackLicenseSigner.ps1
```

等价于 `dotnet build src\HyCADTool.LicenseSigner\HyCADTool.LicenseSigner.csproj -c Release`。

## 流程

1. **生成密钥对**（仅首次、换密钥时）  
   `powershell -ExecutionPolicy Bypass -File build\scripts\_gen-rsa-keys.ps1`  
   得到 `build/keys/rsa-pub.xml` 与 **本地** `build/keys/rsa-priv.xml`（私钥勿提交）。  
   **重要**：必须把 `rsa-pub.xml` 整行内容同步进 `HyCADTool.Licensing/EmbeddedPublicKey.cs` 的 `RsaKeyXml`，并**重编 HyCADTool**（否则签名校验失败）。

2. **编译签发工具**（见上一节一键打包）。

3. **向客户要机器码**：AutoCAD 内 `hyLicense`，复制 **8 段 × 4 位** 机器码。

4. **运行** `build\artifacts\LicenseSigner\HyCADTool.LicenseSigner.exe`，填机器码、客户名、档位与期限 → **生成授权**。

5. **交付（二选一）**  
   - **微信/文本**：「复制 HYC1 授权码」→ 客户 `hyLicense` 粘贴激活  
   - **文件**：「保存 license.lic」→ 客户从文件导入

6. 签发成功后在同目录追加 `issued-licenses.csv`（不含完整授权码）。

7. 发错/作废：无在线吊销；重新签发覆盖即可。
