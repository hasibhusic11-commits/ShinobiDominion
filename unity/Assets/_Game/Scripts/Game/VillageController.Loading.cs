using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The loading screen while the game first reaches the server, so players never see the village swap
    /// from this device's copy to the server's. It lifts the moment the server answers, or fails to.
    /// </summary>
    public sealed partial class VillageController
    {
        private GameObject _loading;

        public bool LoadingShowing => _loading != null && _loading.activeSelf;

        private void ShowLoading()
        {
            if (_loading != null || _uiRoot == null) return;
            var canvas = _uiRoot.GetComponentInParent<Canvas>();
            var root = Ui.Rect("LoadingScreen", canvas != null ? canvas.transform : _uiRoot);
            Ui.Stretch(root);
            root.SetAsLastSibling();
            _loading = root.gameObject;
            Ui.Background(root, new Color(0.05f, 0.05f, 0.1f, 1f));

            var glow = Ui.Icon(root, HeroArt.Glow, new Vector2(0.5f, 0.5f), new Vector2(900f, 900f), new Vector2(0f, 40f));
            glow.color = new Color(1f, 0.6f, 0.25f, 0.35f);
            glow.raycastTarget = false;
            UiMotion.Breathe(glow, 0.45f, 2.4f);

            var title = HudArt.Label(root, "SHINOBI DOMINION", HudArt.Display, 72, TextAnchor.MiddleCenter, HudArt.BrassLight, HudArt.LacquerDeep);
            Ui.Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1200f, 110f), new Vector2(0f, 60f));
            title.raycastTarget = false;
            UiMotion.PopIn(title.transform, 0f, 0.85f, 0.5f);

            var status = HudArt.Label(root, "Gathering your village", HudArt.Numbers, 24, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Anchor(status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(800f, 40f), new Vector2(0f, -30f));
            status.raycastTarget = false;

            // Three lanterns that light in turn, so the wait never looks frozen.
            for (var i = 0; i < 3; i++)
            {
                var dot = Ui.Icon(root, UiSkin.Disc(new Color(1f, 0.85f, 0.45f), new Color(0.9f, 0.5f, 0.15f), new Color(0.35f, 0.15f, 0.05f)), new Vector2(0.5f, 0.5f), new Vector2(18f, 18f), new Vector2((i - 1) * 34f, -80f));
                dot.raycastTarget = false;
                var image = dot;
                var offset = i * 0.33f;
                LMotion.Create(0f, 1f, 1f).WithLoops(-1).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(t => image.color = new Color(1f, 1f, 1f, 0.25f + 0.75f * Mathf.Clamp01(1f - Mathf.Abs(Mathf.Repeat(t - offset, 1f) - 0.2f) * 3f)))
                    .AddTo(dot.gameObject);
            }
        }

        private void HideLoading()
        {
            if (_loading == null) return;
            var leaving = _loading;
            _loading = null;
            // Taps go straight to the village while the screen fades away.
            foreach (var graphic in leaving.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            var group = leaving.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            LMotion.Create(1f, 0f, 0.35f).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale).WithOnComplete(() => { if (leaving != null) Destroy(leaving); })
                .Bind(a => group.alpha = a).AddTo(leaving);
        }
    }
}
