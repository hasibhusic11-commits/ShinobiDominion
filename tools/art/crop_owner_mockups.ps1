param([string]$Source = "$env:USERPROFILE\Downloads\ShinobiDominion_Screen_Redesigns_Available\ShinobiDominion_Screen_Redesigns")
# Cuts the painted scenery and the gift box out of the owner's screen mockups (2026-11-10) so the rebuilt screens
# can stand in front of the same art. Each piece fades to clear on the side where it meets the interface, so it
# blends into the dark panel behind it. Run from anywhere; writes into unity/Assets/_Game/Resources/UI.
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot "..\..\unity\Assets\_Game\Resources\UI"

function Cut([string]$file, [int]$x, [int]$y, [int]$w, [int]$h, [string]$name, [string]$fade, [double]$share) {
  $src = [System.Drawing.Bitmap]::FromFile((Join-Path $Source $file))
  $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.DrawImage($src, (New-Object System.Drawing.Rectangle 0, 0, $w, $h), (New-Object System.Drawing.Rectangle $x, $y, $w, $h), [System.Drawing.GraphicsUnit]::Pixel)
  $g.Dispose(); $src.Dispose()
  $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
  $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $bytes = New-Object byte[] ($data.Stride * $h)
  [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
  for ($py = 0; $py -lt $h; $py++) {
    for ($px = 0; $px -lt $w; $px++) {
      $a = 1.0
      switch ($fade) {
        "right" { $t = ($w - 1 - $px) / ($w * $share); if ($t -lt 1) { $a = [math]::Max(0, $t) } }
        "left"  { $t = $px / ($w * $share); if ($t -lt 1) { $a = [math]::Max(0, $t) } }
        "round" {
          $dx = ($px - $w / 2.0) / ($w / 2.0); $dy = ($py - $h / 2.0) / ($h / 2.0)
          $d = [math]::Sqrt($dx * $dx + $dy * $dy)
          if ($d -gt (1 - $share)) { $a = [math]::Max(0, (1 - $d) / $share) }
        }
      }
      if ($a -lt 1) {
        $i = $py * $data.Stride + $px * 4 + 3
        # Eased, so the edge has no visible line.
        $bytes[$i] = [byte]([math]::Round($bytes[$i] * $a * $a * (3 - 2 * $a)))
      }
    }
  }
  [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
  $bmp.UnlockBits($data)
  $bmp.Save((Join-Path $out "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  "wrote $name.png ($w x $h)"
}

# The castle and blossom strip down the left of the Village Siege mockup.
Cut "01_Village_Siege.png" 0 80 300 861 "scene_castle" "right" 0.22
# The moonlit duelling ground on the right of the Arena mockup.
Cut "02_Arena.png" 1050 200 622 741 "scene_arena" "left" 0.16
# The lantern and blossom strip down the left of the Rankings mockup, below its banner's words.
Cut "04_Rankings.png" 0 400 215 541 "scene_lantern" "right" 0.25
# The wrapped gift on its cushion, from the Daily Gift mockup.
Cut "06_Daily_Gift.png" 600 228 472 280 "art_gift" "round" 0.45
