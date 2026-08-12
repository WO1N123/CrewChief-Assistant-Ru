$ErrorActionPreference = "Stop"

$env:DOTNET_CLI_UI_LANGUAGE = "en-US"
$env:NUGET_XMLDOC_MODE = "skip"

$script:BuildLogPath = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) "build.log"
$script:TranscriptStarted = $false

try {
    Start-Transcript `
        -LiteralPath $script:BuildLogPath `
        -Force | Out-Null

    $script:TranscriptStarted = $true
}
catch {
    Write-Host "Warning: unable to start build transcript." -ForegroundColor Yellow
}

$filesRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = Split-Path -Parent $filesRoot

$appProject = Join-Path $filesRoot "src\CrewChiefRUAssistant\CrewChiefRUAssistant.csproj"
$installerProject = Join-Path $filesRoot "src\CrewChiefRUAssistant.Installer\CrewChiefRUAssistant.Installer.csproj"
$payloadPath = Join-Path $filesRoot "src\CrewChiefRUAssistant.Installer\Payload\payload.zip"
$payloadDirectory = Split-Path -Parent $payloadPath

$buildRoot = Join-Path $filesRoot ".build"
$appPublish = Join-Path $buildRoot "app"
$payloadRoot = Join-Path $buildRoot "payload"
$installerSinglePublish = Join-Path $buildRoot "installer-single"
$outputDir = Join-Path $packageRoot "output"

$dataRoot = Join-Path $env:LOCALAPPDATA "CrewChiefRUAssistant"
$modelSource = Join-Path $dataRoot "models\vosk-model-small-ru-0.22"
$maleVoiceSource = Join-Path $dataRoot "audio\voice_bank_eugene_radio_v1"
$femaleVoiceSource = Join-Path $dataRoot "audio\voice_bank_xenia_radio_v1"

$voiceGenerator = Join-Path $filesRoot "scripts\generate-voice-bank.py"

$toolsDir = Join-Path $filesRoot "tools"
$dotnetDir = Join-Path $toolsDir "dotnet"

$builderToolsRoot = Join-Path $env:LOCALAPPDATA "CrewChiefRUAssistant\builder-tools"
$pythonBaseDir = Join-Path $builderToolsRoot "python311-portable"
$legacyPythonDir = Join-Path $toolsDir "python311"
$legacyPythonVenvDir = Join-Path $builderToolsRoot "venv311"
$downloadsDir = Join-Path $builderToolsRoot "downloads"

$pythonExe = Join-Path $pythonBaseDir "python.exe"
$pythonArchive = Join-Path $downloadsDir "python-3.11.9-amd64.zip"
$pythonArchiveUrl = "https://www.python.org/ftp/python/3.11.9/python-3.11.9-amd64.zip"
$pythonArchiveSha256 = "4ba90a4ab8990891033d37ff04d2047fdae8948d0d2729a68d3a6a17c585b681"

Write-Host ""
Write-Host "CrewChief RU WPF Setup Builder" -ForegroundColor Cyan
Write-Host "Builder version: 0.9.4" -ForegroundColor DarkGray
Write-Host "Builder root: $packageRoot" -ForegroundColor DarkGray
Write-Host "Target systems: Windows 10 1809+ x64 and Windows 11 x64." -ForegroundColor DarkGray
Write-Host "Safe build mode: uv and uvw are not used." -ForegroundColor DarkGray
Write-Host ""

function Find-Or-InstallDotnet {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue

    if ($command) {
        return [string]$command.Source
    }

    $local = Join-Path $dotnetDir "dotnet.exe"

    if (Test-Path -LiteralPath $local) {
        return [string]$local
    }

    New-Item -ItemType Directory -Force -Path $dotnetDir | Out-Null
    $installer = Join-Path $env:TEMP "dotnet-install-crewchief.ps1"

    Write-Host "Downloading .NET 8 SDK..." -ForegroundColor Yellow

    Invoke-WebRequest `
        -Uri "https://dot.net/v1/dotnet-install.ps1" `
        -OutFile $installer `
        -UseBasicParsing

    & powershell.exe `
        -NoProfile `
        -ExecutionPolicy Bypass `
        -File $installer `
        -Channel "8.0" `
        -InstallDir $dotnetDir |
        Out-Host

    if ($LASTEXITCODE -ne 0 -or
        -not (Test-Path -LiteralPath $local)) {
        throw "Unable to install .NET 8 SDK."
    }

    return [string]$local
}

