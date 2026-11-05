# Shinobi Dominion

Original anime-ninja village builder and hero collector (working title). Unity 6 LTS, URP, C#. Early prototype with placeholder art.

- Design rules: [docs/spec](docs/spec/Shinobi_Dominion_Claude_Code_Master_Prompt.md)
- Status: [docs/ROADMAP.md](docs/ROADMAP.md)
- How to try it: [docs/PLAYTEST_CHECKLIST.md](docs/PLAYTEST_CHECKLIST.md)
- Decisions: [docs/DECISIONS.md](docs/DECISIONS.md)

## Run it
Open `unity/` with Unity **6000.3.28f1**, open `Assets/_Game/Scenes/Village.unity`, and press Play.

## Test it
Fast game-rule tests with no Unity needed:
```bash
dotnet test tools/core-tests/ShinobiDominion.Core.Tests.csproj
```
Full suites: in Unity, open Window > General > Test Runner.
