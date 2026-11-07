# Shinobi Dominion UI tokens (v2, heavier pass)

Reference feel: 2013 to 2015 mobile strategy UI. Thick framed parts, strong outlines and drop shadows, icon-first
round actions, crimson banners. All art is original and drawn by `tools/art/ui_kit.py` into
`unity/Assets/_Game/Resources/UI`. Reference canvas 1280 x 720.

## Colour
| Token | Hex | Use |
|---|---|---|
| ink | #0C0806 | outer outlines of every framed part |
| lacquer-light / lacquer / lacquer-deep | #2B3B63 / #16203A / #0C1222 | panel interiors, lacquer buttons |
| resource-blue top / bottom | #24468A / #0D1D42 | counter interiors |
| brass-light / brass / brass-dark | #F2D28A / #B08A47 / #5E4318 | frames, rims, bevels |
| crimson-light / crimson / crimson-dark | #D2503F / #8E2A26 / #4A110E | ATTACK, banners, destructive |
| gold face top / bottom | #F7DC93 / #8A5E22 | primary buttons, active tabs, prices |
| charcoal top / bottom | #5A5A62 / #1C1C21 | disabled |
| ivory / ivory-muted | #EFE6D0 / #B9AF98 | text and icons |

## Type
| Role | Font | Size | Treatment |
|---|---|---|---|
| Banner titles | Cinzel bold | 28 to 30 | ivory, 2.2 px crimson-dark outline, 3 px drop |
| Names on plates and cards | Cinzel bold | 18 to 24 | ivory, 2.2 px ink outline |
| Numbers, buttons, tabs | Rubik bold | 16 to 22 | ivory with ink outline, or dark brown on gold |
| Captions under round buttons | Cinzel bold | 16 | ivory, ink outline |
Minimum 13 anywhere.

## Frame thickness (in reference units at 1x)
| Part | Outer ink | Brass | Inner ink | Corner radius |
|---|---|---|---|---|
| Plate (player bar, name plaque) | 3 | 9 | 2 | 16 |
| Counter | 3 | 9 | 2 | 16 |
| Button plate | 3 | 5 + 7 lip | 2 | 14 |
| Card | 3 | 10 | 2 | 14, plus a recessed picture well |
| Window | 4 | 20 | 3 | 12, with crimson gem corner caps |
| Round action | 4 | 22 % of radius | 2 | circle |

## Buttons
| Kind | Look | Size | Use |
|---|---|---|---|
| Hero | crimson medallion, thick riveted brass ring | 172 | ATTACK only |
| Primary round | gold face, brass ring | 96 | the building's own action |
| Secondary round | lacquer face, brass ring | 96 | Upgrade, Move |
| Destructive round | crimson face | 96 or 54 to 64 | Remove, Close |
| Icon-only round | lacquer face, thin ring | 52 to 72 | Info, Menu, Back |
| Plate primary / secondary / danger | gold / lacquer / crimson plate | 58 high | list actions, tabs |
States: normal; selected (bright gold halo ring and a slow pulse); pressed (scale 0.93, face 20% darker);
disabled (charcoal face, faded icon, shake and dull click on tap); hover on desktop only.

## Depth
- Every framed part sits on a soft drop shadow: `shadow_s32`, 6 down, 45% black.
- Faces have a gloss band over the top 40% and a darker lip along the bottom.
- Picture wells are recessed: dark, with an inner shadow along the top.

## Spacing
8 unit grid. Screen margin 14. Gap between round actions 18, between cards 14, between tabs 10.
Window padding 34 inside the brass.

## Icons
Drawn white-ivory with a 5 px ink outline at 128, shown at 28 to 36 on plates, 52 on round actions, 40 % of
ATTACK's size.
