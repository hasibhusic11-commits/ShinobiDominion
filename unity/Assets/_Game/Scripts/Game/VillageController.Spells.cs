using System.Collections.Generic;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Spells in a live raid (ADR-052). The raid says which spells may be cast and at what level; each is cast
    /// once, by tapping its round button and then the map. The cast goes into the same battle the device is
    /// running, and the list of casts is sent with the drops so the server can fight the battle again.
    /// </summary>
    public sealed partial class VillageController
    {
        private const float SpellButtonSize = 68f;
        private const float SpellButtonGap = 12f;

        private SpellCatalog _spellCatalog;

        public SpellCatalog SpellData
        {
            get
            {
                if (_spellCatalog != null) return _spellCatalog;
                var config = Resources.Load<TextAsset>("Config/spells");
                if (config == null) throw new System.InvalidOperationException("Resources/Config/spells.json is missing.");
                _spellCatalog = SpellCatalog.FromJson(config.text);
                return _spellCatalog;
            }
        }

        /// <summary>How many spells have been cast in the live battle so far.</summary>
        public int SpellsCast => _raid?.Casts.Count ?? 0;

        /// <summary>The spell picked and waiting for a tap on the map; null when none is.</summary>
        public string SelectedSpell => _raid?.SelectedSpell;

        private bool SpellUsed(string id) => _raid.Casts.Any(c => c.spellId == id);

        /// <summary>Picks a spell to cast with the next tap on the map; picking it again puts it back.</summary>
        public void SelectSpell(string id)
        {
            if (!LiveRunning || SpellUsed(id)) return;
            // Only a spell this raid was given can be picked.
            if (_raid.Session.spells == null || _raid.Session.spells.All(s => s.id != id)) return;
            _raid.SelectedSpell = _raid.SelectedSpell == id ? null : id;
            _raid.SelectedTroop = null;
            _raid.SelectedHero = null;
            LastMessage = null;
            ShowDeployScreen();
        }

        /// <summary>
        /// Casts a spell on a cell, now. False, with the reason in LastMessage, when it is refused: the same checks
        /// the server makes, so a cast the device shows is one the server will accept.
        /// </summary>
        public bool CastSpellAt(string id, GridCoord cell)
        {
            if (!LiveRunning) return false;
            if (!AnythingDropped)
            {
                // The server refuses a battle with spells and no army.
                LastMessage = "Drop a hero or troops first: a spell needs an army on the field.";
                return false;
            }
            var session = _raid.Session;
            var cast = new RaidSpellCast { spellId = id, x = cell.X, z = cell.Z, tick = _raid.Live.Tick };
            var all = new List<RaidSpellCast>(_raid.Casts) { cast };
            var error = SpellCatalog.CastError(session.spells, all, BattleSim.TickLimit);
            if (error != "")
            {
                LastMessage = SpellRefusal(error);
                return false;
            }
            _raid.Live.CastSpell(SpellData.ToInput(session.spells, cast));
            _raid.Casts.Add(cast);
            LastMessage = null;
            return true;
        }

        private static string SpellRefusal(string error)
        {
            switch (error)
            {
                case "SPELL_LOCKED": return "That spell is not unlocked yet.";
                case "SPELL_USED": return "Each spell can be cast once in a battle.";
                case "BAD_SPELL_CELL": return "Cast inside the map.";
                default: return "The battle is ending: that spell cannot be cast now.";
            }
        }

        /// <summary>The picture on a spell's button: the same picture the spell draws on the field.</summary>
        private static string SpellPicture(string effect)
        {
            switch (effect)
            {
                case "heal": return "fx_heal_glow";
                case "freeze": return "fx_shield_bubble";
                case "boost": return "fx_hit_spark";
                default: return "fx_fireball";
            }
        }

        private static Color SpellTint(string effect)
        {
            switch (effect)
            {
                case "freeze": return new Color(0.7f, 0.9f, 1f);
                case "boost": return new Color(1f, 0.78f, 0.3f);
                default: return Color.white;
            }
        }

        /// <summary>The spell's name and what it does, shown under the clock while it is picked.</summary>
        private string SpellHint()
        {
            var spell = _raid?.SelectedSpell == null ? null : SpellData.Find(_raid.SelectedSpell);
            return spell == null ? null : $"{spell.displayName}: {spell.description} Tap the map to cast it.";
        }

        /// <summary>
        /// A short row of round spell buttons above the cards, after the Auto button. A spent spell goes grey; the
        /// picked one glows.
        /// </summary>
        private void BuildSpellButtons(RectTransform screen)
        {
            var spells = _raid.Session.spells;
            if (spells == null || spells.Count == 0) return;
            var row = Ui.Rect("SpellRow", screen);
            var width = spells.Count * SpellButtonSize + (spells.Count - 1) * SpellButtonGap;
            Ui.Anchor(row, new Vector2(0f, 0f), new Vector2(width, SpellButtonSize + 22f), new Vector2(166f, CardHeight + 11f));
            for (var i = 0; i < spells.Count; i++)
            {
                var definition = SpellData.Find(spells[i].id);
                if (definition == null) continue;
                SpellButton(row, definition, spells[i].level, i * (SpellButtonSize + SpellButtonGap));
            }
        }

        private void SpellButton(RectTransform row, SpellDefinition spell, int level, float x)
        {
            var used = SpellUsed(spell.id);
            var selected = !used && _raid.SelectedSpell == spell.id;
            var rect = Ui.Rect("SpellButton_" + spell.id, row);
            Ui.Anchor(rect, new Vector2(0f, 1f), new Vector2(SpellButtonSize, SpellButtonSize), new Vector2(x, 0f));
            if (selected)
            {
                var halo = HudArt.Picture(rect, "Glow", "medal_halo", new Vector2(0.5f, 0.5f), new Vector2(SpellButtonSize * 1.3f, SpellButtonSize * 1.3f), Vector2.zero);
                UiMotion.Breathe(halo, 0.95f, 1.2f);
            }
            HudArt.Picture(rect, "Shadow", "shadow", new Vector2(0.5f, 0f), new Vector2(SpellButtonSize, SpellButtonSize * 0.3f), new Vector2(0f, -SpellButtonSize * 0.08f));
            var faceRect = Ui.Rect("Face", rect);
            Ui.Stretch(faceRect);
            var face = faceRect.gameObject.AddComponent<Image>();
            face.sprite = HudArt.Sprite("hud_small_round_button");
            face.preserveAspect = true;

            var picture = Resources.Load<Sprite>("Effects/" + SpellPicture(spell.effect));
            if (picture != null)
            {
                var icon = Ui.Icon(rect, picture, new Vector2(0.5f, 0.5f), new Vector2(SpellButtonSize * 0.9f, SpellButtonSize * 0.9f), Vector2.zero);
                icon.name = "Picture";
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.color = SpellTint(spell.effect);
            }

            // Under the button: its level, or that it is spent.
            var plate = Ui.Rect("Level", rect);
            Ui.Anchor(plate, new Vector2(0.5f, 0f), new Vector2(SpellButtonSize - 8f, 20f), new Vector2(0f, -20f));
            Ui.Background(plate, new Color(0.05f, 0.07f, 0.12f, 0.85f), false);
            var label = HudArt.Label(plate, used ? "Used" : $"Lv {level}", HudArt.Numbers, 15, TextAnchor.MiddleCenter, used ? HudArt.IvoryMuted : HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Stretch(label.rectTransform);
            label.raycastTarget = false;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.interactable = !used;
            var id = spell.id;
            button.onClick.AddListener(() =>
            {
                GameSounds.Play(GameSound.Click);
                SelectSpell(id);
            });
            UiMotion.Pressable(button);
            if (used) Dim(rect);

        }

        /// <summary>
        /// While a spell is picked and a mouse is over the map, a ring shows where it would land and how wide it
        /// reaches. On a touch screen there is no pointer to follow, so the ring shows only when the spell lands.
        /// </summary>
        private void UpdateSpellAim()
        {
            if (_raid?.Stage == null) return;
            var spell = LiveRunning && _raid.SelectedSpell != null ? SpellData.Find(_raid.SelectedSpell) : null;
            var mouse = Mouse.current;
            if (spell == null || mouse == null)
            {
                _raid.Stage.Aim(Vector3.zero, 0f);
                return;
            }
            var cell = CellAt(mouse.position.ReadValue());
            if (!VillageGrid.InBounds(cell.X, cell.Z))
            {
                _raid.Stage.Aim(Vector3.zero, 0f);
                return;
            }
            _raid.Stage.Aim(new Vector3(cell.X + 0.5f, 0f, cell.Z + 0.5f), spell.RadiusCells);
        }
    }
}
