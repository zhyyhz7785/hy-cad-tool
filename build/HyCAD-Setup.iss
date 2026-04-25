; Inno Setup 6：将 dist\HyCAD.bundle 安装到当前用户 ApplicationPlugins
; 需已执行: powershell -File tools\PackBundle.ps1
; 编译前将 #define RepoRoot 改为本机仓库根目录，或用 ISCC /DRepoRoot=...

#define MyAppName "HyCAD"
#define MyAppVersion "1.0.0"
#ifndef RepoRoot
  #define RepoRoot "e:\BaiduSyncdisk\Code\CSharp\CursorProjects\hy-cad-tool"
#endif

[Setup]
AppId={{A7E4D2B1-3C5F-4E6A-9B8C-0D1E2F3A4B5C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={userappdata}\Autodesk\ApplicationPlugins\HyCAD.bundle
DisableDirPage=yes
ArchitecturesAllowed=x64compatible
PrivilegesRequired=lowest
OutputDir={#RepoRoot}\dist
OutputBaseFilename=HyCAD-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "{#RepoRoot}\dist\HyCAD.bundle\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
