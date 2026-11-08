# Shinobi Dominion: engineering notes

The rules are in `docs/spec/Shinobi_Dominion_Claude_Code_Master_Prompt.md`. Its non-negotiable rules override everything else. Read them before changing gameplay, UI or summoning.

## Layout
- `unity/Assets/_Game/Scripts/Core`: engine-free rules. No `UnityEngine` here.
- `unity/Assets/_Game/Scripts/Game`: world, input and UI. No game rules here.
- `unity/Assets/_Game/Scripts/Editor`: generates URP assets, materials and the scene.
- `unity/Assets/_Game/Resources/Config/buildings.json`: building data. All values are unapproved test values.
- `tools/core-tests`: compiles Core and EditMode tests under .NET.

## Commands
Core tests, no Unity needed:
```bash
dotnet test tools/core-tests/ShinobiDominion.Core.Tests.csproj
```

## Do not
- Add anything to the bottom of the idle screen except ATTACK at the bottom left and the owner's left column of round shortcuts (ADR-033), or any energy or stamina counter. The bottom right is for a tapped building's action medallions only.
- Open the build catalog from anywhere but the Builder Hall.
- Add pity, guarantees or duplicate protection to summoning, or roll on the client.
- Trust the client for currency, inventory, placement or battle results once a server exists.
- Copy from `research/` or from repos without a license. See `docs/REPOSITORY_AUDIT.md`.
- Claim something works without running it. Record what was and was not verified.
