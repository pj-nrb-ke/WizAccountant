$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$mdPath = Join-Path $root 'DOCS\Sage_AI_Agent_500_Common_Business_Queries_Mega_Digest.md'
$outDir = Join-Path $root 'src\WizAccountant.Api\Insight\Data'
$outPath = Join-Path $outDir 'mega-digest-catalog.json'

$domain = 'General'
$entries = New-Object System.Collections.Generic.List[object]

Get-Content $mdPath | ForEach-Object {
    $line = $_
    if ($line -match '^# ([^#].+)$' -and $line -notmatch '^## ') {
        $domain = $Matches[1].Trim()
        return
    }
    if ($line -match '^## (\d+)\. (.+)$') {
        $entries.Add([ordered]@{
            id     = [int]$Matches[1]
            domain = $domain
            title  = $Matches[2].Trim()
        })
    }
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$payload = @{
    version    = 1
    source     = 'Sage_AI_Agent_500_Common_Business_Queries_Mega_Digest.md'
    entryCount = $entries.Count
    entries    = $entries
}
$payload | ConvertTo-Json -Depth 5 | Set-Content $outPath -Encoding UTF8
Write-Host "Wrote $($entries.Count) entries to $outPath"
