using System;
using System.Runtime.CompilerServices;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Small DOTween helpers for interruptible UI Toolkit panel transitions.</summary>
public static class UiPanelMotion
{
    private sealed class MotionState
    {
        public Tween Tween;
        public bool Initialized;
        public bool AtRestHidden;
    }

    private static readonly ConditionalWeakTable<VisualElement, MotionState> States = new();

    public static void MarkHidden(VisualElement element)
    {
        if (element == null) return;

        var state = GetState(element);
        state.Tween?.Kill();
        state.Tween = null;
        state.Initialized = true;
        state.AtRestHidden = true;
        element.style.opacity = 1f;
        element.style.translate = new Translate(0f, 0f, 0f);
    }

    public static void Show(
        VisualElement element,
        Vector2 fromOffset,
        float duration,
        bool fade = true,
        bool move = true,
        bool manageDisplay = true)
    {
        if (element == null) return;

        var state = GetState(element);
        state.Tween?.Kill();
        state.Tween = null;

        bool displayHidden = manageDisplay && element.resolvedStyle.display == DisplayStyle.None;
        bool startFromRest = state.AtRestHidden || displayHidden;
        if (manageDisplay && displayHidden)
            element.style.display = DisplayStyle.Flex;

        if (startFromRest)
        {
            if (fade) element.style.opacity = 0f;
            if (move) element.style.translate = new Translate(fromOffset.x, fromOffset.y, 0f);
        }
        element.SetEnabled(true);

        float startOpacity = element.resolvedStyle.opacity;
        Vector3 startOffset = element.resolvedStyle.translate;
        state.Initialized = true;
        state.AtRestHidden = false;

        if (duration <= 0f)
        {
            if (fade) element.style.opacity = 1f;
            if (move) element.style.translate = new Translate(0f, 0f, 0f);
            return;
        }

        var sequence = DOTween.Sequence().SetUpdate(true).SetTarget(element);
        bool hasTween = false;
        if (fade)
        {
            sequence.Append(DOTween.To(
                    () => startOpacity,
                    value => element.style.opacity = value,
                    1f,
                    duration)
                .SetEase(Ease.OutCubic)
                .SetTarget(element));
            hasTween = true;
        }

        if (move)
        {
            var moveTween = DOTween.To(
                    () => startOffset,
                    value => element.style.translate = new Translate(value.x, value.y, value.z),
                    Vector3.zero,
                    duration)
                .SetEase(Ease.OutCubic)
                .SetTarget(element);
            if (hasTween) sequence.Join(moveTween);
            else sequence.Append(moveTween);
        }

        state.Tween = sequence;
        sequence.OnComplete(() =>
        {
            if (state.Tween != sequence) return;
            state.Tween = null;
            state.AtRestHidden = false;
            if (fade) element.style.opacity = 1f;
            if (move) element.style.translate = new Translate(0f, 0f, 0f);
        });
    }

    public static void Hide(
        VisualElement element,
        Vector2 toOffset,
        float duration,
        Action onComplete = null,
        bool fade = true,
        bool move = true,
        bool hideElement = true)
    {
        if (element == null)
        {
            onComplete?.Invoke();
            return;
        }

        var state = GetState(element);
        state.Tween?.Kill();
        state.Tween = null;

        if (!state.Initialized)
        {
            state.Initialized = true;
            state.AtRestHidden = element.resolvedStyle.display == DisplayStyle.None;
        }

        if (state.AtRestHidden)
        {
            onComplete?.Invoke();
            return;
        }

        float startOpacity = element.resolvedStyle.opacity;
        Vector3 startOffset = element.resolvedStyle.translate;
        element.SetEnabled(false);

        void Finish()
        {
            state.Tween = null;
            state.AtRestHidden = true;
            if (hideElement) element.style.display = DisplayStyle.None;
            if (fade) element.style.opacity = 1f;
            if (move) element.style.translate = new Translate(0f, 0f, 0f);
            element.SetEnabled(true);
            onComplete?.Invoke();
        }

        if (duration <= 0f || (!fade && !move))
        {
            Finish();
            return;
        }

        var sequence = DOTween.Sequence().SetUpdate(true).SetTarget(element);
        bool hasTween = false;
        if (fade)
        {
            sequence.Append(DOTween.To(
                    () => startOpacity,
                    value => element.style.opacity = value,
                    0f,
                    duration)
                .SetEase(Ease.InQuad)
                .SetTarget(element));
            hasTween = true;
        }

        if (move)
        {
            var moveTween = DOTween.To(
                    () => startOffset,
                    value => element.style.translate = new Translate(value.x, value.y, value.z),
                    new Vector3(toOffset.x, toOffset.y, 0f),
                    duration)
                .SetEase(Ease.InQuad)
                .SetTarget(element);
            if (hasTween) sequence.Join(moveTween);
            else sequence.Append(moveTween);
        }

        state.Tween = sequence;
        sequence.OnComplete(() =>
        {
            if (state.Tween != sequence) return;
            Finish();
        });
    }

    public static void Kill(VisualElement element)
    {
        if (element == null || !States.TryGetValue(element, out var state)) return;
        state.Tween?.Kill();
        state.Tween = null;
    }

    private static MotionState GetState(VisualElement element)
    {
        var state = States.GetValue(element, _ => new MotionState());
        if (state.Initialized) return state;

        state.Initialized = true;
        state.AtRestHidden = element.resolvedStyle.display == DisplayStyle.None;
        return state;
    }
}
