#define AppName "Chess"
#define AppPublisher "Chess"
#ifndef AppVersion
	#define AppVersion "1.0.2-dev.7"
#endif

[Setup]
AppId={{B1F9A4C7-8F5E-4E4D-9C27-202601220001}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Chess
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\chessicon.ico
OutputDir=..\artifacts\packaged-installer
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
Source: "..\artifacts\packaged-installer\payload\Chess.msix"; DestDir: "{app}"; Flags: ignoreversion
#ifdef TestCertificate
Source: "..\artifacts\packaged-installer\payload\Chess.cer"; DestDir: "{tmp}"; Flags: deleteafterinstall
#endif
Source: "..\artifacts\packaged-installer\payload\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "Install-PackagedApp.ps1"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "..\chessicon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Run]
#ifdef TestCertificate
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -ExecutionPolicy Bypass -File ""{tmp}\Install-PackagedApp.ps1"" -PackagePath ""{app}\Chess.msix"" -CertificatePath ""{tmp}\Chess.cer"" -VcRedistPath ""{tmp}\vc_redist.x64.exe"" -InstallVcRedist -InstallPackage -InstallPackageCertificate -CreateStartShortcut -CreateDesktopShortcut"; StatusMsg: "Updating Chess and renewing the Start menu and Desktop shortcuts..."; Flags: runhidden waituntilterminated
#else
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -ExecutionPolicy Bypass -File ""{tmp}\Install-PackagedApp.ps1"" -PackagePath ""{app}\Chess.msix"" -VcRedistPath ""{tmp}\vc_redist.x64.exe"" -InstallVcRedist -InstallPackage -CreateStartShortcut -CreateDesktopShortcut"; StatusMsg: "Updating Chess and renewing the Start menu and Desktop shortcuts..."; Flags: runhidden waituntilterminated
#endif

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command ""Get-AppxPackage -Name 'Chess' | Remove-AppxPackage -ErrorAction SilentlyContinue"""; RunOnceId: "RemoveChessPackage"; Flags: runhidden waituntilterminated

[UninstallDelete]
Type: files; Name: "{autoprograms}\Chess.lnk"
Type: files; Name: "{autodesktop}\Chess.lnk"
Type: filesandordirs; Name: "{app}"