param(
    [string]$Before = 'docs/loger-2/evidence/real-data/baseline.json',
    [string]$After = 'docs/loger-2/evidence/real-data/after-trc-support.json',
    [string]$Output = 'docs/loger-2/evidence/real-data/comparison-trc-support.json'
)
$ErrorActionPreference = 'Stop'
$baseline = Get-Content -LiteralPath $Before -Raw -Encoding UTF8 | ConvertFrom-Json
$current = Get-Content -LiteralPath $After -Raw -Encoding UTF8 | ConvertFrom-Json
$fields = @('Scenario','Input','Description','Kind','Format','Status','RowsWritten','Error','Reason','Created','Expected','Failed','Hash','Outputs')
if (-not $baseline.InputsUnchanged -or -not $current.InputsUnchanged) { throw 'Fixtures changed.' }
if (($baseline.Inputs | ConvertTo-Json -Compress) -cne ($current.Inputs | ConvertTo-Json -Compress)) { throw 'Fixture hashes differ.' }
if (@($baseline.Scenarios).Count -ne @($current.Scenarios).Count) { throw 'Scenario sets differ.' }
$fixed = [Collections.Generic.List[object]]::new()
$preserved = 0
for ($index = 0; $index -lt @($baseline.Scenarios).Count; $index++) {
    $oldCase = $baseline.Scenarios[$index]
    $newCase = $current.Scenarios[$index]
    if ($oldCase.Status -eq 'FAIL' -and $oldCase.Input -like 'DST\*.trc') {
        if ($oldCase.Input -cne $newCase.Input -or $oldCase.Description -cne $newCase.Description -or
            $oldCase.Scenario -cne $newCase.Scenario -or $newCase.Format -ne 'Csv' -or
            $newCase.Status -ne 'PASS' -or $newCase.RowsWritten -le 0 -or -not $newCase.Hash) { throw 'DST recovery verification failed.' }
        $fixed.Add(@{ Input=$newCase.Input; RowsWritten=$newCase.RowsWritten; Sha256=$newCase.Hash })
    } else {
        $oldShape = $oldCase | Select-Object $fields | ConvertTo-Json -Depth 8 -Compress
        $newShape = $newCase | Select-Object $fields | ConvertTo-Json -Depth 8 -Compress
        if ($oldShape -cne $newShape) { throw "Unexpected result change: $($oldCase.Scenario) $($oldCase.Input)" }
        if ($oldCase.Status -eq 'PASS') { $preserved++ }
    }
}
if ($fixed.Count -ne 9 -or $current.Failed -ne 0) { throw 'All nine DST files must succeed.' }
@{ PublishedCoreSha256=$current.CoreAssemblySha256; BaselineCoreSha256=$baseline.CoreAssemblySha256;
   PreviousSuccessfulCasesPreserved=$preserved; FixedDstFiles=@($fixed.ToArray());
   CurrentPassed=$current.Passed; CurrentFailed=$current.Failed; Unmatched=$current.Unmatched;
   InputsUnchanged=$true; InputsEqual=$true; Method='Exactly nine formerly failed DST inputs become successful; all other statuses, row counts, output hashes and batch results remain equal to bfbea81.' } |
   ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Output -Encoding UTF8
Write-Output "PASS: 9 DST files fixed; $preserved previous successful cases preserved; $($current.Unmatched) unmatched inputs unchanged."
