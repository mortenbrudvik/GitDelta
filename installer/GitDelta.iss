; GitDelta installer (Inno Setup 6.x)
; Per-user, no admin. Installs gitdelta.exe to %LOCALAPPDATA%\Programs\GitDelta,
; puts the install dir on the per-user PATH, and ensures the .NET 10 Desktop Runtime.

#define AppName        "GitDelta"
#define AppExeName     "gitdelta.exe"
#define AppPublisher   "Morten Brudvik"
#define AppUrl         "https://github.com/mortenbrudvik/GitDelta"
#define AppVersion     "0.1.0"
; The exe produced by build/publish.ps1 (run from repo root: pwsh -File build/publish.ps1)
#define PublishDir     "..\publish"
; Bundled .NET Desktop Runtime installer (fetched by installer\fetch-runtime.ps1).
; Run: pwsh -File installer/fetch-runtime.ps1  before compiling this script.
#define RuntimeInstaller "windowsdesktop-runtime-10.0.0-win-x64.exe"
#define RuntimeDownloadUrl "https://dotnet.microsoft.com/download/dotnet/10.0/runtime"

[Setup]
AppId={{6D2B6F2E-7C2C-4F4E-9D3B-7A2E2C9A1F10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}/releases
; Per-user install: no UAC elevation, lands in %LOCALAPPDATA%\Programs\GitDelta.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={localappdata}\Programs\{#AppName}
DisableProgramGroupPage=yes
DefaultGroupName={#AppName}
; Required so Inno broadcasts WM_SETTINGCHANGE after we touch PATH (new terminals pick it up).
ChangesEnvironment=yes
; Per-user uninstaller / Add-Remove-Programs registration (HKCU).
UninstallDisplayName={#AppName}
; NOTE: Uncomment SetupIconFile and UninstallDisplayIcon once src/GitDelta.UI/Assets/gitdelta.ico exists.
; SetupIconFile=..\src\GitDelta.UI\Assets\gitdelta.ico
; UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=GitDelta-{#AppVersion}-setup

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "addtopath"; Description: "Add GitDelta to the PATH (run 'gitdelta' from any terminal)"; GroupDescription: "Integration:"; Flags: checkedonce
Name: "startmenu"; Description: "Create a Start Menu shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce

[Files]
; The single-file framework-dependent exe.
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; Bundle the runtime installer ONLY if it was fetched; runs silently on demand (see [Run]).
; skipifsourcedoesntexist allows compiling without the bundle (installer opens the download page instead).
Source: "runtime\{#RuntimeInstaller}"; DestDir: "{tmp}"; Flags: deleteafterinstall external skipifsourcedoesntexist; Check: NeedsDotNetRuntime

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: startmenu
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"; Tasks: startmenu

[Run]
; Install the .NET Desktop Runtime silently if missing AND we bundled it.
Filename: "{tmp}\{#RuntimeInstaller}"; Parameters: "/install /quiet /norestart"; \
    StatusMsg: "Installing the .NET 10 Desktop Runtime..."; \
    Flags: waituntilterminated; Check: ShouldRunBundledRuntime
; If the runtime is missing and was NOT bundled, open the download page after install.
Filename: "{#RuntimeDownloadUrl}"; Description: "Download the required .NET 10 Desktop Runtime"; \
    Flags: shellexec postinstall skipifsilent; Check: NeedsRuntimeButNotBundled

[Code]
const
  EnvironmentKey = 'Environment';

var
  GBundledRuntimePresent: Boolean;

{ ---- PATH helpers (HKCU\Environment, REG_EXPAND_SZ, idempotent, never setx) ---- }

function GetPathValue(var Value: string): Boolean;
begin
  Result := RegQueryStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', Value);
  if not Result then
    Value := '';
end;

{ Returns True if Dir already appears as a whole entry in the ';'-delimited PathList. }
function PathContainsDir(const PathList, Dir: string): Boolean;
var
  Hay, Needle: string;
begin
  Hay    := ';' + Uppercase(Trim(PathList)) + ';';
  Needle := ';' + Uppercase(Trim(Dir)) + ';';
  Result := Pos(Needle, Hay) > 0;
end;

procedure AddDirToPath(const Dir: string);
var
  PathList, NewPath: string;
begin
  GetPathValue(PathList);
  if PathContainsDir(PathList, Dir) then
    exit; { idempotent: already present, do nothing }

  if (PathList = '') then
    NewPath := Dir
  else if (Copy(PathList, Length(PathList), 1) = ';') then
    NewPath := PathList + Dir
  else
    NewPath := PathList + ';' + Dir;

  { REG_EXPAND_SZ so other entries with %VAR% keep expanding. Inno broadcasts
    WM_SETTINGCHANGE on completion because ChangesEnvironment=yes. }
  RegWriteExpandStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', NewPath);
end;

procedure RemoveDirFromPath(const Dir: string);
var
  PathList, Rebuilt, Part: string;
  P: Integer;
begin
  if not GetPathValue(PathList) then
    exit;

  Rebuilt  := '';
  PathList := PathList + ';';
  repeat
    P    := Pos(';', PathList);
    Part := Trim(Copy(PathList, 1, P - 1));
    PathList := Copy(PathList, P + 1, Length(PathList));
    if (Part <> '') and (Uppercase(Part) <> Uppercase(Trim(Dir))) then
    begin
      if Rebuilt = '' then
        Rebuilt := Part
      else
        Rebuilt := Rebuilt + ';' + Part;
    end;
  until PathList = '';

  if Rebuilt = '' then
    { GitDelta was the only entry — delete the value rather than leaving an empty Path. }
    RegDeleteValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path')
  else
    RegWriteExpandStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', Rebuilt);
end;

{ ---- .NET 10 Desktop Runtime detection ---- }

{ True if a Microsoft.WindowsDesktop.App 10.x shared framework folder exists. }
function IsDotNet10DesktopRuntimeInstalled(): Boolean;
var
  FindRec: TFindRec;
  BaseDir: string;
begin
  Result  := False;
  { Per-machine x64 install location. The shared framework folder is the reliable,
    no-process probe — avoids launching dotnet.exe. }
  BaseDir := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if not DirExists(BaseDir) then
    exit;

  if FindFirst(BaseDir + '\10.*', FindRec) then
  try
    repeat
      if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
      begin
        Result := True;
        break;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function NeedsDotNetRuntime(): Boolean;
begin
  Result := not IsDotNet10DesktopRuntimeInstalled();
end;

{ True only when the runtime is missing AND the bundled installer made it onto disk. }
function ShouldRunBundledRuntime(): Boolean;
begin
  Result := NeedsDotNetRuntime() and GBundledRuntimePresent;
end;

{ True when the runtime is missing but no bundle was shipped (open download page). }
function NeedsRuntimeButNotBundled(): Boolean;
begin
  Result := NeedsDotNetRuntime() and (not GBundledRuntimePresent);
end;

{ ---- Wizard wiring ---- }

procedure InitializeWizard();
begin
  { Record whether the bundle was compiled into this setup (source existed at compile time
    AND extracted to {tmp}). We probe {tmp} after [Files] extraction in CurStepChanged;
    here we initialise to False. }
  GBundledRuntimePresent := False;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    { Re-evaluate after [Files] extraction so the {tmp} probe is accurate. }
    GBundledRuntimePresent := FileExists(ExpandConstant('{tmp}\{#RuntimeInstaller}'));
    if WizardIsTaskSelected('addtopath') then
      AddDirToPath(ExpandConstant('{app}'));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveDirFromPath(ExpandConstant('{app}'));
end;
