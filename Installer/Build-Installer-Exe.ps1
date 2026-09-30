param(
    [string]$Version = "1.0.2-dev.7",
    [string]$Configuration = "Release",
    [string]$CertificatePath = "",
    [string]$CertificatePassword = ""
)

$ErrorActionPreference = "Stop"
$Version = $Version.TrimStart("v")
if ($Version -notmatch "^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$") {
    throw "Version must use semantic versioning, for example 1.0.2-dev.7."
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $projectRoot "artifacts\installer"
$publishDirectory = Join-Path $artifactRoot "publish"
$installerPath = Join-Path $artifactRoot "Chess-v$Version-win-x64-setup.exe"

if (Test-Path $artifactRoot) {
    Remove-Item $artifactRoot -Recurse -Force
}
New-Item $publishDirectory -ItemType Directory -Force | Out-Null

Write-Host "Publishing Chess ($Configuration, win-x64)..."
dotnet publish (Join-Path $projectRoot "Chess.csproj") `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory `
    -p:ChessPackaged=false `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$systemDirectory = Join-Path $env:WINDIR "System32"
foreach ($runtimeFile in @("vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "msvcp140_1.dll", "msvcp140_2.dll")) {
    $sourcePath = Join-Path $systemDirectory $runtimeFile
    if (-not (Test-Path $sourcePath)) {
        throw "Required Visual C++ runtime file was not found: $sourcePath"
    }
    Copy-Item $sourcePath $publishDirectory -Force
}

$innoCompiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($null -eq $innoCompiler) {
    foreach ($knownPath in @(
        (Join-Path $env:ProgramFiles "Inno Setup 7\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 7\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe")
    )) {
        if (Test-Path $knownPath) {
            $innoCompiler = Get-Item $knownPath
            break
        }
    }
}
if ($null -eq $innoCompiler) {
    throw "ISCC.exe was not found. Install Inno Setup 6 before creating the installer."
}

$innoCompilerPath = if ($innoCompiler.PSObject.Properties.Name -contains "Source") {
    $innoCompiler.Source
} else {
    $innoCompiler.FullName
}
& $innoCompilerPath "/DAppVersion=$Version" (Join-Path $PSScriptRoot "Chess.iss")
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $installerPath)) {
    throw "Inno Setup completed but did not create $installerPath."
}

if ($CertificatePath) {
    $signTool = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -eq $signTool) {
        throw "signtool.exe was not found. Install the Windows 11 SDK before signing."
    }

    & $signTool.Source sign /fd SHA256 /a /f $CertificatePath /p $CertificatePassword $installerPath
    if ($LASTEXITCODE -ne 0) {
        throw "signtool.exe failed with exit code $LASTEXITCODE."
    }
}

$checksum = Get-FileHash $installerPath -Algorithm SHA256
$checksum.Hash | Set-Content "$installerPath.sha256"
$checksum | Format-List
Write-Host "Created $installerPath"
