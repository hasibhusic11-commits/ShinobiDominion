# Writes unity/Assets/_Game/Resources/Config/buildings.json with a 15-level table per building.
# Edit the tables here and rerun. The game and the server both read the output.
# All numbers are PROPOSED TEST VALUES. Curve shapes were studied from genre conventions; values are our own.
$out = Join-Path $PSScriptRoot '..\..\unity\Assets\_Game\Resources\Config\buildings.json'

# HP multipliers: accelerating, about 9x by level 15.
$hpCurve = 1, 1.15, 1.35, 1.6, 1.85, 2.2, 2.6, 3.05, 3.55, 4.15, 4.85, 5.65, 6.6, 7.7, 9
# Walls grow much faster: an old wall should be a real obstacle.
$wallHpCurve = 1, 3, 6, 10, 16, 28, 45, 70, 100, 135, 175, 220, 270, 330, 400
# Cost of going from level N to N+1. The last entry is unused, because level 15 is the top.
$costCurve = 1, 2, 4, 7, 12, 20, 32, 50, 75, 110, 160, 230, 320, 450, 0
# Dojo level needed to reach each level: a gate about every 3 levels.
$gates = 1, 1, 1, 1, 3, 3, 3, 6, 6, 6, 9, 9, 9, 12, 12
$productionCurve = 1, 1.4, 1.8, 2.2, 2.7, 3.2, 3.8, 4.4, 5, 5.7, 6.5, 7.3, 8.2, 9.1, 10
$storageCurve = 1, 1.3, 1.6, 2, 2.4, 3, 3.6, 4.3, 5, 6, 7, 8, 9.5, 11, 13
$vaultCurve = 1, 1.1, 1.3, 1.7, 2.2, 2.9, 3.6, 4.4, 5.3, 6.3, 7.5, 9, 10.5, 12.5, 15
$rangeSteps = 0, 0, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5
$baseRangeSteps = 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 3
$campCurve = 15, 17, 19, 21, 23, 25, 27, 29, 31, 33, 35, 36, 37, 38, 40

function Round5([double]$value) {
    if ($value -lt 5) { return [long][math]::Round($value) }
    return [long]([math]::Round($value / 5) * 5)
}

function Levels($spec) {
    $rows = @()
    for ($i = 0; $i -lt 15; $i++) {
        $hpCurveUsed = if ($spec.wall) { $wallHpCurve } else { $hpCurve }
        $row = [ordered]@{
            hp = Round5 ($spec.hp * $hpCurveUsed[$i])
            upgradeCost = Round5 ($spec.cost * $costCurve[$i])
            requiredHq = if ($spec.noGate) { 1 } else { $gates[$i] }
        }
        if ($spec.production) {
            $row.production = [long][math]::Round($spec.production * $productionCurve[$i])
            $row.storedCap = [long][math]::Round($spec.cap * $storageCurve[$i])
        }
        if ($spec.vault) { $row.goldCapacity = Round5 ($spec.vault * $vaultCurve[$i]) }
        if ($spec.attack) { $row.attack = $spec.attack + $i * $spec.attackStep }
        if ($spec.range) {
            $steps = if ($spec.baseRange) { $baseRangeSteps } else { $rangeSteps }
            $row.range = $spec.range + $steps[$i]
        }
        if ($spec.bonus) { $row.bonusPercent = ($i + 1) * $spec.bonus }
        if ($spec.camp) { $row.troopCapacity = $campCurve[$i] }
        $row.might = ($i + 1) * $spec.might
        $rows += ('{' + (($row.Keys | ForEach-Object { '"' + $_ + '":' + $row[$_] }) -join ',') + '}')
    }
    return '[' + "`n        " + ($rows -join ",`n        ") + "`n      ]"
}

