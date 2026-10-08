param(
    [string]$Before = 'docs/loger-2/evidence/real-data/baseline.json',
    [string]$After = 'docs/loger-2/evidence/real-data/current.json',
    [string]$Output = 'docs/loger-2/evidence/real-data/comparison.json'
)
$ErrorActionPreference = 'Stop'
$baseline = Get-Content -LiteralPath $Before -Raw -Encoding UTF8 | ConvertFrom-Json
$current = Get-Content -LiteralPath $After -Raw -Encoding UTF8 | ConvertFrom-Json
$fields = @('Scenario','Input','Description','Kind','Format','Status','RowsWritten','Error','Reason','Created','Expected','Failed','Hash','Outputs')
$beforeCases = $baseline.Scenarios | Select-Object $fields | ConvertTo-Json -Depth 8 -Compress
$afterCases = $current.Scenarios | Select-Object $fields | ConvertTo-Json -Depth 8 -Compress
$inputsEqual = ($baseline.Inputs | ConvertTo-Json -Compress) -ceq ($current.Inputs | ConvertTo-Json -Compress)
$casesEqual = $beforeCases -ceq $afterCases
$descriptionsEqual = ($baseline.Descriptions | ConvertTo-Json -Depth 8 -Compress) -ceq ($current.Descriptions | ConvertTo-Json -Depth 8 -Compress)
$inventoryEqual = ($baseline.Inventory | ConvertTo-Json -Compress) -ceq ($current.Inventory | ConvertTo-Json -Compress)
$unchanged = $baseline.InputsUnchanged -and $current.InputsUnchanged
@{
    BaselineCoreSha256 = $baseline.CoreAssemblySha256
    CurrentCoreSha256 = $current.CoreAssemblySha256
    SourceFiles = @($current.Inputs).Count
    InputsEqual = $inputsEqual; InputsUnchanged = $unchanged
    DescriptionsEqual = $descriptionsEqual; InventoryEqual = $inventoryEqual; ScenariosEqual = $casesEqual
    SuccessfulCases = $current.Passed; FailedCasesInBoth = $current.Failed; UnmatchedInputs = $current.Unmatched
    Method = 'Compare input/configuration hashes, classifications, statuses, row counts, output hashes and batch counts; exclude elapsed time. Equality includes failures and does not imply all files are supported.'
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Output -Encoding UTF8
if (-not ($inputsEqual -and $casesEqual -and $descriptionsEqual -and $inventoryEqual -and $unchanged)) { throw 'Real-data baseline comparison differs.' }
Write-Output "Parity PASS: $($current.Passed) successful cases; $($current.Failed) identical failures; $($current.Unmatched) unmatched inputs."