function Invoke-NativeProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Executable,

        [Parameter(Mandatory = $false)]
        [string[]]$Arguments = @(),

        [Parameter(Mandatory = $false)]
        [bool]$ShowOutput = $true
    )

    $oldPreference =
        $ErrorActionPreference

    try {
        # PowerShell 5.1 converts stderr from native programs into error
        # records. With ErrorActionPreference=Stop that aborts the script
        # before LASTEXITCODE can be checked. Native programs must be judged
        # by their own exit code instead.
        $ErrorActionPreference =
            "Continue"

        if ($ShowOutput) {
            & $Executable @Arguments 2>&1 |
                Out-Host
        }
        else {
            & $Executable @Arguments *> $null
        }

        return [int]$LASTEXITCODE
    }
    finally {
        $ErrorActionPreference =
            $oldPreference
    }
}

function Test-PythonRuntime {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Executable,

        [Parameter(Mandatory = $false)]
        [bool]$ShowOutput = $false
    )

    if (-not (Test-Path -LiteralPath $Executable)) {
        return $false
    }

    $oldPythonHome = $env:PYTHONHOME
    $oldPythonPath = $env:PYTHONPATH
    $oldNoUserSite = $env:PYTHONNOUSERSITE

    try {
        $runtimeRoot = Split-Path -Parent $Executable
        $env:PYTHONHOME = $runtimeRoot
        $env:PYTHONPATH = $null
        $env:PYTHONNOUSERSITE = "1"

        $exitCode =
            Invoke-NativeProcess `
                -Executable $Executable `
                -Arguments @(
                    "-I",
                    "-c",
                    "import struct, sys; assert sys.version_info[:2] == (3, 11); assert struct.calcsize('P') * 8 == 64; print(sys.executable); print(sys.version)"
                ) `
                -ShowOutput $ShowOutput

        return $exitCode -eq 0
    }
    catch {
        return $false
    }
    finally {
        $env:PYTHONHOME = $oldPythonHome
        $env:PYTHONPATH = $oldPythonPath
        $env:PYTHONNOUSERSITE = $oldNoUserSite
    }
}

function Test-FileSha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedHash
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $actualHash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    return $actualHash -eq $ExpectedHash.ToLowerInvariant()
}

function Set-PortablePythonEnvironment {
    $env:PYTHONHOME = $pythonBaseDir
    $env:PYTHONPATH = $null
    $env:PYTHONNOUSERSITE = "1"
    $env:PYTHONDONTWRITEBYTECODE = "1"
    $env:PYTHONUTF8 = "1"
    $env:PIP_DISABLE_PIP_VERSION_CHECK = "1"
}

function Ensure-PrivatePip {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Executable
    )

    Set-PortablePythonEnvironment

    $pipExit =
        Invoke-NativeProcess `
            -Executable $Executable `
            -Arguments @(
                "-m",
                "pip",
                "--version"
            ) `
            -ShowOutput $true

    if ($pipExit -eq 0) {
        return
    }

    Write-Host "Bootstrapping pip inside the portable Python runtime..." -ForegroundColor Yellow

    $ensurePipExit =
        Invoke-NativeProcess `
            -Executable $Executable `
            -Arguments @(
                "-m",
                "ensurepip",
                "--upgrade",
                "--default-pip"
            ) `
            -ShowOutput $true

    if ($ensurePipExit -ne 0) {
        throw (
            "The official portable Python package has no working pip or ensurepip. " +
            "Exit code: $ensurePipExit. Delete the builder-tools folder and run the builder again."
        )
    }

    $pipExit =
        Invoke-NativeProcess `
            -Executable $Executable `
            -Arguments @(
                "-m",
                "pip",
                "--version"
            ) `
            -ShowOutput $true

    if ($pipExit -ne 0) {
        throw "pip could not be initialized in the portable Python runtime."
    }
}

