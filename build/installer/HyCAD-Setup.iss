; Inno Setup 6：HyCAD.bundle → 当前用户 ApplicationPlugins（绿色插件，无需管理员）
; 发版：powershell -File build\scripts\Publish-Setup.ps1 -Version x.y.z

#define MyAppName "HyCAD"
#define MyAppPublisher "HyCAD"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef RepoRoot
  #define RepoRoot "e:\BaiduSyncdisk\Code\CSharp\CursorProjects\hy-cad-tool"
#endif
#define MyAppId "{{A7E4D2B1-3C5F-4E6A-9B8C-0D1E2F3A4B5C}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={userappdata}\Autodesk\ApplicationPlugins\HyCAD.bundle
DisableDirPage=yes
DisableProgramGroupPage=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
OutputDir={#RepoRoot}\build\artifacts
OutputBaseFilename=HyCAD-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=HyCAD AutoCAD 插件
UninstallDisplayIcon={app}\Contents\Win64\HyCADTool.dll
CreateUninstallRegKey=yes

[Messages]
WelcomeLabel2=本安装程序将把 HyCAD 安装到当前用户的 AutoCAD ApplicationPlugins 目录。%n%n【绿色插件】无需管理员；仅写入 AppData；卸载即删文件夹。AutoCAD 2024–2027 启动后自动加载。授权在 %%ProgramData%%\HyCAD\license.lic，卸载不删授权。%n%n若已安装，启动时会询问：卸载 / 覆盖安装 / 取消。

[Files]
Source: "{#RepoRoot}\build\artifacts\HyCAD.bundle\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\卸载 HyCAD 插件"; Filename: "{uninstallexe}"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
const
  UninstallRegKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}_is1';

function IsHyCADInstalled: Boolean;
var
  S: String;
begin
  Result := RegQueryStringValue(HKCU, UninstallRegKey, 'UninstallString', S) and (S <> '');
  if not Result then
    Result := DirExists(ExpandConstant('{userappdata}\Autodesk\ApplicationPlugins\HyCAD.bundle'));
end;

function InitializeSetup: Boolean;
var
  ResultCode: Integer;
  UninstallCmd: String;
  Choice: Integer;
begin
  Result := True;
  if not IsHyCADInstalled then
    Exit;

  Choice := MsgBox(
    'Detected HyCAD is already installed.' + #13#10 + #13#10 +
    '[Yes] Uninstall (keeps license.lic)' + #13#10 +
    '[No]  Upgrade / reinstall' + #13#10 +
    '[Cancel] Exit',
    mbConfirmation, MB_YESNOCANCEL);

  case Choice of
    IDYES:
      begin
        if RegQueryStringValue(HKCU, UninstallRegKey, 'UninstallString', UninstallCmd) and (UninstallCmd <> '') then
          Exec(RemoveQuotes(UninstallCmd), '/SILENT', '', SW_SHOW, ewWaitUntilTerminated, ResultCode)
        else if DirExists(ExpandConstant('{userappdata}\Autodesk\ApplicationPlugins\HyCAD.bundle')) then
          DelTree(ExpandConstant('{userappdata}\Autodesk\ApplicationPlugins\HyCAD.bundle'), True, True, True);
        MsgBox('HyCAD uninstalled. License kept at: ' + ExpandConstant('{commonappdata}\HyCAD\license.lic'), mbInformation, MB_OK);
        Result := False;
      end;
    IDNO:
      Result := True;
    IDCANCEL:
      Result := False;
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
  if MsgBox(
    'Uninstall HyCAD plugin?' + #13#10 + #13#10 +
    'Remove: ' + ExpandConstant('{userappdata}\Autodesk\ApplicationPlugins\HyCAD.bundle') + #13#10 + #13#10 +
    'Keep license: ' + ExpandConstant('{commonappdata}\HyCAD\license.lic'),
    mbConfirmation, MB_YESNO) = IDNO then
    Result := False;
end;
