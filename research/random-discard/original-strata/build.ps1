param([Parameter(Mandatory)][string]$LegacyCheckout)
$ErrorActionPreference = 'Stop'
$checkout = (Resolve-Path -LiteralPath $LegacyCheckout).Path
$revision = git -C $checkout rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $revision -ne '88d7e83a969bcfa63b2bc84423f4512b50d82e4f') {
    throw 'The original-strata exporter requires engine revision 88d7e83.'
}
$source = Get-Content -Raw -LiteralPath (Join-Path $checkout 'src/CardProbability/CalculationEngine.cs')
$entry = 'CalculateCore(request, options, cancellationToken, false);'
if ($source.IndexOf($entry) -lt 0 -or $source.IndexOf($entry) -ne $source.LastIndexOf($entry)) {
    throw 'The original engine public entry point changed.'
}
$generated = Join-Path $checkout 'obj/original-strata'
[IO.Directory]::CreateDirectory($generated) | Out-Null
$entryPoint = Join-Path $generated 'CalculationEngine.cs'
[IO.File]::WriteAllText($entryPoint, $source.Replace($entry, 'OriginalStrata.Calculate(request, options ?? new());'))
$targets = Join-Path $PSScriptRoot 'Offline.targets'
Push-Location $checkout
try {
    dotnet build src/CardProbability -c OriginalStrata -p:Optimize=true "-p:CustomAfterMicrosoftCommonTargets=$targets" "-p:OriginalStrataEntryPoint=$entryPoint"
    if ($LASTEXITCODE -ne 0) { throw 'Original-strata build failed.' }
} finally {
    Pop-Location
}