function Find-Or-InstallPython {
    if (Test-PythonRuntime -Executable $pythonExe -ShowOutput $false) {
        Set-PortablePythonEnvironment
        Ensure-PrivatePip -Executable $pythonExe
        Write-Host "Using portable Python 3.11: $pythonExe" -ForegroundColor DarkGray
        return [string]$pythonExe
    }

    # Never invoke py.exe, PATH Python or Windows Installer. Old launcher and
    # registry entries may point to deleted builder folders and must not affect
    # this build. Only directories owned by CrewChiefRU Builder are touched.
    Remove-Item -LiteralPath $legacyPythonDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $legacyPythonVenvDir -Recurse -Force -ErrorAction SilentlyContinue

    New-Item -ItemType Directory -Force -Path $builderToolsRoot, $downloadsDir | Out-Null

    if (-not (Test-FileSha256 -Path $pythonArchive -ExpectedHash $pythonArchiveSha256)) {
        Remove-Item -LiteralPath $pythonArchive -Force -ErrorAction SilentlyContinue

        Write-Host "Downloading official portable Python 3.11.9 package..." -ForegroundColor Yellow
        Write-Host "Source: python.org" -ForegroundColor DarkGray

        Invoke-WebRequest `
            -Uri $pythonArchiveUrl `
            -OutFile $pythonArchive `
            -UseBasicParsing
    }

    if (-not (Test-FileSha256 -Path $pythonArchive -ExpectedHash $pythonArchiveSha256)) {
        $actualHash = "missing"

        if (Test-Path -LiteralPath $pythonArchive) {
            $actualHash = (Get-FileHash -LiteralPath $pythonArchive -Algorithm SHA256).Hash
        }

        Remove-Item -LiteralPath $pythonArchive -Force -ErrorAction SilentlyContinue
        throw (
            "Portable Python archive integrity check failed. " +
            "Expected SHA-256: $pythonArchiveSha256. Received: $actualHash"
        )
    }

    Write-Host "Portable Python SHA-256 verified." -ForegroundColor DarkGray

    $stagingDir = "$pythonBaseDir.staging-$PID"
    Remove-Item -LiteralPath $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null

    Write-Host "Extracting private Python runtime..." -ForegroundColor Yellow
    Write-Host "Target: $pythonBaseDir" -ForegroundColor DarkGray

    try {
        Expand-Archive `
            -LiteralPath $pythonArchive `
            -DestinationPath $stagingDir `
            -Force

        $stagedPython = Join-Path $stagingDir "python.exe"

        if (-not (Test-Path -LiteralPath $stagedPython)) {
            $foundPython = Get-ChildItem `
                -LiteralPath $stagingDir `
                -Filter "python.exe" `
                -File `
                -Recurse | Select-Object -First 1

            if (-not $foundPython) {
                throw "The official Python archive does not contain python.exe."
            }

            $contentRoot = Split-Path -Parent $foundPython.FullName
            $normalizedDir = "$stagingDir.normalized"
            Remove-Item -LiteralPath $normalizedDir -Recurse -Force -ErrorAction SilentlyContinue
            New-Item -ItemType Directory -Force -Path $normalizedDir | Out-Null
            Get-ChildItem -LiteralPath $contentRoot -Force | Move-Item -Destination $normalizedDir
            Remove-Item -LiteralPath $stagingDir -Recurse -Force
            Move-Item -LiteralPath $normalizedDir -Destination $stagingDir
            $stagedPython = Join-Path $stagingDir "python.exe"
        }

        if (-not (Test-PythonRuntime -Executable $stagedPython -ShowOutput $true)) {
            throw "The extracted portable Python runtime cannot be started."
        }

        Remove-Item -LiteralPath $pythonBaseDir -Recurse -Force -ErrorAction SilentlyContinue
        Move-Item -LiteralPath $stagingDir -Destination $pythonBaseDir
    }
    catch {
        Remove-Item -LiteralPath $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
        throw
    }

    if (-not (Test-PythonRuntime -Executable $pythonExe -ShowOutput $true)) {
        throw "Portable Python was extracted, but python.exe cannot be started: $pythonExe"
    }

    Set-PortablePythonEnvironment
    Ensure-PrivatePip -Executable $pythonExe

    Write-Host "Portable Python ready: $pythonExe" -ForegroundColor Green
    return [string]$pythonExe
}

