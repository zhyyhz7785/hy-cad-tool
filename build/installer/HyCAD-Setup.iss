; Inno Setup 6：HyCAD.bundle → 当前用户 ApplicationPlugins（绿色插件，无需管理员）
; 发版：powershell -File build\scripts\Publish-Setup.ps1 -Version x.y.z
; 中文文案 UTF-8 源文件；编译须走 Publish-Setup.ps1（自动转 GBK 再调 ISCC，避免乱码）。

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
DisableProgramGroupPage=yes
DefaultGroupName=HyCAD
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
WelcomeLabel2=本安装程序将把 HyCAD 安装到固定目录：%n%n  %%APPDATA%%\Autodesk\ApplicationPlugins\HyCAD.bundle%n%n[绿色插件] 无需管理员；支持 Windows 64 位完整版 AutoCAD（ApplicationPlugins 自动加载）。%n%n授权文件在 %%ProgramData%%\HyCAD\ (与插件目录分离)。%n%n若本机已安装，启动时将询问：纯净卸载 / 覆盖安装。
; MsgBox 专用槽位（对应页面已禁用，不会在向导中显示）：
; ConfirmUninstall | PasswordEditLabel | ExitSetupMessage | AboutSetupNote | DiskSpaceMBLabel
ConfirmUninstall=本机已安装 HyCAD。%n%n[是] 纯净卸载%n[否] 覆盖安装%n[取消] 退出
PasswordEditLabel=是否同时清除授权文件？%n(%%ProgramData%%\HyCAD\license.lic 与 state.bin)
ExitSetupMessage=确认卸载 HyCAD 插件？%n%n将删除：%n%%APPDATA%%\Autodesk\ApplicationPlugins\HyCAD.bundle
AboutSetupNote=纯净卸载完成。插件与授权文件均已删除。
DiskSpaceMBLabel=卸载完成。授权文件已保留于 %%ProgramData%%\HyCAD\

[Files]
Source: "{#RepoRoot}\build\artifacts\HyCAD.bundle\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\卸载 HyCAD 插件"; Filename: "{uninstallexe}"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
const
  UninstallRegKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}_is1';

var
  UninstallClearLicense: Boolean;

function BundlePath: String;
begin
  Result := ExpandConstant('{userappdata}\Autodesk\ApplicationPlugins\HyCAD.bundle');
end;

function IsHyCADInstalled: Boolean;
var
  S: String;
begin
  Result := RegQueryStringValue(HKCU, UninstallRegKey, 'UninstallString', S) and (S <> '');
  if not Result then
    Result := DirExists(BundlePath);
end;

function HyCADDataDir: String;
begin
  Result := ExpandConstant('{commonappdata}\HyCAD');
end;

procedure DeleteHyCADLicenseData;
var
  Dir: String;
begin
  Dir := HyCADDataDir;
  if FileExists(Dir + '\license.lic') then
    DeleteFile(Dir + '\license.lic');
  if FileExists(Dir + '\state.bin') then
    DeleteFile(Dir + '\state.bin');
  if DirExists(Dir) then
    RemoveDir(Dir);
end;

procedure RemoveUninstallRegistry;
begin
  if RegKeyExists(HKCU, UninstallRegKey) then
    RegDeleteKeyIncludingSubkeys(HKCU, UninstallRegKey);
end;

procedure PerformCleanUninstall(ClearLicense: Boolean);
var
  UninstallCmd: String;
  ResultCode: Integer;
begin
  if RegQueryStringValue(HKCU, UninstallRegKey, 'UninstallString', UninstallCmd) and (UninstallCmd <> '') then
    Exec(RemoveQuotes(UninstallCmd), '/SILENT /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if DirExists(BundlePath) then
    DelTree(BundlePath, True, True, True);

  RemoveUninstallRegistry;

  if ClearLicense then
    DeleteHyCADLicenseData;
end;

function InitializeSetup: Boolean;
var
  Choice: Integer;
  ClearLicense: Boolean;
begin
  Result := True;
  if not IsHyCADInstalled then
    Exit;

  Choice := MsgBox(SetupMessage(msgConfirmUninstall), mbConfirmation, MB_YESNOCANCEL);
  case Choice of
    IDYES:
      begin
        ClearLicense := MsgBox(SetupMessage(msgPasswordEditLabel), mbConfirmation, MB_YESNO) = IDYES;
        PerformCleanUninstall(ClearLicense);
        if ClearLicense then
          MsgBox(SetupMessage(msgAboutSetupNote), mbInformation, MB_OK)
        else
          MsgBox(SetupMessage(msgDiskSpaceMBLabel), mbInformation, MB_OK);
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
  Result := MsgBox(SetupMessage(msgExitSetupMessage), mbConfirmation, MB_YESNO) = IDYES;
  if Result then
    UninstallClearLicense := MsgBox(SetupMessage(msgPasswordEditLabel), mbConfirmation, MB_YESNO) = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if DirExists(BundlePath) then
      DelTree(BundlePath, True, True, True);
    RemoveUninstallRegistry;
    if UninstallClearLicense then
      DeleteHyCADLicenseData;
  end;
end;
