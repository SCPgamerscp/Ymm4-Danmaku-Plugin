[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "dist",
    [switch]$UseStubs,
    [switch]$IncludePdb
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
if (-not $scriptDir) { $scriptDir = $PSScriptRoot }
$repoRoot = (Resolve-Path "$scriptDir\..").Path

Push-Location $repoRoot
try {
    # Check if Directory.Build.props exists and has YMM4DirPath
    $hasRealYmm4 = Test-Path (Join-Path $repoRoot "Directory.Build.props")
    $buildWithStubs = $UseStubs.IsPresent -or (-not $hasRealYmm4)

    Write-Host "=== Ymm4DanmakuPlugin .ymme Packaging ===" -ForegroundColor Cyan
    Write-Host "Configuration : $Configuration"
    Write-Host "UseStubs      : $buildWithStubs"
    Write-Host "OutputDir     : $OutputDir"

    # 1. Build project
    $projectRelPath = "src/Ymm4DanmakuPlugin/Ymm4DanmakuPlugin.csproj"
    Write-Host "`n[1/3] Building plugin ($Configuration)..." -ForegroundColor Yellow

    if ($buildWithStubs) {
        dotnet build $projectRelPath -c $Configuration -p:UseYmm4Stubs=true
    } else {
        dotnet build $projectRelPath -c $Configuration
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }

    # 2. Verify output DLL
    $binDir = "src/Ymm4DanmakuPlugin/bin/$Configuration/net10.0-windows10.0.19041.0"
    $pluginDll = "$binDir/Ymm4DanmakuPlugin.dll"

    if (-not (Test-Path $pluginDll)) {
        throw "Output DLL not found: $pluginDll"
    }

    if (-not (Test-Path $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    }

    # 3. Create clean staging directory
    $tempStage = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
    New-Item -ItemType Directory -Path $tempStage -Force | Out-Null

    try {
        Copy-Item -Path $pluginDll -Destination (Join-Path $tempStage "Ymm4DanmakuPlugin.dll") -Force

        $pluginJsonPath = Join-Path $repoRoot "plugin.json"
        if (Test-Path $pluginJsonPath) {
            Copy-Item -Path $pluginJsonPath -Destination (Join-Path $tempStage "plugin.json") -Force
        }

        $readmeTxtPath = Join-Path $repoRoot "README.txt"
        if (Test-Path $readmeTxtPath) {
            Copy-Item -Path $readmeTxtPath -Destination (Join-Path $tempStage "README.txt") -Force
        }

        $readmeMdPath = Join-Path $repoRoot "README.md"
        if (Test-Path $readmeMdPath) {
            Copy-Item -Path $readmeMdPath -Destination (Join-Path $tempStage "README.md") -Force
        }

        if ($IncludePdb) {
            $pluginPdb = "$binDir/Ymm4DanmakuPlugin.pdb"
            if (Test-Path $pluginPdb) {
                Copy-Item -Path $pluginPdb -Destination (Join-Path $tempStage "Ymm4DanmakuPlugin.pdb") -Force
            }
        }

        # 4. Pack into .zip then rename to .ymme
        Write-Host "`n[2/3] Packing into .ymme..." -ForegroundColor Yellow
        $zipPath = Join-Path $OutputDir "Ymm4DanmakuPlugin.zip"
        $ymmePath = Join-Path $OutputDir "Ymm4DanmakuPlugin.ymme"

        if (Test-Path $zipPath) { Remove-Item -Path $zipPath -Force }
        if (Test-Path $ymmePath) { Remove-Item -Path $ymmePath -Force }

        Compress-Archive -Path "$tempStage\*" -DestinationPath $zipPath -Force
        Move-Item -Path $zipPath -Destination $ymmePath -Force

        $ymmeFile = Get-Item $ymmePath
        $sizeKb = [math]::Round($ymmeFile.Length / 1024, 2)

        Write-Host "`n[3/3] Successfully created .ymme package!" -ForegroundColor Green
        Write-Host "Output file : $($ymmeFile.FullName)" -ForegroundColor Green
        Write-Host "File size   : $sizeKb KB ($($ymmeFile.Length) bytes)" -ForegroundColor Green

        # Copy directly to YMM4 user plugin folder if installed
        $ymm4UserPluginDir = "C:\YukkuriMovieMaker4-20231229T073048Z-001\YukkuriMovieMaker4\user\plugin\Ymm4DanmakuPlugin"
        if (Test-Path (Split-Path -Parent $ymm4UserPluginDir)) {
            if (-not (Test-Path $ymm4UserPluginDir)) {
                New-Item -ItemType Directory -Path $ymm4UserPluginDir -Force | Out-Null
            }
            try {
                Copy-Item -Path $pluginDll -Destination "$ymm4UserPluginDir\Ymm4DanmakuPlugin.dll" -Force
                if (Test-Path $pluginJsonPath) { Copy-Item -Path $pluginJsonPath -Destination "$ymm4UserPluginDir\plugin.json" -Force }
                if (Test-Path $readmeTxtPath) { Copy-Item -Path $readmeTxtPath -Destination "$ymm4UserPluginDir\README.txt" -Force }
                if (Test-Path $readmeMdPath) { Copy-Item -Path $readmeMdPath -Destination "$ymm4UserPluginDir\README.md" -Force }
                Write-Host "Updated YMM4 user plugin directory: $ymm4UserPluginDir" -ForegroundColor Cyan
            } catch {
                Write-Host "Note: YMM4 is currently running. Close YMM4 and re-run pack.ps1 (or install dist/Ymm4DanmakuPlugin.ymme) to update the running instance." -ForegroundColor Yellow
            }
        }
    } finally {
        if (Test-Path $tempStage) {
            Remove-Item -Path $tempStage -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
} finally {
    Pop-Location
}