function Ensure-VoskModel {
    if (Test-Path -LiteralPath $modelSource) {
        return
    }

    Write-Host "Downloading the Russian speech model..." -ForegroundColor Yellow

    $modelParent = Split-Path -Parent $modelSource
    $archive = Join-Path $dataRoot "vosk-model-small-ru-0.22.zip"
    $extract = Join-Path $dataRoot "vosk-model-extract"

    New-Item -ItemType Directory -Force -Path $modelParent | Out-Null
    Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $extract -Recurse -Force -ErrorAction SilentlyContinue

    Invoke-WebRequest `
        -Uri "https://alphacephei.com/vosk/models/vosk-model-small-ru-0.22.zip" `
        -OutFile $archive `
        -UseBasicParsing

    Expand-Archive `
        -LiteralPath $archive `
        -DestinationPath $extract `
        -Force

    $extracted = Get-ChildItem `
        -LiteralPath $extract `
        -Directory |
        Where-Object {
            $_.Name -eq "vosk-model-small-ru-0.22"
        } |
        Select-Object -First 1

    if (-not $extracted) {
        throw "The downloaded archive does not contain the Vosk model."
    }

    Remove-Item -LiteralPath $modelSource -Recurse -Force -ErrorAction SilentlyContinue
    Move-Item -LiteralPath $extracted.FullName -Destination $modelSource

    Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $extract -Recurse -Force -ErrorAction SilentlyContinue
}

function Clear-ProjectBuildArtifacts {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    $projectDirectory = Split-Path -Parent $ProjectPath

    foreach ($name in @("bin", "obj")) {
        Remove-Item `
            -LiteralPath (Join-Path $projectDirectory $name) `
            -Recurse `
            -Force `
            -ErrorAction SilentlyContinue
    }
}

function Voice-IsReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return `
        (Test-Path -LiteralPath (Join-Path $Path "READY.json")) -and `
        (Test-Path -LiteralPath (Join-Path $Path "phrases\preview.wav")) -and `
        (Test-Path -LiteralPath (Join-Path $Path "phrases\unknown.wav")) -and `
        (Test-Path -LiteralPath (Join-Path $Path "phrases\track_monza.wav")) -and `
        (Test-Path -LiteralPath (Join-Path $Path "phrases\track_temperature.wav")) -and `
        (Test-Path -LiteralPath (Join-Path $Path "phrases\air_temperature.wav")) -and `
        (Test-Path -LiteralPath (Join-Path $Path "phrases\damage_none.wav")) -and `
        (Test-Path -LiteralPath (Join-Path $Path "phrases\battery_charge.wav")) -and `
        (Test-Path -LiteralPath (Join-Path $Path "phrases\leader_completed.wav")) -and `
        (Test-Path -LiteralPath (Join-Path $Path "phrases\leader_completed_self.wav")) -and `
        (Test-Path -LiteralPath (Join-Path $Path "phrases\incident_ahead.wav")) -and `
        (Test-Path -LiteralPath (Join-Path $Path "phrases\incident_behind.wav")) -and `
        (Test-Path -LiteralPath (Join-Path $Path "phrases\abs_level.wav")) -and `
        (Test-Path -LiteralPath (Join-Path $Path "phrases\tc_level.wav"))
}

