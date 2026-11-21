using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Flow.Launcher.Interop;

namespace Flow.Launcher.Helper;

/// <summary>
/// Provides global smooth scrolling and precision touchpad support (vertical and horizontal)
/// for all WPF <see cref="ScrollViewer"/> instances without altering their control styles or templates.
/// </summary>
public static class SmoothScrollHelper
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(SmoothScrollHelper),
        new PropertyMetadata(true));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static readonly Func<ScrollViewer, bool>? s_getHandlesMouseWheelScrolling =
        typeof(ScrollViewer).GetProperty("HandlesMouseWheelScrolling", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetGetMethod(true)
            ?.CreateDelegate<Func<ScrollViewer, bool>>();

    private static readonly ConditionalWeakTable<ScrollViewer, SmoothScrollState> s_states = [];
    private static readonly ConditionalWeakTable<HwndSource, HwndSourceHook> s_hwndHooks = [];
    private static readonly List<SmoothScrollState> s_activeStates = [];
    private static readonly List<SmoothScrollState> s_renderSnapshot = [];
    private static readonly TimeSpan PrecisionGestureWindow = TimeSpan.FromMilliseconds(250);

    private static bool s_initialized;
    private static bool s_isRenderingSubscribed;
    private static long s_lastPrecisionInputTicks;
    private static long s_lastRenderTicks;
    private static TimeSpan s_lastRenderingTime;

    private sealed class SmoothScrollState(ScrollViewer scrollViewer)
    {
        public WeakReference<ScrollViewer> ScrollViewer { get; } = new(scrollViewer);

        public SmoothScrollAxis Vertical { get; } = new();

        public SmoothScrollAxis Horizontal { get; } = new();

        public bool IsActive { get; set; }

        public bool HasActiveAnimation => Vertical.IsAnimating || Horizontal.IsAnimating;
    }

    /// <summary>
    /// Registers class handlers on <see cref="ScrollViewer"/> to enable smooth scrolling globally.
    /// </summary>
    public static void Initialize()
    {
        if (s_initialized)
            return;

        s_initialized = true;

        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            UIElement.MouseWheelEvent,
            new MouseWheelEventHandler(OnScrollViewerMouseWheel));

        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            ScrollViewer.ScrollChangedEvent,
            new ScrollChangedEventHandler(OnScrollViewerScrollChanged));

        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnScrollViewerLoaded));

        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            FrameworkElement.UnloadedEvent,
            new RoutedEventHandler(OnScrollViewerUnloaded));
    }

    /// <summary>
    /// Classifies whether a wheel delta originates from a precision touchpad / high-resolution wheel
    /// versus a discrete notched wheel.
    /// </summary>
    internal static bool ClassifyPrecisionInput(int wheelDelta, long nowTicks, ref long lastPrecisionTicks)
    {
        bool isSubDetent = (wheelDelta % Mouse.MouseWheelDeltaForOneLine) != 0;
        if (isSubDetent)
        {
            lastPrecisionTicks = nowTicks;
            return true;
        }

        return lastPrecisionTicks != 0
            && Stopwatch.GetElapsedTime(lastPrecisionTicks, nowTicks) < PrecisionGestureWindow;
    }

    private static void OnScrollViewerMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not ScrollViewer scrollViewer || !ShouldHandleScrollViewer(scrollViewer))
            return;

        if (e.Delta == 0)
            return;

        // Guard against precision touchpad pinch-to-zoom (Ctrl+MouseWheel) jumping the scroll position.
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            e.Handled = true;
            return;
        }

        long nowTicks = Stopwatch.GetTimestamp();
        bool isPrecisionInput = ClassifyPrecisionInput(e.Delta, nowTicks, ref s_lastPrecisionInputTicks);
        bool isShiftHorizontal = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        e.Handled = true;

        if (isShiftHorizontal)
        {
            if (CanScrollHorizontally(scrollViewer))
            {
                HandleAxisScroll(
                    scrollViewer,
                    isHorizontal: true,
                    wheelDelta: e.Delta,
                    invertSign: true,
                    isPrecisionInput,
                    nowTicks,
                    out bool shouldBubble);

                if (shouldBubble && HasScrollableAncestor(scrollViewer, isHorizontal: true))
                    BubbleMouseWheelToParent(scrollViewer, e);
            }
            else if (HasScrollableAncestor(scrollViewer, isHorizontal: true))
            {
                BubbleMouseWheelToParent(scrollViewer, e);
            }

            return;
        }

        if (CanScrollVertically(scrollViewer))
        {
            HandleAxisScroll(
                scrollViewer,
                isHorizontal: false,
                wheelDelta: e.Delta,
                invertSign: true,
                isPrecisionInput,
                nowTicks,
                out bool shouldBubble);

            if (shouldBubble && HasScrollableAncestor(scrollViewer, isHorizontal: false))
                BubbleMouseWheelToParent(scrollViewer, e);
        }
        else if (HasScrollableAncestor(scrollViewer, isHorizontal: false))
        {
            BubbleMouseWheelToParent(scrollViewer, e);
        }
    }

    private static void HandleAxisScroll(
        ScrollViewer scrollViewer,
        bool isHorizontal,
        int wheelDelta,
        bool invertSign,
        bool isPrecisionInput,
        long nowTicks,
        out bool shouldBubbleToParent)
    {
        SmoothScrollState state = s_states.GetValue(scrollViewer, static sv => new SmoothScrollState(sv));
        SmoothScrollAxis axis = isHorizontal ? state.Horizontal : state.Vertical;
        bool isLogicalItemScrolling = IsLogicalItemScrolling(scrollViewer);

        int systemLines = SystemParameters.WheelScrollLines;
        int wheelScrollLines = isHorizontal
            ? (systemLines > 0 ? systemLines : SmoothScrollAxis.DefaultScrollLines)
            : systemLines;

        double viewportSize = isHorizontal ? scrollViewer.ViewportWidth : scrollViewer.ViewportHeight;
        double actualOffset = isHorizontal ? scrollViewer.HorizontalOffset : scrollViewer.VerticalOffset;
        double scrollableExtent = isHorizontal ? scrollViewer.ScrollableWidth : scrollViewer.ScrollableHeight;

        double offsetDelta = SmoothScrollAxis.ComputeOffsetDelta(
            wheelDelta,
            viewportSize,
            wheelScrollLines,
            isLogicalItemScrolling,
            invertSign);

        axis.ApplyWheelDelta(
            offsetDelta,
            actualOffset,
            scrollableExtent,
            isPrecisionInput,
            isLogicalItemScrolling,
            nowTicks,
            out double? offsetToApply,
            out shouldBubbleToParent);

        if (offsetToApply.HasValue)
        {
            if (isHorizontal)
                scrollViewer.ScrollToHorizontalOffset(offsetToApply.Value);
            else
                scrollViewer.ScrollToVerticalOffset(offsetToApply.Value);
        }

        if (axis.IsAnimating)
            ActivateAnimation(state);
        else
            DeactivateStateIfIdle(state);
    }

    private static void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || !s_states.TryGetValue(scrollViewer, out SmoothScrollState? state))
            return;

        state.Vertical.OnScrollChanged(e.VerticalOffset, e.VerticalChange, scrollViewer.ScrollableHeight);
        state.Horizontal.OnScrollChanged(e.HorizontalOffset, e.HorizontalChange, scrollViewer.ScrollableWidth);

        DeactivateStateIfIdle(state);
    }

    private static void OnScrollViewerLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer && PresentationSource.FromVisual(scrollViewer) is HwndSource hwndSource)
            EnsureHorizontalWheelHook(hwndSource);
    }

    private static void OnScrollViewerUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || !s_states.TryGetValue(scrollViewer, out SmoothScrollState? state))
            return;

        state.Vertical.CancelAnimation(scrollViewer.VerticalOffset, scrollViewer.ScrollableHeight);
        state.Horizontal.CancelAnimation(scrollViewer.HorizontalOffset, scrollViewer.ScrollableWidth);

        if (state.IsActive)
            RemoveActiveState(state);
    }

    private static void EnsureHorizontalWheelHook(HwndSource hwndSource)
    {
        if (s_hwndHooks.TryGetValue(hwndSource, out _))
            return;

        HwndSourceHook hook = OnHwndMessage;
        s_hwndHooks.Add(hwndSource, hook);
        hwndSource.AddHook(hook);
        hwndSource.Disposed += OnHwndSourceDisposed;
    }

    private static void OnHwndSourceDisposed(object? sender, EventArgs e)
    {
        if (sender is not HwndSource hwndSource)
            return;

        hwndSource.Disposed -= OnHwndSourceDisposed;
        if (s_hwndHooks.TryGetValue(hwndSource, out HwndSourceHook? hook))
        {
            hwndSource.RemoveHook(hook);
            s_hwndHooks.Remove(hwndSource);
        }
    }

    private static IntPtr OnHwndMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WindowHelper.WM_MOUSEHWHEEL || handled)
            return IntPtr.Zero;

        int delta = unchecked((short)((wParam.ToInt64() >> 16) & 0xFFFF));
        if (delta == 0)
            return IntPtr.Zero;

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            handled = true;
            return IntPtr.Zero;
        }

        if (HwndSource.FromHwnd(hwnd) is not { RootVisual: UIElement rootElement })
            return IntPtr.Zero;

        long lParamValue = lParam.ToInt64();
        int screenX = unchecked((short)(lParamValue & 0xFFFF));
        int screenY = unchecked((short)((lParamValue >> 16) & 0xFFFF));

        DependencyObject? hitElement = null;
        try
        {
            Point localPoint = rootElement.PointFromScreen(new Point(screenX, screenY));
            hitElement = rootElement.InputHitTest(localPoint) as DependencyObject;
        }
        catch (InvalidOperationException)
        {
        }

        hitElement ??= Mouse.DirectlyOver as DependencyObject;
        if (hitElement is null)
            return IntPtr.Zero;

        long nowTicks = Stopwatch.GetTimestamp();
        bool isPrecisionInput = ClassifyPrecisionInput(delta, nowTicks, ref s_lastPrecisionInputTicks);

        ScrollViewer? current = FindScrollViewerAncestor(hitElement);
        while (current is not null)
        {
            if (ShouldHandleScrollViewer(current) && CanScrollHorizontally(current))
            {
                HandleAxisScroll(
                    current,
                    isHorizontal: true,
                    wheelDelta: delta,
                    invertSign: false,
                    isPrecisionInput,
                    nowTicks,
                    out bool shouldBubble);

                if (!shouldBubble)
                {
                    handled = true;
                    break;
                }
            }

            current = FindScrollViewerAncestor(GetParentObject(current));
        }

        return IntPtr.Zero;
    }

    private static void ActivateAnimation(SmoothScrollState state)
    {
        if (!state.IsActive)
        {
            state.IsActive = true;
            s_activeStates.Add(state);
        }

        if (!s_isRenderingSubscribed)
        {
            s_isRenderingSubscribed = true;
            s_lastRenderTicks = Stopwatch.GetTimestamp();
            s_lastRenderingTime = TimeSpan.MinValue;
            CompositionTarget.Rendering += OnRendering;
        }
    }

    private static void DeactivateStateIfIdle(SmoothScrollState state)
    {
        if (state.IsActive && !state.HasActiveAnimation)
            RemoveActiveState(state);
    }

    private static void RemoveActiveState(SmoothScrollState state)
    {
        state.IsActive = false;
        s_activeStates.Remove(state);

        if (s_activeStates.Count == 0 && s_isRenderingSubscribed)
        {
            s_isRenderingSubscribed = false;
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private static void OnRendering(object? sender, EventArgs e)
    {
        if (e is RenderingEventArgs renderingArgs)
        {
            if (renderingArgs.RenderingTime == s_lastRenderingTime)
                return;

            s_lastRenderingTime = renderingArgs.RenderingTime;
        }

        long nowTicks = Stopwatch.GetTimestamp();
        double dtSeconds = Stopwatch.GetElapsedTime(s_lastRenderTicks, nowTicks).TotalSeconds;
        s_lastRenderTicks = nowTicks;

        s_renderSnapshot.Clear();
        s_renderSnapshot.AddRange(s_activeStates);

        try
        {
            foreach (SmoothScrollState state in s_renderSnapshot)
            {
                if (!state.IsActive)
                    continue;

                if (!state.ScrollViewer.TryGetTarget(out ScrollViewer? scrollViewer)
                    || !scrollViewer.IsLoaded
                    || !scrollViewer.IsVisible
                    || !GetIsEnabled(scrollViewer))
                {
                    if (scrollViewer is not null)
                    {
                        state.Vertical.CancelAnimation(scrollViewer.VerticalOffset, scrollViewer.ScrollableHeight);
                        state.Horizontal.CancelAnimation(scrollViewer.HorizontalOffset, scrollViewer.ScrollableWidth);
                    }

                    state.IsActive = false;
                    s_activeStates.Remove(state);
                    continue;
                }

                state.Vertical.StepAnimation(dtSeconds, scrollViewer.ScrollableHeight, out double? verticalOffset);
                state.Horizontal.StepAnimation(dtSeconds, scrollViewer.ScrollableWidth, out double? horizontalOffset);

                if (verticalOffset.HasValue)
                    scrollViewer.ScrollToVerticalOffset(verticalOffset.Value);

                if (horizontalOffset.HasValue)
                    scrollViewer.ScrollToHorizontalOffset(horizontalOffset.Value);

                if (!state.HasActiveAnimation && state.IsActive)
                {
                    state.IsActive = false;
                    s_activeStates.Remove(state);
                }
            }
        }
        finally
        {
            s_renderSnapshot.Clear();
        }

        if (s_activeStates.Count == 0 && s_isRenderingSubscribed)
        {
            s_isRenderingSubscribed = false;
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private static bool ShouldHandleScrollViewer(ScrollViewer scrollViewer)
    {
        if (!GetIsEnabled(scrollViewer))
            return false;

        return s_getHandlesMouseWheelScrolling?.Invoke(scrollViewer)
            ?? (scrollViewer.TemplatedParent is not TextBoxBase and not PasswordBox);
    }

    private static bool CanScrollVertically(ScrollViewer scrollViewer) =>
        scrollViewer.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled
        && scrollViewer.ScrollableHeight > 0;

    private static bool CanScrollHorizontally(ScrollViewer scrollViewer) =>
        scrollViewer.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled
        && scrollViewer.ScrollableWidth > 0;

    private static bool IsLogicalItemScrolling(ScrollViewer scrollViewer)
    {
        if (!scrollViewer.CanContentScroll)
            return false;

        if (scrollViewer.TemplatedParent is ItemsControl itemsControl)
        {
            if (VirtualizingPanel.GetIsVirtualizing(itemsControl)
                && VirtualizingPanel.GetScrollUnit(itemsControl) == ScrollUnit.Pixel)
            {
                return false;
            }

            if (scrollViewer.Content is VirtualizingPanel panel
                && VirtualizingPanel.GetScrollUnit(panel) == ScrollUnit.Pixel)
            {
                return false;
            }

            return true;
        }

        if (scrollViewer.Content is VirtualizingPanel directPanel)
            return VirtualizingPanel.GetScrollUnit(directPanel) != ScrollUnit.Pixel;

        return scrollViewer.Content is IScrollInfo;
    }

    private static ScrollViewer? FindScrollViewerAncestor(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is ScrollViewer scrollViewer)
                return scrollViewer;

            current = GetParentObject(current);
        }

        return null;
    }

    private static bool HasScrollableAncestor(ScrollViewer scrollViewer, bool isHorizontal)
    {
        DependencyObject? current = GetParentObject(scrollViewer);
        while (current is not null)
        {
            if (current is ScrollViewer ancestor
                && ShouldHandleScrollViewer(ancestor)
                && (isHorizontal ? CanScrollHorizontally(ancestor) : CanScrollVertically(ancestor)))
            {
                return true;
            }

            current = GetParentObject(current);
        }

        return false;
    }

    private static IInputElement? FindVisualParentInputElement(DependencyObject child)
    {
        DependencyObject? current = GetParentObject(child);
        while (current is not null)
        {
            if (current is IInputElement inputElement)
                return inputElement;

            current = GetParentObject(current);
        }

        return null;
    }

    private static DependencyObject? GetParentObject(DependencyObject child)
    {
        if (child is Visual or Visual3D)
            return VisualTreeHelper.GetParent(child);

        if (child is FrameworkContentElement fce)
            return fce.Parent ?? ContentOperations.GetParent(fce);

        if (child is ContentElement ce)
            return ContentOperations.GetParent(ce);

        return LogicalTreeHelper.GetParent(child);
    }

    private static void BubbleMouseWheelToParent(ScrollViewer scrollViewer, MouseWheelEventArgs e)
    {
        if (FindVisualParentInputElement(scrollViewer) is not { } parent)
            return;

        MouseWheelEventArgs routedEventArgs = new(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = scrollViewer
        };
        parent.RaiseEvent(routedEventArgs);
    }
}
