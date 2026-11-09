using System.Linq;
using LitMotion;
using LitMotion.Extensions;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The summon reveal: a burst of light in the rarity's colour, then the hero's card. It only shows
    /// what the server already rolled, over the result screen, which is drawn first and stays usable.
    /// A ten-pull reveals its rarest hero. Tap anywhere to close it.
    /// </summary>
    public sealed partial class VillageController
    {
        private GameObject _reveal;

        public bool RevealShowing => _reveal != null;

        private void ShowSummonReveal(ServerSummonResult result)
        {
            CloseReveal();
            var best = result.Rolls
                .Select(r => new { Roll = r, Hero = Roster.Find(r.instanceId) })
                .Where(r => r.Hero != null)
                .OrderByDescending(r => (int)Roster.ArchetypeOf(r.Hero).rarity)
                .ThenBy(r => r.Roll.rollIndex)
                .FirstOrDefault();
            if (best == null) return;
            var archetype = Roster.ArchetypeOf(best.Hero);
            var rarity = archetype.rarity;
            var (glow, edge, _) = HeroArt.Palette(rarity);
            var clock = MotionScheduler.UpdateIgnoreTimeScale;

            var root = Ui.Rect("SummonReveal", _uiRoot);
            Ui.Stretch(root);
            root.SetAsLastSibling();
            _reveal = root.gameObject;
            var dim = Ui.Background(root, new Color(edge.r * 0.25f, edge.g * 0.25f, edge.b * 0.25f, 0.92f));
            var close = root.gameObject.AddComponent<Button>();
            close.targetGraphic = dim;
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(CloseReveal);
            UiMotion.FadeIn(root.gameObject, 0.2f);

            // Two slow counter-turning rays and a bright core behind the card.
            for (var i = 0; i < 2; i++)
            {
                var rays = Ui.Icon(root, HeroArt.Glow, new Vector2(0.5f, 0.5f), new Vector2(900f, 900f), new Vector2(0f, 10f));
                rays.color = new Color(glow.r, glow.g, glow.b, i == 0 ? 0.55f : 0.35f);
                rays.raycastTarget = false;
                rays.rectTransform.localScale = new Vector3(i == 0 ? 1.2f : 0.5f, i == 0 ? 0.5f : 1.2f, 1f);
                var direction = i == 0 ? 1f : -1f;
                var rect = rays.rectTransform;
                LMotion.Create(0f, 360f * direction, rarity == HeroRarity.Legendary ? 5f : 9f).WithLoops(-1).WithScheduler(clock)
                    .Bind(z => rect.localEulerAngles = new Vector3(0f, 0f, z)).AddTo(rays.gameObject);
            }
            var core = Ui.Icon(root, HeroArt.Glow, new Vector2(0.5f, 0.5f), new Vector2(520f, 520f), new Vector2(0f, 10f));
            core.color = new Color(glow.r, glow.g, glow.b, 0.95f);
            core.raycastTarget = false;
            core.rectTransform.localScale = Vector3.one * 0.1f;
            LMotion.Create(0.1f, 1.25f, 0.55f).WithEase(Ease.OutExpo).WithScheduler(clock).BindToLocalScaleXYZ(core.rectTransform).AddTo(core.gameObject);
            UiMotion.Breathe(core, 1f, 1.6f);

            // A white flash as the card lands, brighter for rarer heroes.
            var flash = Ui.Rect("Flash", root);
            Ui.Stretch(flash);
            var flashImage = Ui.Background(flash, new Color(1f, 1f, 1f, 0f), false);
            var peak = rarity == HeroRarity.Legendary ? 0.9f : rarity == HeroRarity.Elite ? 0.55f : 0.25f;
            LMotion.Create(0f, 1f, 0.7f).WithDelay(0.45f, DelayType.FirstLoop, false).WithScheduler(clock)
                .Bind(t => flashImage.color = new Color(1f, 1f, 1f, peak * (t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f))).AddTo(flash.gameObject);

            var card = HeroCard(root, "RevealCard", best.Hero, best.Roll.copiesOwned <= 1 ? "New hero" : $"You own {best.Roll.copiesOwned}", null, true);
            Ui.Anchor((RectTransform)card.transform, new Vector2(0.5f, 0.5f), new Vector2(300f, 420f), new Vector2(0f, 30f));
            card.GetComponent<Image>().raycastTarget = false;
            UiMotion.PopIn(card.transform, 0.5f, 0.2f, 0.5f);

            var label = rarity == HeroRarity.Legendary ? "LEGENDARY!" : rarity == HeroRarity.Elite ? "ELITE" : "COMMON";
            var banner = HudArt.Label(root, label, HudArt.Display, rarity == HeroRarity.Legendary ? 64 : 48, TextAnchor.MiddleCenter, glow, HudArt.LacquerDeep);
            Ui.Anchor(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(900f, 90f), new Vector2(0f, -40f));
            banner.raycastTarget = false;
            UiMotion.PopIn(banner.transform, 0.85f, 2f, 0.4f);
            if (rarity != HeroRarity.Common) UiMotion.Shine(banner, Color.white, 1.8f, 0.3f);

            var name = HudArt.Label(root, archetype.displayName, HudArt.Display, 34, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Anchor(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(900f, 56f), new Vector2(0f, 74f));
            name.raycastTarget = false;
            UiMotion.FadeIn(name.gameObject, 0.3f, 1.0f);

            var more = result.Rolls.Count > 1 ? $"and {result.Rolls.Count - 1} more.   " : "";
            var hint = HudArt.Label(root, more + "Tap to continue", HudArt.Numbers, 22, TextAnchor.MiddleCenter, HudArt.IvoryMuted, HudArt.LacquerDeep);
            Ui.Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(900f, 40f), new Vector2(0f, 28f));
            hint.raycastTarget = false;
            UiMotion.FadeIn(hint.gameObject, 0.4f, 1.4f);

            GameSounds.Play(rarity == HeroRarity.Legendary ? GameSound.Horn : GameSound.Star);
            if (rarity == HeroRarity.Legendary) UiMotion.Shake(card.GetComponent<RectTransform>());
        }

        public void CloseReveal()
        {
            if (_reveal == null) return;
            Destroy(_reveal);
            _reveal = null;
        }
    }
}
