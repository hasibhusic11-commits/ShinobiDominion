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
            target.localScale = Vector3.one;
            LMotion.Punch.Create(1f, strength, duration).WithScheduler(Clock).BindToLocalScaleXYZ(target).AddTo(target.gameObject);
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
