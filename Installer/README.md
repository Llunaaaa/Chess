# Chess for Windows

Download `Chess-v1.0.2-dev.5-win-x64-setup.exe` from the GitHub Release, double-click it, complete the installer, and launch Chess from Start or the Desktop shortcut. The installer updates the existing app in place, recursively replaces its files, and renews both shortcuts against the new package. It includes the app package, .NET runtime, Windows App SDK/WinUI runtime, Microsoft Visual C++ runtime, native files, sounds, and package certificate setup. No separate developer or runtime installation is required.

The installer does not launch Chess automatically. Account data remains under `%LocalAppData%\Chess` and is not removed by application updates.

For maintainers, the packaged release installer is built with:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
./Installer/Build-Packaged-Installer.ps1 -Version 1.0.2-dev.5 -CertificatePath .\release-signing.pfx -CertificatePassword $env:CHESS_CERTIFICATE_PASSWORD
```

The existing unpackaged Inno Setup path remains available as a fallback through `./Installer/Build-Installer-Exe.ps1` while the packaged path is being validated.

Local-only package installation testing can use `-AllowTestCertificate`; that mode is not suitable for public distribution and may trigger antivirus or SmartScreen warnings.

GitHub Actions signs the MSIX and installer when these repository secrets are configured:

- `WINDOWS_SIGNING_CERTIFICATE_BASE64`
- `WINDOWS_SIGNING_CERTIFICATE_PASSWORD`

Public builds require a trusted code-signing certificate. The outer installer and MSIX should both be signed before distribution; ordinary users do not need to install or trust a certificate manually. Unsigned or test-certificate builds may trigger antivirus or SmartScreen warnings and are for local testing only.
