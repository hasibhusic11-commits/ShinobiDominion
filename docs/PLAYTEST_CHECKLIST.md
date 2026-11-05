# Play Mode checklist

Open `unity/` in Unity 6000.3.28f1, open `Assets/_Game/Scenes/Village.unity`, press Play. Use a mouse: left drag pans, the wheel zooms, right drag always pans.

## Phase 1
1. **Idle screen:** you see name and level at top left, gold and crystals at top right, and only ATTACK at the bottom. There is no grid and no energy or stamina.
2. **Building navigation:** tap the Dojo, then Hero Altar, then a Hero Base. Each opens its own panel. Tap empty ground to close. Dragging across a building should pan and never select it.
3. **Build a tower:** tap the Builder Hall (dark wooden hut with a small crane), then Build, Defense, Place. The grid appears and the catalog disappears. Drag the ghost onto a building and see red cells with a white X plus "Blocked". Drag it to open ground and Confirm. Close, and the grid is gone.
4. **Draw walls:** Builder Hall, Build, Walls, Draw walls. Drag across the ground, including diagonally, and the run stays straight with the cost shown. Confirm, then draw a second run off its end to see the corner join. Try Undo.
5. **Six bases:** Builder Hall, Build, Bases, and place four more. The seventh must be refused. Tap one and Move it.
6. **Reload:** stop Play Mode and press Play again. The layout must be identical. To start over, use Menu, then Reset village.

## Phase 2
The editor gives you a development-only starter roster. It is not a summon, and release builds do not have it.

1. **Roster:** tap the Hero Altar, which has four dark pillars. Try the rarity, role and affinity filters.
2. **Assign:** tap a Hero Base, then Assign Hero, and pick someone. A small figure appears on the pedestal. On a second base, that hero is greyed out as "on another base".
3. **Evolve:** Altar, then the Solar Beast Sage at Lv 200, then Evolution. Check the requirements list, choose the copy to sacrifice, read the before and after stats, and confirm. The hero becomes Evo I at Lv 1 with higher stats, and the copy and one Awakening Scroll are gone.
4. **Protection:** lock a copy, or put it on a base. It must disappear from the sacrifice choices.
5. **All-Legendary team:** putting six Legendaries on six bases must be allowed.

If your layout predates Phase 2 you may want Menu, then Reset village. That keeps your heroes.

## Phase 3
Start Docker Desktop, then from `backend/` run `docker compose up -d --wait nakama` before pressing Play. Without the server the game plays offline and summoning is unavailable.

1. **Crystals:** the top bar shows 3,000, which comes from the server.
2. **Summon:** tap the Summoning Gate, which has the red gate frame. Check the rates, which should say they are test rates. Summon x10: you pay 1,000 and get exactly ten heroes, whatever they are.
3. **Persistence:** stop and restart Play Mode. Crystals and heroes stay, because the server holds them.
4. **Server off:** run `docker compose stop nakama`, then try to summon. You should get a clear message and lose nothing.
5. **Village on the server:** with the server running, build a tower and draw some walls, then restart Play Mode. Everything stays, and gold matches. Your village now lives on the server, so earlier local layouts are gone. The Undo button is hidden while online.
6. **Build conflict:** a refused build shows the reason in the placement bar and charges nothing.

## Please watch for
- Whether pan, zoom and placement *feel* right. Automated tests prove they work, not that they feel good.
- Anything hard to tap or read.
