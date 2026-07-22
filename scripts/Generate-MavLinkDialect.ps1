[CmdletBinding()]
param(
    [Parameter()]
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,

    [Parameter()]
    [ValidateSet('Verify', 'Write')]
    [string] $Mode = 'Verify',

    [Parameter()]
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
$repositoryPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$manifestPath = Join-Path $repositoryPath 'src/Core/MissionPlanner.MavLink/Dialects/mavlink-generation.json'
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$dialectDirectory = Join-Path $repositoryPath 'src/Core/MissionPlanner.MavLink/Dialects'
$generatorProject = Join-Path $repositoryPath 'src/Tools/MissionPlanner.MavLink.Generator/MissionPlanner.MavLink.Generator.csproj'
$testProject = Join-Path $repositoryPath 'src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj'

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Assert-ManifestInputs {
    $declaredDialects = @($manifest.rootDialect) + @($manifest.inheritedDialects)
    foreach ($dialect in $declaredDialects) {
        $path = Join-Path $dialectDirectory $dialect
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "The generation manifest references a missing dialect: $path"
        }
    }

    $readme = Get-Content -Raw -LiteralPath (Join-Path $dialectDirectory 'README.md')
    if (-not $readme.Contains($manifest.sourceRevision, [StringComparison]::Ordinal)) {
        throw 'The vendored dialect README does not contain the manifest source revision.'
    }

    if (-not (Test-Path -LiteralPath (Join-Path $dialectDirectory 'COPYING') -PathType Leaf)) {
        throw 'The vendored MAVLink license file is missing.'
    }
}

function Compare-OrWriteGeneratedFile {
    param(
        [Parameter(Mandatory)][string] $GeneratedPath,
        [Parameter(Mandatory)][string] $CommittedRelativePath
    )

    $committedPath = Join-Path $repositoryPath $CommittedRelativePath
    if (-not (Test-Path -LiteralPath $committedPath -PathType Leaf)) {
        throw "The committed generated output is missing: $committedPath"
    }

    $generatedHash = (Get-FileHash -LiteralPath $GeneratedPath -Algorithm SHA256).Hash
    $committedHash = (Get-FileHash -LiteralPath $committedPath -Algorithm SHA256).Hash
    if ($generatedHash -eq $committedHash) {
        Write-Host "MATCH $CommittedRelativePath"
        return
    }

    if ($Mode -eq 'Write') {
        Copy-Item -LiteralPath $GeneratedPath -Destination $committedPath -Force
        Write-Host "UPDATED $CommittedRelativePath"
        return
    }

    throw "Generated artifact drift detected: $CommittedRelativePath. Run scripts/Generate-MavLinkDialect.ps1 -Mode Write and review the result."
}

Assert-ManifestInputs
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("missionplanner-mavlink-{0}" -f [Guid]::NewGuid().ToString('N'))
$resolvedTemporaryRoot = [IO.Path]::GetFullPath($temporaryRoot)
$resolvedSystemTemporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
if (-not $resolvedTemporaryRoot.StartsWith($resolvedSystemTemporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use a temporary generation directory outside the system temporary root: $resolvedTemporaryRoot"
}

New-Item -ItemType Directory -Path $resolvedTemporaryRoot | Out-Null
try {
    $registryOutput = Join-Path $resolvedTemporaryRoot 'MavLinkMessageDefinitions.g.cs'
    $enumOutput = Join-Path $resolvedTemporaryRoot 'MavLinkEnums.g.cs'
    $modelOutput = Join-Path $resolvedTemporaryRoot 'MavLinkWireMessages.g.cs'
    $decoderOutput = Join-Path $resolvedTemporaryRoot 'MavLinkWireDecoders.g.cs'
    $promotionOutput = Join-Path $resolvedTemporaryRoot 'mavlink-promotion-catalog.json'
    $coverageOutput = Join-Path $resolvedTemporaryRoot 'mavlink-coverage.json'

    Invoke-DotNet @('run', '--project', $generatorProject, '--no-restore', '--', 'registry', $repositoryPath, $registryOutput)
    Invoke-DotNet @('run', '--project', $generatorProject, '--no-restore', '--', 'enums', $repositoryPath, $enumOutput)
    Invoke-DotNet @('run', '--project', $generatorProject, '--no-restore', '--', 'wire', $repositoryPath, $modelOutput, $decoderOutput)
    Invoke-DotNet @('run', '--project', $generatorProject, '--no-restore', '--', 'promotion', $repositoryPath, $promotionOutput)
    Invoke-DotNet @('run', '--project', $generatorProject, '--no-restore', '--', $repositoryPath, $coverageOutput)

    Compare-OrWriteGeneratedFile $registryOutput $manifest.outputs.registry
    Compare-OrWriteGeneratedFile $enumOutput $manifest.outputs.enums
    Compare-OrWriteGeneratedFile $modelOutput $manifest.outputs.wireModels
    Compare-OrWriteGeneratedFile $decoderOutput $manifest.outputs.wireDecoders
    Compare-OrWriteGeneratedFile $promotionOutput $manifest.domainPromotionCatalog

    $coverage = Get-Content -Raw -LiteralPath $coverageOutput | ConvertFrom-Json
    $expectedLegacyConstants = @($manifest.knownLegacyConstants) | Sort-Object
    $reportedLegacyConstants = @($coverage.incorrectConstants) | Sort-Object
    if (($expectedLegacyConstants -join '|') -ne ($reportedLegacyConstants -join '|') -or
        @($coverage.incorrectCrcExtras).Count -ne 0 -or
        @($coverage.unknownDecoderMessageIds).Count -ne 0) {
        throw 'MAVLink coverage differs from the manifest legacy allow-list or contains invalid CRC/decoder IDs.'
    }

    Write-Host "MAVLink source revision: $($manifest.sourceRevision)"
    Write-Host "Root dialect: $($manifest.rootDialect)"
    Write-Host "Resolved messages: $(@($coverage.messages).Count)"
    $coverage.messages |
        Group-Object -Property classification |
        Sort-Object -Property Name |
        ForEach-Object { Write-Host ("Coverage {0}: {1}" -f $_.Name, $_.Count) }

    $coverageDestination = Join-Path $repositoryPath $manifest.outputs.coverage
    New-Item -ItemType Directory -Path (Split-Path $coverageDestination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $coverageOutput -Destination $coverageDestination -Force
    Write-Host "Coverage report: $coverageDestination"
}
finally {
    if (Test-Path -LiteralPath $resolvedTemporaryRoot) {
        Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force
    }
}

if (-not $SkipTests) {
    $testFilter = 'FullyQualifiedName~MavLinkMessageDefinitionRegistryTests|FullyQualifiedName~MavLinkGenerated|FullyQualifiedName~MavLinkDecoderCatalogTests|FullyQualifiedName~MavLinkRawFallbackTests|FullyQualifiedName~MavLinkConformanceFixtureTests|FullyQualifiedName~MavLinkPromotionCatalogTests'
    Invoke-DotNet @('test', $testProject, '--no-restore', '--filter', $testFilter, '--verbosity', 'minimal')
}

Write-Host "MAVLink generation $Mode completed successfully."
