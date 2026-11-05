# Proves two things that need the server itself to change, so they cannot live inside go test:
# 1. Hard-killing the server mid-load loses nothing and grants nothing.
# 2. With development mode off, dev calls do not exist and players start with no free heroes.
$ErrorActionPreference = 'Continue'
Set-Location (Split-Path $PSScriptRoot -Parent)

Write-Output '=== Crash recovery'
$job = Start-Job -ScriptBlock {
    param($dir)
    Set-Location $dir
    docker compose run --rm -e SD_RECOVERY_TEST=1 tests test -count=1 -v -timeout=500s -run TestCrashRecovery ./... 2>&1 | ForEach-Object { "$_" }
} -ArgumentList (Get-Location).Path
# Wait until the test has really started, or the kill lands before there is any load.
$output = @()
for ($i = 0; $i -lt 240; $i++) {
    $output += Receive-Job $job
    if ($output -match '=== RUN') { break }
    Start-Sleep -Milliseconds 500
}
Start-Sleep -Seconds 4
docker kill --signal=SIGKILL shinobi-dominion-nakama-1 | Out-Null
Write-Output 'Server killed mid-load.'
Start-Sleep -Seconds 4
docker start shinobi-dominion-nakama-1 | Out-Null
Wait-Job $job -Timeout 520 | Out-Null
$output += Receive-Job $job
$output | Select-String -Pattern '^(=== RUN|--- |PASS|FAIL|ok|\s+integration_test)' | ForEach-Object { $_.Line }
Remove-Job $job -Force

Write-Output '=== Production mode'
$previous = @{ dev = $env:SD_DEV_MODE; rate = $env:SD_RATE_PER_SEC; burst = $env:SD_RATE_BURST }
$env:SD_DEV_MODE = 'false'; $env:SD_RATE_PER_SEC = '5'; $env:SD_RATE_BURST = '20'
docker compose up -d --wait nakama | Out-Null
docker compose run --rm -e SD_PRODUCTION_TEST=1 tests test -count=1 -v -run TestProductionMode ./... 2>&1 | ForEach-Object { "$_" } | Select-String -Pattern '^(=== RUN|--- |PASS|FAIL|ok|\s+integration_test)' | ForEach-Object { $_.Line }
$env:SD_DEV_MODE = $previous.dev; $env:SD_RATE_PER_SEC = $previous.rate; $env:SD_RATE_BURST = $previous.burst
docker compose up -d --wait nakama | Out-Null
Write-Output 'Restored development mode.'
