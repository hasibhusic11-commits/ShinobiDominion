# SHINOBI DOMINION — UI ART DIRECTOR / CLAUDE CODE HANDOFF

**Purpose:** Replace the current uneven, unfinished Unity uGUI presentation with a coherent, production-grade, original classic mobile fantasy strategy visual system inspired by the *experience* of old-school Castle Clash, not its specific artwork or proprietary interface.

## What is in this package

- `01_CLAUDE_CODE_MASTER_PROMPT.md` — **the long prompt**. Paste it to Claude Code or tell Claude Code to read the file. It is an implementation assignment, not a brainstorming request.
- `02_UI_DESIGN_SYSTEM.md` — specific palettes, geometry, component specifications, type, textures, interactions, and accessibility.
- `03_SCREEN_ACCEPTANCE_MATRIX.md` — audit and acceptance criteria for individual pages, screenshots, animation, readability and functional preservation.
- `04_REPOS_AND_TOOLS.md` — verified third-party resources, licensing notes, workflow and installation options.
- `05_CLAUDE_CODE_BOOTSTRAP.txt` — short first message for Claude Code, with the path it must read.
- `project_skill/.claude/skills/shinobi-ui-art-director/SKILL.md` — optional persistent skill to copy into your project.
- `references/current/` — the two current screen contact sheets provided by the owner.
- `references/redesign_concepts/` — available AI mockups. **These are mood/quality targets only, not truthful game data and not pixel-perfect instructions.**

## How to use on Windows

1. Extract the ZIP outside the Unity project first, e.g. `Downloads\ShinobiDominion_UI_Art_Direction_Master_Pack`.
2. Copy `01_CLAUDE_CODE_MASTER_PROMPT.md`, `02_UI_DESIGN_SYSTEM.md`, `03_SCREEN_ACCEPTANCE_MATRIX.md`, and `04_REPOS_AND_TOOLS.md` into your existing repo under `docs/ui/overhaul/`. Copy `references/` under `docs/ui/overhaul/references/`. Keep images outside runtime `Resources` unless required for the actual game; they are *references*, not in-game art assets.
3. Optionally copy the supplied skill directory into `<repo>\.claude\skills\shinobi-ui-art-director\SKILL.md` to make its standards persistent in future Claude Code sessions. Do not overwrite an existing skill by accident.
4. Launch Claude Code **from the repo root**. Paste the text of `05_CLAUDE_CODE_BOOTSTRAP.txt` or instruct Claude to read the absolute path to `01_CLAUDE_CODE_MASTER_PROMPT.md`.
5. Share screenshots from the **actual Unity game**, not only image-generation mockups. Review the visual quality at 16:9, 20:9 and 4:3 as applicable before letting Claude migrate more screens.
6. **Do not blindly approve third-party installs or purchases.** Review package licenses and whether paths with spaces work on your computer, especially for Unity-MCP.

## Boundaries

This is a UI/UX and art-direction production pass. It must NOT change game balance, number of active heroes, monetization, summoning odds, progression, online security, currencies, or gameplay rules. The repository's latest human-approved decisions are the authority. The repo currently includes a conflict: `docs/spec/...Master_Prompt.md` originally specifies six deployed heroes, while `docs/DECISIONS.md` ADR-030 says only one. Claude must inspect, record, and **not resolve this by changing gameplay**. Treat it as a preexisting design conflict that must be left unchanged for a visual overhaul.

**Important:** A magic prompt cannot automatically create professional illustration from colored geometry. The standard demands real approved art inputs, authored nine-slice assets, and screenshots/review gates. An honest visual blocker is preferable to a fake polished-looking placeholder.