$buildings = @(
    @{ head = '"id": "dojo_hq", "displayName": "Dojo HQ", "description": "The heart of the village. Its level sets building limits and how far others can be upgraded.", "category": "Structures", "function": "Headquarters", "width": 5, "depth": 5, "removable": false, "costGold": 0, "maxCountByHqLevel": [1]'; hp = 2000; cost = 2000; might = 50; noGate = $true }
    @{ head = '"id": "builder_hall", "displayName": "Builder Hall", "description": "Opens the build catalog.", "category": "Structures", "function": "BuilderHall", "width": 3, "depth": 3, "removable": false, "costGold": 0, "maxCountByHqLevel": [1]'; hp = 600; cost = 800; might = 3 }
    @{ head = '"id": "hero_altar", "displayName": "Hero Altar", "description": "Your hero roster, upgrades and evolution. Higher levels will shorten hero revival.", "category": "Structures", "function": "HeroAltar", "width": 4, "depth": 4, "removable": false, "costGold": 0, "maxCountByHqLevel": [1]'; hp = 800; cost = 1000; might = 10; bonus = 3 }
    @{ head = '"id": "summoning_gate", "displayName": "Summoning Gate", "description": "Summon heroes. Every roll is independent and random.", "category": "Structures", "function": "SummoningGate", "width": 4, "depth": 4, "removable": false, "costGold": 0, "maxCountByHqLevel": [1]'; hp = 800; cost = 1000; might = 5 }
    @{ head = '"id": "training_grounds", "displayName": "Training Hall", "description": "Trains troops, who wait in your Troop Camps. Higher levels unlock stronger troops.", "category": "Structures", "function": "Training", "width": 4, "depth": 3, "rotatable": true, "costGold": 1500, "maxCountByHqLevel": [1]'; hp = 500; cost = 800; might = 3 }
    @{ head = '"id": "troop_camp", "displayName": "Troop Camp", "description": "Houses trained troops. Stronger troops take up more room. You can have up to four.", "category": "Structures", "function": "TroopCamp", "width": 3, "depth": 3, "costGold": 400, "maxCountByHqLevel": [1, 2, 3, 4]'; hp = 450; cost = 350; might = 3; camp = $true }
    @{ head = '"id": "research_hut", "displayName": "Research Hut", "description": "Research upgrades. Available in a later phase.", "category": "Structures", "function": "Research", "width": 3, "depth": 3, "costGold": 1200, "maxCountByHqLevel": [0, 1]'; hp = 400; cost = 700; might = 3 }
    @{ head = '"id": "defense_tower", "displayName": "Defense Tower", "description": "Fires on raiders in range.", "category": "Defense", "function": "Defense", "width": 2, "depth": 2, "costGold": 600, "maxCountByHqLevel": [2, 3, 4, 6, 8]'; hp = 700; cost = 400; might = 5; attack = 60; attackStep = 20; range = 13 }
    @{ head = '"id": "spirit_turret", "displayName": "Spirit Turret", "description": "Channels Spirit Force into bolts that strike raiders.", "category": "Defense", "function": "Defense", "width": 2, "depth": 2, "costGold": 900, "maxCountByHqLevel": [0, 1, 2, 3, 4]'; hp = 500; cost = 500; might = 6; attack = 90; attackStep = 30; range = 14 }
    @{ head = '"id": "gold_mine", "displayName": "Gold Mine", "description": "Produces gold over time.", "category": "Resources", "function": "Collector", "width": 3, "depth": 3, "costGold": 300, "maxCountByHqLevel": [2, 3, 4, 5, 6], "produces": "Gold"'; hp = 400; cost = 300; might = 3; production = 600; cap = 2400 }
    @{ head = '"id": "gold_vault", "displayName": "Gold Vault", "description": "Raises how much gold you can hold.", "category": "Resources", "function": "Storage", "width": 3, "depth": 3, "costGold": 500, "maxCountByHqLevel": [1, 2, 3, 4, 4]'; hp = 500; cost = 400; might = 3; vault = 15000 }
    @{ head = '"id": "crystal_mine", "displayName": "Crystal Mine", "description": "Slowly produces crystals.", "category": "Resources", "function": "Collector", "width": 3, "depth": 3, "costGold": 1500, "maxCountByHqLevel": [0, 1, 1, 2, 2], "produces": "Crystals"'; hp = 400; cost = 1200; might = 3; production = 4; cap = 16 }
    @{ head = '"id": "hero_base", "displayName": "Hero Base", "description": "A pedestal that holds one hero for raids and defense. Higher levels strengthen its hero.", "category": "HeroBases", "function": "HeroBase", "width": 2, "depth": 2, "costGold": 400, "maxCountByHqLevel": [6]'; hp = 500; cost = 400; might = 1; bonus = 2; range = 14; baseRange = $true }
    @{ head = '"id": "wall", "displayName": "Stone Wall", "description": "Drag to build a straight run of wall.", "category": "Walls", "function": "Wall", "width": 1, "depth": 1, "costGold": 25, "maxCountByHqLevel": [60, 100, 150, 220, 300]'; hp = 300; cost = 20; might = 0; wall = $true }
)

$items = $buildings | ForEach-Object { "    {`n      " + $_.head + ",`n      ""levels"": " + (Levels $_) + "`n    }" }
$json = "{`n  ""schemaVersion"": 2,`n  ""balanceStatus"": ""PROPOSED TEST VALUES - NOT APPROVED"",`n  ""startingGold"": 5000,`n  ""startingCrystals"": 100,`n  ""startingMana"": 500,`n  ""baseGoldCapacity"": 20000,`n  ""upgradeSeconds"": [0, 0, 0, 60, 180, 600, 1200, 2400, 3600, 7200, 14400, 21600, 28800, 43200, 57600, 86400],`n  ""buildings"": [`n" + ($items -join ",`n") + "`n  ]`n}`n"
[IO.File]::WriteAllText((Resolve-Path (Split-Path $out)).Path + '\buildings.json', $json)
Write-Output 'buildings.json written.'
