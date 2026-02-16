# HotShallot CLI Build Script
# Publishes the CLI as a Native AOT executable

$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectPath = Join-Path $PSScriptRoot "HotShallot.Cli.csproj"
$rootPath = Resolve-Path (Join-Path $PSScriptRoot "../..")
$outputPath = Join-Path $rootPath "output"

# Ensure output directory exists
if (-not (Test-Path $outputPath)) {
    Write-Host "Creating output directory: $outputPath" -ForegroundColor Gray
    New-Item -ItemType Directory -Path $outputPath | Out-Null
}

Write-Host "Publishing HotShallot CLI (Native AOT) to $outputPath..." -ForegroundColor Cyan

# Use -o to specify the output directory
# We specify -r win-x64 for Native AOT on Windows
dotnet publish $projectPath -c Release -r win-x64 -o $outputPath --nologo

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✅ Build Succeeded!" -ForegroundColor Green
    Write-Host "Executable location: $(Join-Path $outputPath "shallot.exe")"
} else {
    Write-Host "`n❌ Build Failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}
