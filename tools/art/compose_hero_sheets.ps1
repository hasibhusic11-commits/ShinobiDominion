# Lays hero renders out on labelled design sheets.
# Usage: compose_hero_sheets.ps1 <folder of renders named id_form.png> <output folder>
param([string]$Renders, [string]$Output)
Add-Type -AssemblyName System.Drawing
$config = Join-Path $PSScriptRoot '..\..\unity\Assets\_Game\Resources\Config'
$heroes = (Get-Content (Join-Path $config 'heroes.json') -Raw | ConvertFrom-Json).heroes
$designs = (Get-Content (Join-Path $config 'hero_designs.json') -Raw | ConvertFrom-Json).heroes
New-Item -ItemType Directory -Force $Output | Out-Null

$ink = [System.Drawing.ColorTranslator]::FromHtml('#14161F')
$card = [System.Drawing.ColorTranslator]::FromHtml('#232738')
$white = [System.Drawing.Brushes]::White
$muted = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#B9BED0'))
$gold = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#E6B85A'))
$title = New-Object System.Drawing.Font 'Segoe UI', 34, ([System.Drawing.FontStyle]::Bold)
$name = New-Object System.Drawing.Font 'Segoe UI', 21, ([System.Drawing.FontStyle]::Bold)
$small = New-Object System.Drawing.Font 'Segoe UI', 14
$tiny = New-Object System.Drawing.Font 'Segoe UI', 12

function New-Sheet($w, $h) {
    $bitmap = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = 'HighQuality'; $g.InterpolationMode = 'HighQualityBicubic'; $g.TextRenderingHint = 'ClearTypeGridFit'
    $g.Clear($ink)
    return @($bitmap, $g)
}

function Draw-Hero($g, $id, $form, $x, $y, $size) {
    $path = Join-Path $Renders "${id}_$form.png"
    if (-not (Test-Path $path)) { throw "missing render $path" }
    $image = [System.Drawing.Image]::FromFile($path)
    $g.DrawImage($image, $x, $y, $size, $size)
    $image.Dispose()
}

# Legendaries: one sheet per hero, four forms side by side.
$labels = 'Base', 'Evo I', 'Evo II', 'Evo III'
foreach ($hero in ($heroes | Where-Object { $_.rarity -eq 'Legendary' })) {
    $design = $designs | Where-Object { $_.id -eq $hero.id }
    $sheet = New-Sheet 1760 760; $bitmap = $sheet[0]; $g = $sheet[1]
    $g.DrawString($hero.displayName, $title, $white, 30, 16)
    $g.DrawString("Legendary  -  $($hero.role)  -  $($hero.affinity)", $small, $gold, 34, 78)
    $g.DrawString($design.concept, $small, $muted, (New-Object System.Drawing.RectangleF 34, 106, 1690, 54))
    for ($i = 0; $i -lt 4; $i++) {
        $x = 30 + $i * 430
        $g.FillRectangle((New-Object System.Drawing.SolidBrush $card), $x, 166, 410, 570)
        Draw-Hero $g $hero.id $i ($x + 10) 172 390
        $form = $design.forms[$i]
        $g.DrawString("$($labels[$i]): $($form.name)", (New-Object System.Drawing.Font 'Segoe UI', 15, ([System.Drawing.FontStyle]::Bold)), $white, ($x + 12), 566)
        $g.DrawString($form.summary, $tiny, $muted, (New-Object System.Drawing.RectangleF ($x + 12), 600, 388, 130))
    }
    $bitmap.Save((Join-Path $Output "legendary_$($hero.id).png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bitmap.Dispose()
}

# Commons and Elites: one look each, on a shared sheet.
foreach ($rarity in 'Elite', 'Common') {
    $list = @($heroes | Where-Object { $_.rarity -eq $rarity })
    $columns = 4
    $rows = [math]::Ceiling($list.Count / $columns)
    $sheet = New-Sheet (30 + $columns * 430) (90 + $rows * 500); $bitmap = $sheet[0]; $g = $sheet[1]
    $g.DrawString("$rarity heroes: one look each", $title, $white, 30, 16)
    for ($i = 0; $i -lt $list.Count; $i++) {
        $hero = $list[$i]
        $design = $designs | Where-Object { $_.id -eq $hero.id }
        $x = 30 + ($i % $columns) * 430
        $y = 90 + [math]::Floor($i / $columns) * 500
        $g.FillRectangle((New-Object System.Drawing.SolidBrush $card), $x, $y, 410, 484)
        Draw-Hero $g $hero.id 0 ($x + 45) ($y + 4) 320
        $g.DrawString($hero.displayName, $name, $white, ($x + 12), ($y + 322))
        $g.DrawString("$($hero.role)  -  $($hero.affinity)", $small, $gold, ($x + 12), ($y + 360))
        $g.DrawString($design.concept, $tiny, $muted, (New-Object System.Drawing.RectangleF ($x + 12), ($y + 388), 388, 92))
    }
    $bitmap.Save((Join-Path $Output "$($rarity.ToLower())_heroes.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bitmap.Dispose()
}
Write-Output "Sheets written to $Output"