function Ensure-VoiceBanks {
    if ((Voice-IsReady -Path $maleVoiceSource) -and
        (Voice-IsReady -Path $femaleVoiceSource)) {
        return
    }

    $python = Find-Or-InstallPython

    Write-Host "Installing voice generator packages..." -ForegroundColor Yellow

    $toolsExit =
        Invoke-NativeProcess `
            -Executable $python `
            -Arguments @(
                "-m",
                "pip",
                "install",
                "--disable-pip-version-check",
                "--upgrade",
                "pip",
                "setuptools",
                "wheel"
            ) `
            -ShowOutput $true

    if ($toolsExit -ne 0) {
        throw (
            "Unable to update the private Python package tools. " +
            "Exit code: $toolsExit"
        )
    }

    # scipy is required by the Silero model package but is not always pulled
    # in automatically by third-party dependency metadata.
    $packagesExit =
        Invoke-NativeProcess `
            -Executable $python `
            -Arguments @(
                "-m",
                "pip",
                "install",
                "--disable-pip-version-check",
                "numpy",
                "scipy",
                "silero-tts"
            ) `
            -ShowOutput $true

    if ($packagesExit -ne 0) {
        throw (
            "Unable to install voice generator packages. " +
            "Exit code: $packagesExit"
        )
    }

    Write-Host "Creating Eugene and Xenia voice banks..." -ForegroundColor Yellow

    $generatorExit =
        Invoke-NativeProcess `
            -Executable $python `
            -Arguments @(
                $voiceGenerator,
                "--speaker",
                "all"
            ) `
            -ShowOutput $true

    if ($generatorExit -ne 0) {
        throw (
            "Unable to create voice banks. " +
            "Exit code: $generatorExit"
        )
    }

    if (-not (Voice-IsReady -Path $maleVoiceSource) -or
        -not (Voice-IsReady -Path $femaleVoiceSource)) {
        throw "One or both voice banks were not created."
    }
}

try {
    # Remove files left by older uv-based builder versions.
    Remove-Item `
        -LiteralPath (Join-Path $toolsDir "uv") `
        -Recurse `
        -Force `
        -ErrorAction SilentlyContinue

    Remove-Item `
        -LiteralPath (Join-Path $toolsDir "cache") `
        -Recurse `
        -Force `
        -ErrorAction SilentlyContinue

    Ensure-VoskModel
    Ensure-VoiceBanks

    $dotnet = Find-Or-InstallDotnet

    # A stale apphost or Win32 resource in bin/obj can survive normal publish
    # and recreate the side-by-side startup error. Clean both projects first.
    Clear-ProjectBuildArtifacts -ProjectPath $appProject
    Clear-ProjectBuildArtifacts -ProjectPath $installerProject

    Remove-Item -LiteralPath $buildRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $outputDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $payloadPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $payloadDirectory ".keep") -Force -ErrorAction SilentlyContinue

    New-Item -ItemType Directory -Force -Path `
        $appPublish, `
        $payloadRoot, `
        $installerSinglePublish, `
        $outputDir, `
        $payloadDirectory | Out-Null

    Write-Host "Building the WPF application..." -ForegroundColor Yellow

    & $dotnet restore `
        $appProject `
        --runtime win-x64 `
        -p:NuGetAudit=false `
        --nologo |
        Out-Host

    if ($LASTEXITCODE -ne 0) {
        throw "Application package restore failed."
    }

    & $dotnet publish `
        $appProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --no-restore `
        --output $appPublish `
        -p:PublishSingleFile=false `
        -p:IncludeNativeLibrariesForSelfExtract=false `
        -p:EnableCompressionInSingleFile=false `
        -p:NuGetAudit=false `
        --nologo |
        Out-Host

    if ($LASTEXITCODE -ne 0) {
        throw "Application build failed."
    }

    $appPayload = Join-Path $payloadRoot "app"
    $dataPayload = Join-Path $payloadRoot "data"

    New-Item -ItemType Directory -Force -Path `
        $appPayload, `
        (Join-Path $dataPayload "models"), `
        (Join-Path $dataPayload "audio") | Out-Null

    Copy-Item `
        -Path (Join-Path $appPublish "*") `
        -Destination $appPayload `
        -Recurse `
        -Force

    if (-not (Test-Path -LiteralPath (Join-Path $appPayload "CrewChiefRUAssistant.exe"))) {
        throw "The published application executable is missing."
    }

    Copy-Item `
        -LiteralPath (Join-Path $filesRoot "COMMANDS_RU.txt") `
        -Destination $appPayload `
        -Force

    Copy-Item `
        -LiteralPath $modelSource `
        -Destination (Join-Path $dataPayload "models") `
        -Recurse `
        -Force

    Copy-Item `
        -LiteralPath $maleVoiceSource `
        -Destination (Join-Path $dataPayload "audio") `
        -Recurse `
        -Force

    Copy-Item `
        -LiteralPath $femaleVoiceSource `
        -Destination (Join-Path $dataPayload "audio") `
        -Recurse `
        -Force

    Write-Host "Packaging files..." -ForegroundColor Yellow

    Compress-Archive `
        -Path (Join-Path $payloadRoot "*") `
        -DestinationPath $payloadPath `
        -CompressionLevel Optimal `
        -Force

    if (-not (Test-Path -LiteralPath $payloadPath)) {
        throw "Installer payload.zip was not created."
    }

    Write-Host "Building the WPF Setup.exe..." -ForegroundColor Yellow

    & $dotnet restore `
        $installerProject `
        --runtime win-x64 `
        -p:NuGetAudit=false `
        --nologo |
        Out-Host

    if ($LASTEXITCODE -ne 0) {
        throw "Installer package restore failed."
    }

    & $dotnet publish `
        $installerProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --no-restore `
        --output $installerSinglePublish `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:IncludeAllContentForSelfExtract=false `
        -p:EnableCompressionInSingleFile=false `
        -p:NuGetAudit=false `
        --nologo |
        Out-Host

    if ($LASTEXITCODE -ne 0) {
        throw "Single-file installer build failed."
    }

    $setup = Join-Path $installerSinglePublish "CrewChiefRU_Setup.exe"
    $finalSetup = Join-Path $outputDir "CrewChiefRU_Setup.exe"

    if (-not (Test-Path -LiteralPath $setup)) {
        throw "Setup.exe was not created."
    }

    Copy-Item `
        -LiteralPath $setup `
        -Destination $finalSetup `
        -Force

    # Release output is intentionally minimal: only the finished installer.
    Get-ChildItem -LiteralPath $outputDir -Force |
        Where-Object { $_.FullName -ne $finalSetup } |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    Remove-Item -LiteralPath $buildRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $payloadPath -Force -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Host "Done." -ForegroundColor Green
    Write-Host "Installer:"
    Write-Host $finalSetup -ForegroundColor Green
    Write-Host ""
    Write-Host "Build log:"
    Write-Host $script:BuildLogPath -ForegroundColor DarkGray
    Write-Host ""

    Start-Process explorer.exe -ArgumentList ('/select,"{0}"' -f $finalSetup)
}
catch {
    Write-Host ""
    Write-Host "Build failed:" -ForegroundColor Red
    Write-Host $_.Exception.ToString() -ForegroundColor Red
    Write-Host ""
    Write-Host "Full log:" -ForegroundColor Yellow
    Write-Host $script:BuildLogPath -ForegroundColor Yellow

    Write-Host ""

    if ($script:TranscriptStarted) {
        try {
            Stop-Transcript | Out-Null
            $script:TranscriptStarted = $false
        }
        catch {
        }
    }

    exit 1
}
finally {
    if ($script:TranscriptStarted) {
        try {
            Stop-Transcript | Out-Null
        }
        catch {
        }
    }
}
