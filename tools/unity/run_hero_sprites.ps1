# Builds the sprite hero, then runs its tests with image capture. Pass -Filter to change which tests run.
param([string]$WorkDir, [string]$Filter = 'ShinobiDominion.Tests.HeroSpriteTests', [switch]$SkipBuild)
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe'
$project = (Resolve-Path "$PSScriptRoot\..\..\unity").Path
New-Item -ItemType Directory -Force $WorkDir | Out-Null
if (-not $SkipBuild) {
  $b = Start-Process $unity -ArgumentList @('-batchmode','-quit','-projectPath',"`"$project`"",'-executeMethod','ShinobiDominion.EditorTools.HeroSpriteBuilder.BuildAll','-logFile',"`"$WorkDir\build.log`"") -Wait -PassThru
  "build exit $($b.ExitCode)"
  Select-String -Path "$WorkDir\build.log" -Pattern 'error CS|Exception|HERO_SPRITES_BUILT|^Built ' | Select-Object -First 12 | ForEach-Object { $_.Line.Substring(0,[Math]::Min(220,$_.Line.Length)) }
  if ($b.ExitCode -ne 0) { return }
}
$env:SD_CAPTURE_DIR = "$WorkDir\shots"
Remove-Item "$WorkDir\last.xml" -Force -ErrorAction SilentlyContinue
$p = Start-Process $unity -ArgumentList @('-batchmode','-projectPath',"`"$project`"",'-runTests','-testPlatform','PlayMode','-testFilter',$Filter,'-testResults',"`"$WorkDir\last.xml`"",'-logFile',"`"$WorkDir\last.log`"") -Wait -PassThru
"tests exit $($p.ExitCode)"
if (Test-Path "$WorkDir\last.xml") {
  $x = [xml](Get-Content "$WorkDir\last.xml"); $r = $x.'test-run'; "total=$($r.total) passed=$($r.passed) failed=$($r.failed) skipped=$($r.skipped)"
  $x.SelectNodes("//test-case[@result='Failed']") | ForEach-Object { $m = "FAIL $($_.name): $($_.failure.message.InnerText)"; if ($m.Length -gt 500) { $m.Substring(0,500) } else { $m } }
}
Select-String -Path "$WorkDir\last.log" -Pattern 'error CS' | Select-Object -First 8 | ForEach-Object { $_.Line.Substring(0,[Math]::Min(220,$_.Line.Length)) }
if (Test-Path "$WorkDir\shots\sprite_report.txt") { Get-Content "$WorkDir\shots\sprite_report.txt" }
