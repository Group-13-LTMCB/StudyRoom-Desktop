# Automated Build, Test, Publish, Package and Release script for StudyRoom v1.0.0
[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$SlnPath = Join-Path $RootDir "StudyRoom.sln"
$UiProjPath = Join-Path $RootDir "src\StudyRoom.UI\StudyRoom.UI.csproj"
$SetupIssPath = Join-Path $RootDir "setup.iss"
$ReleaseDir = Join-Path $RootDir "release\v$Version"
$PublishDir = Join-Path $RootDir "src\StudyRoom.UI\bin\$Configuration\net8.0-windows\$Runtime\publish"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  StudyRoom Desktop - Automated Release Builder v$Version" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Run Tests
if (-not $SkipTests) {
    Write-Host "`n[1/5] Running automated test suite ($Configuration)..." -ForegroundColor Yellow
    dotnet test $SlnPath -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Test run failed with exit code $LASTEXITCODE. Aborting release."
        exit $LASTEXITCODE
    }
    Write-Host "All tests passed successfully." -ForegroundColor Green
} else {
    Write-Host "`n[1/5] Skipping tests as requested." -ForegroundColor DarkYellow
}

# 2. Publish Single-File Executable
Write-Host "`n[2/5] Publishing Single-File Executable ($Runtime, Self-Contained, Compressed)..." -ForegroundColor Yellow
dotnet publish $UiProjPath -c $Configuration -r $Runtime --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed with exit code $LASTEXITCODE. Aborting release."
    exit $LASTEXITCODE
}
Write-Host "Publish completed: $PublishDir" -ForegroundColor Green

# Ensure Release output directory exists
if (-not (Test-Path $ReleaseDir)) {
    New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null
}

# 3. Locate Inno Setup Compiler
Write-Host "`n[3/5] Locating Inno Setup Compiler (ISCC.exe)..." -ForegroundColor Yellow
$IsccCandidates = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)

$IsccPath = $null
foreach ($path in $IsccCandidates) {
    if ($path -and (Test-Path $path)) {
        $IsccPath = $path
        break
    }
}

if (-not $IsccPath) {
    Write-Error "Inno Setup Compiler (ISCC.exe) not found. Please install Inno Setup 6."
    exit 1
}
Write-Host "Found ISCC at: $IsccPath" -ForegroundColor Green

# 4. Compile Inno Setup Script
Write-Host "`n[4/5] Compiling Inno Setup package (StudyRoomSetup.exe)..." -ForegroundColor Yellow
& $IsccPath "/DMyAppVersion=$Version" "/DSourceDir=$PublishDir" "/DOutputDir=$ReleaseDir" $SetupIssPath
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup compilation failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}
Write-Host "Installer created successfully in $ReleaseDir" -ForegroundColor Green

# Copy portable executable to release directory
$PortableExe = Join-Path $ReleaseDir "StudyRoom-v$Version-win-x64-portable.exe"
Copy-Item (Join-Path $PublishDir "StudyRoom.UI.exe") -Destination $PortableExe -Force
Write-Host "Copied portable executable: $PortableExe" -ForegroundColor Green

$SettingsFile = Join-Path $PublishDir "appsettings.json"
if (Test-Path $SettingsFile) {
    Copy-Item $SettingsFile -Destination (Join-Path $ReleaseDir "appsettings.json") -Force
}

# 5. Generate SHA-256 Checksums
Write-Host "`n[5/5] Generating SHA-256 Checksums..." -ForegroundColor Yellow
$ChecksumFile = Join-Path $ReleaseDir "CHECKSUMS.txt"
$Artifacts = Get-ChildItem -Path $ReleaseDir -Filter "*.exe"

$ChecksumLines = foreach ($artifact in $Artifacts) {
    $hash = (Get-FileHash -Path $artifact.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($artifact.Name)"
}
$ChecksumLines | Set-Content -Path $ChecksumFile -Encoding utf8
Write-Host "Checksums written to $ChecksumFile" -ForegroundColor Green
Get-Content $ChecksumFile | ForEach-Object { Write-Host "  $_" -ForegroundColor Cyan }

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  RELEASE v$Version BUILT & PACKAGED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
