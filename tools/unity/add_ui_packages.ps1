# Adds each UI package to the manifest with its official git URL and pinned version, one at a time.
# A package that fails to resolve or compile is taken out again and the run stops there.
param([string]$WorkDir)
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe'
$project = (Resolve-Path "$PSScriptRoot\..\..\unity").Path
$manifest = "$project\Packages\manifest.json"
New-Item -ItemType Directory -Force $WorkDir | Out-Null
$packages = @(
  @('com.annulusgames.lit-motion', 'https://github.com/annulusgames/LitMotion.git?path=src/LitMotion/Assets/LitMotion#v2.0.2'),
  @('com.coffee.ui-effect', 'https://github.com/mob-sakai/UIEffect.git?path=Packages/src#5.11.8'),
  @('com.coffee.softmask-for-ugui', 'https://github.com/mob-sakai/SoftMaskForUGUI.git?path=Packages/src#3.6.6')
)
foreach ($p in $packages) {
  $before = [IO.File]::ReadAllText($manifest)
  if ($before.Contains($p[0])) { "$($p[0]) already listed"; continue }
  $newline = if ($before.Contains("`r`n")) { "`r`n" } else { "`n" }
  $marker = "`"dependencies`": {$newline"
  if (-not $before.Contains($marker)) { "manifest format not recognised, stopping"; break }
  $after = $before.Replace($marker, "$marker    `"$($p[0])`": `"$($p[1])`",$newline")
  [IO.File]::WriteAllText($manifest, $after)
  $log = "$WorkDir\$($p[0]).log"
  $r = Start-Process $unity -ArgumentList @('-batchmode','-quit','-projectPath',"`"$project`"",'-logFile',"`"$log`"") -Wait -PassThru
  $errors = @(Select-String -Path $log -Pattern 'error CS|An error occurred while resolving|Compilation failed' | Select-Object -First 5)
  "$($p[0]) exit $($r.ExitCode), errors $($errors.Count)"
  $errors | ForEach-Object { $_.Line.Substring(0,[Math]::Min(220,$_.Line.Length)) }
  if ($r.ExitCode -ne 0 -or $errors.Count -gt 0) {
    [IO.File]::WriteAllText($manifest, $before)
    "$($p[0]) removed again, stopping"
    break
  }
}
"PACKAGES_DONE"
