#define AppName "Chess"
#define AppPublisher "Chess"
#define AppExeName "Chess.exe"
#ifndef AppVersion
	#define AppVersion "1.0.0"
#endif

[Setup]
AppId={{B1F9A4C7-8F5E-4E4D-9C27-202601220001}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Chess
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\chessicon.ico
OutputDir=..\artifacts\installer
OutputBaseFilename=Chess-v{#AppVersion}-win-x64-setup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
SetupIconFile=..\chessicon.ico
DisableProgramGroupPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce

[Files]
Source: "..\artifacts\installer\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\Chess"; Filename: "{app}\Chess.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Chess"; Filename: "{app}\Chess.exe"; WorkingDir: "{app}"; Tasks: desktopicon

