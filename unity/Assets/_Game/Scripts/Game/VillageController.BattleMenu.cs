using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The Battle menu ATTACK opens, the first screen of the UI overhaul (docs/ui/overhaul, M2): a war table of five
    /// illustrated mode cards in UI kit v3. Each card is the mode's own painting in the brass card frame, its name on a
    /// ribbon, a live fact where the game knows one, a short line (or why it is locked) and one action. The modes,
    /// their routes, their button names and when each is open are exactly as before; only how they look changed.
    /// </summary>
    public sealed partial class VillageController
    {
        private struct ModeCard
        {
            public string Button;
            public string Title;
            public string Blurb;
            public string Go;
            /// <summary>The mode's painting in Resources/UI (cut from the owner's own art, docs/ui/overhaul/ART_ASSET_REGISTER.csv).</summary>
            public string Picture;
            /// <summary>A live fact for the badge, or null when the game has none to hand here.</summary>
            public string Badge;
            public bool Open;
            /// <summary>Why a closed mode is closed, in the player's words.</summary>
            public string Locked;
            public System.Action Action;
        }

        private const float ModeCardWidth = 196f;
        private const float ModeCardMinWidth = 184f;
        /// <summary>How far a card sits below the top of its row: room for the name ribbon that rises above it.</summary>
        private const float CardDrop = 20f;
        /// <summary>How far the row reaches up past the window's content area, into the space beside the title ribbon.</summary>
        private const float RowLift = 26f;

        private void ShowBattleHub()
        {
            var team = Village.Buildings.Count(b => b.AssignedHeroInstanceId != null);
            var dungeonStars = RaidData.All.Sum(r => BestStarsOn(r.id));
            var noHero = "Put a hero on a Hero Base first";
            var noServer = "Needs the server";
            _panel.ShowThemedWindow("Battle", new Vector2(1180f, 600f), CloseAll, content =>
            {
                // No standing caption: the cards say everything. A line appears only when something went wrong or the
                // game is offline, and then the cards move down to make room for it.
                string note = null;
                if (!string.IsNullOrEmpty(LastMessage)) note = LastMessage;
                else if (!IsOnline) note = AllowOfflinePlay ? "Practice battles only: not connected, so nothing pays." : "Battles need the server.";
                var top = 0f;
                if (note != null)
                {
                    var noteLabel = HudArt.Label(content, note, HudArt.Numbers, SdTheme.NoteSize, TextAnchor.MiddleCenter, SdTheme.BrassLight, SdTheme.Ink);
                    noteLabel.name = "BattleNote";
                    Ui.Anchor(noteLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(content.rect.width, 30f), Vector2.zero);
                    top = 34f;
                }

                if (IsOnline && _raidList == null)
                {
                    // Drawn again when the list arrives, so nothing is tapped on a menu about to be replaced.
                    var wait = HudArt.Label(content, "Checking your battles...", HudArt.Numbers, 22, TextAnchor.MiddleCenter, SdTheme.Ivory, SdTheme.Ink);
                    Ui.Stretch(wait.rectTransform);
                    return;
                }

                var cards = new[]
                {
                    new ModeCard { Button = "FindRivalButton", Title = "Raid", Blurb = "Raid a rival's village for gold and Mana.", Go = "Find a Rival", Picture = "mode_raid",
                        Open = IsOnline && team > 0, Locked = !IsOnline ? noServer : noHero, Action = FindRival },
                    new ModeCard { Button = "OpenDungeonsButton", Title = "Dungeons", Blurb = "Storm enemy forts, chapter by chapter.", Go = "Open the Map", Picture = "mode_dungeons",
                        Badge = $"{dungeonStars} / {RaidData.All.Count * 3}", Open = IsOnline || AllowOfflinePlay, Locked = noServer, Action = OpenCampaign },
                    new ModeCard { Button = "OpenSiegeButton", Title = "Village Siege", Blurb = "Hold your village against raiders.", Go = "Defend", Picture = "mode_siege",
                        Open = ModesOnline, Locked = noServer, Action = OpenSiege },
                    new ModeCard { Button = "OpenArenaButton", Title = "Arena", Blurb = "Duel another player's hero.", Go = "Enter", Picture = "mode_arena",
                        Open = ModesOnline && team > 0, Locked = !ModesOnline ? noServer : noHero, Action = OpenArena },
                    new ModeCard { Button = "OpenTrialsButton", Title = "Trial Tower", Blurb = "Beat each floor's guardian to climb.", Go = "Climb", Picture = "mode_trials",
                        Open = ModesOnline && team > 0, Locked = !ModesOnline ? noServer : noHero, Action = OpenTrials }
                };

                // The row of cards. Five fit side by side on a wide screen; where they would be squeezed
                // narrower than they read well (a 4:3 tablet), the row scrolls sideways with the next card in view.
                var row = Ui.Rect("ModeCards", content);
                Ui.Stretch(row, 0f, 0f, 0f, top - RowLift);
                var height = Mathf.Max(300f, content.rect.height - top + RowLift - CardDrop);
                var width = (content.rect.width - (cards.Length - 1) * SdTheme.Gap) / cards.Length;
                var scrolls = width < ModeCardMinWidth;
                if (scrolls) width = ModeCardWidth;
                RectTransform holder = row;
                if (scrolls)
                {
                    var scroll = row.gameObject.AddComponent<ScrollRect>();
                    scroll.horizontal = true;
                    scroll.vertical = false;
                    scroll.movementType = ScrollRect.MovementType.Elastic;
                    row.gameObject.AddComponent<RectMask2D>();
                    var hit = row.gameObject.AddComponent<Image>();
                    hit.color = new Color(0f, 0f, 0f, 0f);
                    holder = Ui.Rect("Strip", row);
                    holder.anchorMin = new Vector2(0f, 0f);
                    holder.anchorMax = new Vector2(0f, 1f);
                    holder.pivot = new Vector2(0f, 0.5f);
                    holder.sizeDelta = new Vector2(cards.Length * width + (cards.Length - 1) * SdTheme.Gap + 8f, 0f);
                    scroll.content = holder;
                    scroll.viewport = row;
                }
                var start = scrolls ? 4f : 0f;
                for (var i = 0; i < cards.Length; i++)
                    DrawModeCard(holder, cards[i], i, start + i * (width + SdTheme.Gap), width, height);
            });
        }

        /// <summary>
        /// One mode as a card: the painting in the brass card frame, the name on a ribbon over the frame, a live fact on
        /// a badge, the line on a dark plate and the action below. The whole card can be tapped as well as its button.
        /// A closed mode keeps its picture, greyed, with a padlock and the reason on the plate.
        /// </summary>
        private static void DrawModeCard(RectTransform parent, ModeCard card, int index, float x, float width, float height)
        {
            var rect = Ui.Rect(card.Button.Replace("Button", "Card"), parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -CardDrop);

            // The painting and its frame, above the button zone.
            var picture = Ui.Rect("PictureArea", rect);
            Ui.Stretch(picture, 0f, 74f, 0f, 0f);
            HudArt.DropShadow(picture, 6f, 10f, 0.55f);
            var hit = picture.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            SdKit.FramedPicture(picture, card.Picture, !card.Open);
            if (card.Open)
            {
                var whole = picture.gameObject.AddComponent<Button>();
                whole.targetGraphic = hit;
                whole.transition = Selectable.Transition.None;
                whole.onClick.AddListener(() => GameSounds.Play(GameSound.Click));
                whole.onClick.AddListener(() => card.Action());
                UiMotion.Pressable(whole);
            }

            // The name on a ribbon across the frame's top edge.
            var ribbon = Ui.Rect("Ribbon", rect);
            Ui.Anchor(ribbon, new Vector2(0.5f, 1f), new Vector2(width + 14f, 48f), new Vector2(0f, 20f));
            var ribbonImage = Ui.Framed(ribbon, HudArt.Sprite(SdTheme.Ribbon), false);
            ribbonImage.pixelsPerUnitMultiplier = 2.6f;
            if (!card.Open) ribbonImage.color = SdTheme.LockedTint;
            var title = HudArt.Label(ribbon, card.Title, HudArt.Display, SdTheme.CardTitleSize, TextAnchor.MiddleCenter, card.Open ? SdTheme.Ivory : SdTheme.IvoryMuted, SdTheme.CrimsonDark);
            Ui.Stretch(title.rectTransform, 30f, 4f, 30f, 6f);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 14;
            title.resizeTextMaxSize = SdTheme.CardTitleSize;

            if (card.Badge != null)
            {
                var badge = SdKit.Badge(picture, card.Badge, "icon_star");
                Ui.Anchor(badge, new Vector2(1f, 1f), new Vector2(112f, 42f), new Vector2(-12f, -40f));
            }
            if (!card.Open)
            {
                var padlock = HudArt.Picture(picture, "Lock", "icon_lock", new Vector2(0.5f, 0.5f), new Vector2(64f, 64f), new Vector2(0f, 26f));
                padlock.preserveAspect = true;
            }

            // The line, on a dark plate at the foot of the painting, so it reads over any picture.
            var plate = Ui.Rect("BlurbPlate", picture);
            Ui.Anchor(plate, new Vector2(0.5f, 0f), new Vector2(width - 26f, 66f), new Vector2(0f, 14f));
            SdKit.Plate(plate);
            var blurb = HudArt.Label(plate, card.Open ? card.Blurb : card.Locked, HudArt.Numbers, SdTheme.BodySize, TextAnchor.MiddleCenter,
                card.Open ? SdTheme.Ivory : SdTheme.BrassLight, SdTheme.Ink);
            blurb.name = "Blurb";
            Ui.Stretch(blurb.rectTransform, 10f, 6f, 10f, 6f);
            blurb.horizontalOverflow = HorizontalWrapMode.Wrap;
            blurb.verticalOverflow = VerticalWrapMode.Truncate;
            blurb.resizeTextForBestFit = true;
            blurb.resizeTextMinSize = 13;
            blurb.resizeTextMaxSize = SdTheme.BodySize;

            var go = SdKit.Button(rect, card.Button, card.Go, SdButtonKind.War, card.Action, card.Open, card.Open ? null : card.Locked);
            Ui.Anchor((RectTransform)go.transform, new Vector2(0.5f, 0f), new Vector2(width - 6f, 62f), new Vector2(0f, 4f));
            UiMotion.PopIn(rect, 0.04f * index, 0.9f, 0.22f);
        }
    }
}
