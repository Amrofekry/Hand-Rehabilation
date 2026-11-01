; GripRehab installer (Stage 19). Built by Tools/release/stage_release.py with ISCC /DAppVersion=... /DSourceDir=...
; Per-user install (no admin): the camera worker's .venv is created on the target machine from the bundled Python, offline.
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #error SourceDir (the staged release folder) is required
#endif

[Setup]
AppId={{8C2F6B1E-5D4A-4E8B-9A57-3F1C0B7D2E64}
AppName=GripRehab
AppVersion={#AppVersion}
AppPublisher=GripRehab capstone team
DefaultDirName={localappdata}\Programs\GripRehab
DefaultGroupName=GripRehab
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputBaseFilename=GripRehab-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
UninstallDisplayIcon={app}\Game\GripRehab.exe

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion; Excludes: "Setup-Portable.cmd"

[Icons]
Name: "{group}\GripRehab"; Filename: "{app}\Game\GripRehab.exe"; WorkingDir: "{app}\Game"
Name: "{group}\Camera check"; Filename: "{app}\.venv\Scripts\python.exe"; Parameters: """{app}\Tools\release\first_run_check.py"""; WorkingDir: "{app}"
Name: "{group}\Arabic guide"; Filename: "{app}\Docs\GUIDE_AR.pdf"
Name: "{group}\Uninstall GripRehab"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\python\python.exe"; Parameters: "-m venv --without-pip --system-site-packages ""{app}\.venv"""; StatusMsg: "Preparing the camera worker..."; Flags: runhidden waituntilterminated
Filename: "{app}\Game\GripRehab.exe"; Description: "Start GripRehab"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\.venv"
Type: filesandordirs; Name: "{app}\python\Lib\site-packages\__pycache__"
