param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,
    [string]$CertificatePath = "",
    [string]$VcRedistPath = "",
    [switch]$InstallVcRedist,
    [switch]$InstallPackageCertificate,
    [switch]$InstallPackage,
    [switch]$CreateStartShortcut,
    [switch]$CreateDesktopShortcut
)

$ErrorActionPreference = "Stop"

function New-ChessShortcut {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ShortcutPath,
        [Parameter(Mandatory = $true)]
        [string]$ApplicationUserModelId,
        [Parameter(Mandatory = $true)]
        [string]$IconPath
    )

    $parent = Split-Path -Parent $ShortcutPath
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = Join-Path $env:WINDIR "explorer.exe"
    $shortcut.Arguments = "shell:AppsFolder\$ApplicationUserModelId"
    $shortcut.WorkingDirectory = $env:WINDIR
    $shortcut.IconLocation = "$IconPath,0"
    $shortcut.Save()
}

if ($InstallPackage) {
    if ($InstallVcRedist) {
        if (-not (Test-Path $VcRedistPath)) {
            throw "The bundled Visual C++ Redistributable was not found: $VcRedistPath"
        }

        $vcProcess = Start-Process -FilePath $VcRedistPath -ArgumentList "/install", "/quiet", "/norestart" -Wait -PassThru
        if ($vcProcess.ExitCode -notin @(0, 1638, 3010)) {
            throw "The Visual C++ Redistributable installer failed with exit code $($vcProcess.ExitCode)."
        }
    }

    if ($InstallPackageCertificate) {
        if (-not (Test-Path $CertificatePath)) {
            throw "The local test package certificate was not found: $CertificatePath"
        }

        & "$env:WINDIR\System32\certutil.exe" -addstore -f TrustedPeople $CertificatePath | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "The Chess package certificate could not be trusted automatically."
        }
    }

    $installedPackages = @(Get-AppxPackage -Name Chess -ErrorAction SilentlyContinue)
    foreach ($installedPackage in $installedPackages) {
        if ($installedPackage.PackageFullName) {
            Remove-AppxPackage -Package $installedPackage.PackageFullName -ErrorAction SilentlyContinue
        }
    }

    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if (-not (Get-AppxPackage -Name Chess -ErrorAction SilentlyContinue)) {
            break
        }
        Start-Sleep -Milliseconds 500
    }
    if (Get-AppxPackage -Name Chess -ErrorAction SilentlyContinue) {
        throw "The previous Chess package could not be removed before the update."
    }

    Add-AppxPackage -Path $PackagePath -ForceApplicationShutdown -ForceUpdateFromAnyVersion
}

$package = $null
for ($attempt = 0; $attempt -lt 30 -and $null -eq $package; $attempt++) {
    $package = Get-AppxPackage -Name Chess -ErrorAction SilentlyContinue | Sort-Object Version -Descending | Select-Object -First 1
    if ($null -eq $package) {
        Start-Sleep -Milliseconds 500
    }
}
if ($null -eq $package) {
    throw "Chess was not registered as a Windows package."
}

$applicationUserModelId = "$($package.PackageFamilyName)!Chess"
$iconPath = Join-Path $package.InstallLocation "chessicon.ico"

if ($CreateStartShortcut) {
    Remove-Item (Join-Path ([Environment]::GetFolderPath("CommonPrograms")) "Chess.lnk") -Force -ErrorAction SilentlyContinue
    New-ChessShortcut -ShortcutPath (Join-Path ([Environment]::GetFolderPath("CommonPrograms")) "Chess.lnk") -ApplicationUserModelId $applicationUserModelId -IconPath $iconPath
}

if ($CreateDesktopShortcut) {
    Remove-Item (Join-Path ([Environment]::GetFolderPath("CommonDesktopDirectory")) "Chess.lnk") -Force -ErrorAction SilentlyContinue
    New-ChessShortcut -ShortcutPath (Join-Path ([Environment]::GetFolderPath("CommonDesktopDirectory")) "Chess.lnk") -ApplicationUserModelId $applicationUserModelId -IconPath $iconPath
}