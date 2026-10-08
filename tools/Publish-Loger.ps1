param(
    [string]$OutputPath = 'artifacts/release/win-x64',
    [string]$RuntimeIdentifier = 'win-x64',
    [string]$LocalPackageFeed,
    [string]$PackageFallback
)
$ErrorActionPreference = 'Stop'
$publishRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publishProject = Join-Path $publishRoot 'logReader.UI/logReader.UI.csproj'
$publishTargets = Join-Path $publishRoot 'tools/UiOnlyReadyToRun.targets'
$publishOutput = [IO.Path]::GetFullPath((Join-Path $publishRoot $OutputPath))
$previousPackages = $env:NUGET_PACKAGES
try {
    # Tool/compiler extraction stays in the project; the fallback cache is read-only input.
    $env:NUGET_PACKAGES = Join-Path $publishRoot 'artifacts/r2r/packages'
    $publishArguments = @('publish', $publishProject, '-c', 'Release', '-r', $RuntimeIdentifier,
        '--self-contained', 'false', '-p:PublishReadyToRun=true',
        "-p:CustomAfterMicrosoftCommonTargets=$publishTargets", '-o', $publishOutput)
    if ($LocalPackageFeed) {
        $publishArguments += @('--source', [IO.Path]::GetFullPath($LocalPackageFeed))
    }
    if ($PackageFallback) {
        $fallbackPath = [IO.Path]::GetFullPath($PackageFallback)
        $publishArguments += @("-p:RestoreFallbackFolders=$fallbackPath", '--source', $fallbackPath)
    }
    & dotnet @publishArguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }

    $compiledDirectory = Join-Path $publishRoot ('logReader.UI/bin/Release/net10.0-windows/' + $RuntimeIdentifier)
    foreach ($dependency in Get-ChildItem -LiteralPath $publishOutput -Filter '*.dll' -File) {
        if ($dependency.Name -eq 'LOGER.dll') { continue }
        $originalDependency = Join-Path $compiledDirectory $dependency.Name
        if (-not (Test-Path -LiteralPath $originalDependency)) { throw "Missing IL dependency: $originalDependency" }
        $beforeHash = (Get-FileHash -LiteralPath $originalDependency -Algorithm SHA256).Hash
        $afterHash = (Get-FileHash -LiteralPath $dependency.FullName -Algorithm SHA256).Hash
        if ($beforeHash -ne $afterHash) { throw "Non-UI assembly changed during publish: $($dependency.Name)" }
    }
    Write-Output "ReadyToRun UI published to $publishOutput; all non-UI DLLs retain their compiled IL bytes."
}
finally { $env:NUGET_PACKAGES = $previousPackages }
