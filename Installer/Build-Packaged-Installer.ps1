param(
    [string]$Version = "1.0.2-dev.6",
    [string]$Configuration = "Release",
    [string]$CertificatePath = "",
    [string]$CertificatePassword = "",
    [string]$VcRedistPath = "",
    [switch]$AllowTestCertificate
)

$ErrorActionPreference = "Stop"
$Version = $Version.TrimStart("v")
if ($Version -notmatch "^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$") {
    throw "Version must use semantic versioning, for example 1.0.2-dev.6."
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $projectRoot "artifacts\packaged-installer"
$payloadDirectory = Join-Path $artifactRoot "payload"
$manifestPath = Join-Path $artifactRoot "Package.appxmanifest"
$installerPath = Join-Path $artifactRoot "Chess-v$Version-win-x64-setup.exe"
$packageBuildRoot = Join-Path $projectRoot "bin\x64\$Configuration\net8.0-windows10.0.19041.0\win-x64\AppPackages"

if (-not $CertificatePath -and -not $AllowTestCertificate) {
    throw "A trusted package-signing PFX is required. Use -AllowTestCertificate only for local installation testing."
}

if (Test-Path $artifactRoot) {
    Remove-Item $artifactRoot -Recurse -Force
}
New-Item $payloadDirectory -ItemType Directory -Force | Out-Null

if (-not $VcRedistPath) {
    $VcRedistPath = Join-Path $payloadDirectory "vc_redist.x64.exe"
    Write-Host "Downloading the Microsoft Visual C++ x64 Redistributable..."
    Invoke-WebRequest -Uri "https://aka.ms/vs/17/release/vc_redist.x64.exe" -OutFile $VcRedistPath
}
if (-not (Test-Path $VcRedistPath)) {
    throw "The Visual C++ Redistributable was not found: $VcRedistPath"
}
if (-not (Test-Path (Join-Path $payloadDirectory "vc_redist.x64.exe"))) {
    Copy-Item $VcRedistPath (Join-Path $payloadDirectory "vc_redist.x64.exe") -Force
}

$packageCertificatePath = $CertificatePath
$testCertificate = $false
if (-not $packageCertificatePath) {
    $testCertificate = $true
    $certificate = New-SelfSignedCertificate -Type Custom -Subject "CN=Chess" -KeyUsage DigitalSignature -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3") -FriendlyName "Chess local package certificate" -CertStoreLocation Cert:\CurrentUser\My
    $packageCertificatePath = Join-Path $artifactRoot "Chess-signing.pfx"
    $securePassword = ConvertTo-SecureString "Chess-local-package" -AsPlainText -Force
    Export-PfxCertificate -Cert $certificate -FilePath $packageCertificatePath -Password $securePassword | Out-Null
    $CertificatePassword = "Chess-local-package"
}

$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($packageCertificatePath, $CertificatePassword)
if (-not $certificate.HasPrivateKey) {
    throw "The package certificate must contain a private key: $packageCertificatePath"
}
$secureCertificatePassword = ConvertTo-SecureString $CertificatePassword -AsPlainText -Force
Import-PfxCertificate -FilePath $packageCertificatePath -CertStoreLocation Cert:\CurrentUser\My -Password $secureCertificatePassword | Out-Null
$publisher = [System.Security.SecurityElement]::Escape($certificate.Subject)

$sourceManifest = Join-Path $projectRoot "Package.appxmanifest"
$manifestText = Get-Content $sourceManifest -Raw
$manifestText = $manifestText.Replace('Publisher="CN=Chess"', ('Publisher="' + $publisher + '"'))
Set-Content $manifestPath $manifestText -Encoding utf8

$publishArguments = @(
    "publish", (Join-Path $projectRoot "Chess.csproj"),
    "--configuration", $Configuration,
    "--runtime", "win-x64",
    "--self-contained", "true",
    "-p:Platform=x64",
    "-p:ChessPackaged=true",
    "-p:WindowsPackageType=MSIX",
    "-p:ChessPackageManifest=$manifestPath",
    "-p:AppxPackageSigningEnabled=true",
    "-p:PackageCertificateThumbprint=$($certificate.Thumbprint)",
    "-p:PublishAppxPackage=true",
    "-p:AppxBundle=Never"
)

Write-Host "Publishing signed Chess MSIX ($Configuration, win-x64)..."
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$package = Get-ChildItem $packageBuildRoot -Recurse -File -Filter "Chess_*.msix" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $package) {
    throw "The MSIX package was not created under $packageBuildRoot."
}
Copy-Item $package.FullName (Join-Path $payloadDirectory "Chess.msix") -Force

$certificateBytes = $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
[IO.File]::WriteAllBytes((Join-Path $payloadDirectory "Chess.cer"), $certificateBytes)

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
    throw "ISCC.exe was not found. Install Inno Setup 6 or 7 before creating the installer."
}

$innoCompilerPath = if ($innoCompiler.PSObject.Properties.Name -contains "Source") { $innoCompiler.Source } else { $innoCompiler.FullName }
$innoArguments = @("/DAppVersion=$Version")
if ($testCertificate) {
    $innoArguments += "/DTestCertificate"
}
$innoArguments += (Join-Path $PSScriptRoot "Chess-Packaged.iss")
& $innoCompilerPath @innoArguments
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

if ($testCertificate) {
    Remove-Item $packageCertificatePath -Force
}