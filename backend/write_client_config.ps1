# Writes the game's local server settings from backend/.env. The output is gitignored.
$here = $PSScriptRoot
$key = (Get-Content "$here\.env" | Where-Object { $_ -match '^SD_SERVER_KEY=' }) -replace '^SD_SERVER_KEY=', ''
if (-not $key) { throw 'SD_SERVER_KEY is missing from backend/.env' }
$target = Join-Path $here '..\unity\Assets\_Game\Resources\Config\server.local.json'
$json = @{ scheme = 'http'; host = '127.0.0.1'; port = 7350; serverKey = $key } | ConvertTo-Json
[IO.File]::WriteAllText($target, $json)
Write-Output 'Client config written.'
