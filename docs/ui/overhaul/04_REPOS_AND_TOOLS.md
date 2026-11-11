# Verified helpful tools — prioritized for this Unity project (November 2026)

**These are optional references, not dependencies that must all be installed.** An agent should use at most one primary design-decision skill and one Unity editor bridge, then prefer the repository's existing sprite kit. Web-oriented design skills help with *taste, hierarchy, structure and critique*, NOT with directly producing Unity-ready sprites.

## 1. Unity-MCP / AI Game Developer — highest leverage if it works locally
- GitHub: https://github.com/IvanMurzak/Unity-MCP
- Setup: https://github.com/IvanMurzak/Unity-MCP/wiki/Getting-Started
- Use: allow Claude to inspect and change Unity GameObjects/Canvas and verify actual scenes, screenshots and errors.
- **Conditional:** In this repository's `docs/VISUAL_BASELINE.md`, a prior audit rejected Unity-MCP because the Windows project path contained a space (`Game Design`) and an account sign-in was required. **Check compatibility with the current release and ask the owner before login or moving directories.** Do not break a working Unity installation. If unavailable, use the existing Unity batchmode screenshot/test pathway.
- Newer Unity-MCP versions expose RQL tools (`rql-schema`, `rql-query`, `rql-mutate`) rather than assuming every older one-tool-per-action name. Follow the installed plugin's actual documentation.

## 2. UI/UX Pro Max skill — design intelligence, not Unity code
- GitHub: https://github.com/nextlevelbuilder/ui-ux-pro-max-skill
- License: MIT for public repository (verify current license before use).
- Install via Claude Code plugin marketplace (commands shown in project's README):
  - `/plugin marketplace add nextlevelbuilder/ui-ux-pro-max-skill`
  - `/plugin install ui-ux-pro-max@ui-ux-pro-max-skill`
- Alternative (current documented): `npx ui-ux-pro-max-cli init --ai claude` or `npm install -g ui-ux-pro-max-cli` then `uipro init --ai claude` **after reviewing code, dependencies and expected changes**.
- **Important:** This is heavily web/app oriented. Use its palette, typography, cognitive hierarchy and evaluation methods, but not webpage-style card layouts or HTML as a substitute for Unity uGUI.

## 3. `mae616/design-skills` — complementary structured critique
- GitHub: https://github.com/mae616/design-skills
- Documented optional install: `npx skills add mae616/design-skills`.
- Relevant roles: `ui-designer`, `creative-coder`, `accessibility-engineer`, `usability-psychologist`.
- If both this and UI/UX Pro Max are installed, explicitly resolve conflicts by referring to Shinobi Dominion's own tokens; do NOT average their style suggestions. One source of art direction: this game's design system.

## 4. Kenney Fantasy UI Borders — available CC0 base chrome
- Official: https://kenney.nl/assets/fantasy-ui-borders
- itch: https://kenney-assets.itch.io/fantasy-ui-borders
- CC0; ~130+ 9-slice-friendly sprites. Use them selectively for ORIGINAL lacquer/brass windows. The repo already uses a Kenney border in one location: audit before importing another copy. CC0 does not mean the art naturally matches the game's premium look; recolor/recompose or omit.

## 5. Kenney UI Pack — optional icon/background prototyping only
- Official: https://kenney.nl/assets/ui-pack
- CC0, 400+ UI pieces. Most have a flatter style than the desired game; do not blanket-replace the designed fantasy frames with them.

## 6. UIEffect (mob-sakai) — optional and performance-tested
- GitHub: https://github.com/mob-sakai/uieffect
- Use only if shader-compatible, and only for subtle selective effects on selected/locked/reveal buttons. Verify the license, Unity version, URP integration, build size and on-device GPU cost. Optional; simple UI animations can be implemented without it.

## 7. PrimeTween — optional tweening, with custom distribution rules
- GitHub: https://github.com/KyryloKuzyk/PrimeTween
- **License is not blanket MIT.** Free/commercial game usage allowed, but source redistribution in a public derivative repo has specific restrictions; the author directs use of its UPM/NPM dependency method. Inspect current `LICENSE` and do not vendor it into public source. If complex, use existing `UiMotion.cs` and Unity APIs instead.

## Official Unity docs
- UI system comparison: https://docs.unity.com/en-us/engine/6000.6/manual/uitoolkits/ui-system-compare
- Runtime UI for Unity 6.6: **uGUI is recommended**, UI Toolkit an alternative. Because this project is already uGUI, a full framework migration is out of scope.
- Nine-slice: https://docs.unity.cn/Manual/9SliceSprites.html
- Screenshot capture: https://docs.unity.com/en-us/engine/6000.7/script-reference/unityengine/screencapture/capturescreenshot

## Existing project sources — read before installing anything
- `unity/Assets/_Game/Scripts/Game/UI/VillageUi.cs`
- `unity/Assets/_Game/Scripts/Game/UI/HudArt.cs`
- `unity/Assets/_Game/Scripts/Game/UI/UiMotion.cs`
- `unity/Assets/_Game/Scripts/Editor/UiSpriteImporter.cs`
- `unity/Assets/_Game/Scripts/Editor/ProjectSetup.cs`
- `tools/art/ui_kit.py`
- `docs/ui/UI_TOKENS.md`, `HUD_SPEC.md`, `CATALOG_SPEC.md`
- `docs/ui/target_home_mockup_v2.webp`
- `docs/screenshots/`, `docs/ui/v*`

## Asset and plugin audit protocol
1. Identify exact version, latest release, compatibility with installed Unity and license type.
2. Check whether an existing equivalent is already present in project.
3. Check whether package's import action runs code or hooks. Read source before executing scripts.
4. Store research downloads outside production assets; add the smallest approved subset only.
5. Record URL, author, version, commit, license, integration purpose, changes, tests and required credit in `docs/THIRD_PARTY_NOTICES.md`.
6. No unreviewed marketplace purchases; user has stated a no-spend preference in repo ADR-022.
7. Do not ingest old Castle Clash art/icon sheets as runtime assets. Use screenshots only as broad comparison references.

## Recommendation
Start with the repo's existing uGUI + sprite kit + screenshot tooling. Optionally install UI/UX Pro Max, but the biggest improvement is an **asset-first, screenshot-first art direction loop**; installing 20 skills cannot substitute for a cohesive set of professionally designed frame/button/icon sprites.
