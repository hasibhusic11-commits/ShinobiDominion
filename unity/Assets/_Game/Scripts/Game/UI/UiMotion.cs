using Coffee.UIEffects;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Small, short motions that make the interface feel alive. All run on real time, so they
    /// stay the same speed when a battle is sped up, and all stop when their object goes away.
    /// None of them change what a button does or when it can be pressed.
    /// </summary>
    public static class UiMotion
    {
        private static IMotionScheduler Clock => MotionScheduler.UpdateIgnoreTimeScale;

        /// <summary>Grows from small to full size with a little overshoot.</summary>
        public static void PopIn(Transform target, float delay = 0f, float from = 0.4f, float duration = 0.32f)
        {
            target.localScale = Vector3.one * from;
            LMotion.Create(from, 1f, duration).WithEase(Ease.OutBack).WithDelay(delay, DelayType.FirstLoop, false).WithScheduler(Clock)
                .BindToLocalScaleXYZ(target).AddTo(target.gameObject);
        }

        /// <summary>Fades a whole panel in through a CanvasGroup, which it adds if needed.</summary>
        public static void FadeIn(GameObject target, float duration = 0.18f, float delay = 0f)
        {
            var group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            LMotion.Create(0f, 1f, duration).WithEase(Ease.OutQuad).WithDelay(delay, DelayType.FirstLoop, false).WithScheduler(Clock)
                .Bind(alpha => group.alpha = alpha).AddTo(target);
        }

        /// <summary>Slides in from an offset to where it already is.</summary>
        public static void SlideIn(RectTransform target, Vector2 offset, float duration = 0.22f, float delay = 0f)
        {
            var end = target.anchoredPosition;
            target.anchoredPosition = end + offset;
            LMotion.Create(end + offset, end, duration).WithEase(Ease.OutCubic).WithDelay(delay, DelayType.FirstLoop, false).WithScheduler(Clock)
                .BindToAnchoredPosition(target).AddTo(target.gameObject);
        }

        /// <summary>A quick bounce, for something that just changed.</summary>
        public static void Punch(Transform target, float strength = 0.2f, float duration = 0.35f)
        {
            // Rapid repeats (coins landing one after another) restart the bounce instead of stacking two on one scale.
            var state = target.GetComponent<UiPunchState>();
            if (state == null) state = target.gameObject.AddComponent<UiPunchState>();
            if (state.Motion.IsActive()) state.Motion.Cancel();
            target.localScale = Vector3.one;
            state.Motion = LMotion.Punch.Create(1f, strength, duration).WithScheduler(Clock).BindToLocalScaleXYZ(target).AddTo(target.gameObject);
        }

        /// <summary>A quick side-to-side shake, for a refused tap. Returns exactly to where it started.</summary>
        public static void Shake(RectTransform target)
        {
            var start = target.anchoredPosition;
            LMotion.Shake.Create(start.x, 7f, 0.28f).WithScheduler(Clock).WithOnComplete(() => { if (target != null) target.anchoredPosition = start; })
                .Bind(x => target.anchoredPosition = new Vector2(x, start.y)).AddTo(target.gameObject);
        }

        /// <summary>A slow endless swell of a glow's opacity up to the given alpha.</summary>
        public static void Breathe(Graphic graphic, float peak, float period)
        {
            var color = graphic.color;
            LMotion.Create(0.05f, peak, period * 0.5f).WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).WithScheduler(Clock)
                .Bind(a => graphic.color = new Color(color.r, color.g, color.b, a)).AddTo(graphic.gameObject);
        }

        /// <summary>Turns text one colour for a moment, then fades it back.</summary>
        public static void Flash(Graphic graphic, Color flash, Color rest)
        {
            graphic.color = flash;
            LMotion.Create(0f, 1f, 0.5f).WithScheduler(Clock).Bind(t => graphic.color = Color.Lerp(flash, rest, t)).AddTo(graphic.gameObject);
        }

        /// <summary>Counts a number from one value to another, calling back with each whole step.</summary>
        public static MotionHandle Count(GameObject owner, long from, long to, float duration, System.Action<long> show)
        {
            return LMotion.Create((double)from, (double)to, duration).WithEase(Ease.OutCubic).WithScheduler(Clock)
                .Bind(v => show((long)System.Math.Round(v))).AddTo(owner);
        }

        /// <summary>A gentle endless breathing, for the one thing the player should press next.</summary>
        public static void Pulse(Transform target, float amount = 0.05f, float period = 1.4f)
        {
            LMotion.Create(1f, 1f + amount, period * 0.5f).WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).WithScheduler(Clock)
                .BindToLocalScaleXYZ(target).AddTo(target.gameObject);
        }

        /// <summary>A band of light that sweeps across a picture every few seconds.</summary>
        public static UIEffect Shine(Graphic graphic, Color color, float interval = 2.2f, float width = 0.25f)
        {
            var effect = graphic.gameObject.GetComponent<UIEffect>();
            if (effect == null) effect = graphic.gameObject.AddComponent<UIEffect>();
            effect.transitionFilter = TransitionFilter.Shiny;
            effect.transitionColor = color;
            effect.transitionWidth = width;
            effect.transitionSoftness = 0.6f;
            effect.transitionRotation = 30f;
            effect.transitionRate = 0f;
            var tweener = graphic.gameObject.GetComponent<UIEffectTweener>();
            if (tweener == null) tweener = graphic.gameObject.AddComponent<UIEffectTweener>();
            tweener.cullingMask = UIEffectTweener.CullingMask.Transition;
            tweener.duration = 0.9f;
            tweener.interval = interval;
            tweener.wrapMode = UIEffectTweener.WrapMode.Loop;
            tweener.updateMode = UIEffectTweener.UpdateMode.Unscaled;
            tweener.playOnEnable = UIEffectTweener.PlayOnEnable.Forward;
            tweener.enabled = false;
            tweener.enabled = true;
            return effect;
        }

        /// <summary>Makes a button sink a little while held, like a real button.</summary>
        public static void Pressable(Button button)
        {
            if (button.GetComponent<UiPress>() == null) button.gameObject.AddComponent<UiPress>();
        }
    }

    /// <summary>Remembers the bounce running on an object, so a new one can replace it.</summary>
    public sealed class UiPunchState : MonoBehaviour
    {
        public MotionHandle Motion;
    }

    /// <summary>Shrinks a button while it is held and springs it back on release.</summary>
    public sealed class UiPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private const float Pressed = 0.93f;
        private MotionHandle _motion;

        public void OnPointerDown(PointerEventData eventData) => To(Pressed, 0.06f, Ease.OutQuad);
        public void OnPointerUp(PointerEventData eventData) => To(1f, 0.22f, Ease.OutBack);

        private void To(float scale, float duration, Ease ease)
        {
            var button = GetComponent<Selectable>();
            if (button != null && !button.interactable) return;
            if (_motion.IsActive()) _motion.Cancel();
            _motion = LMotion.Create(transform.localScale.x, scale, duration).WithEase(ease).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalScaleXYZ(transform).AddTo(gameObject);
        }
    }
}
