using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.Helper;

namespace Flow.Launcher.Tests.Helper;

public class SmoothScrollTests
{
    [Theory]
    [InlineData(-120, 400.0, 3, false, true, 48.0)]
    [InlineData(120, 400.0, 3, false, true, -48.0)]
    [InlineData(-240, 400.0, 3, false, true, 96.0)]
    [InlineData(-1, 400.0, 3, false, true, 0.4)]
    [InlineData(-5, 400.0, 3, false, true, 2.0)]
    [InlineData(15, 400.0, 3, false, true, -6.0)]
    [InlineData(-120, 400.0, -1, false, true, 400.0)]
    [InlineData(-60, 400.0, -1, false, true, 200.0)]
    [InlineData(-120, 400.0, 0, false, true, 0.0)]
    [InlineData(0, 400.0, 3, false, true, 0.0)]
    [InlineData(-120, 400.0, 3, true, true, 3.0)]
    [InlineData(-40, 400.0, 3, true, true, 1.0)]
    [InlineData(120, 400.0, 3, false, false, 48.0)]
    [InlineData(-15, 400.0, 3, false, false, -6.0)]
    public void ComputeOffsetDelta_ReturnsExpectedDelta(
        int wheelDelta,
        double viewportSize,
        int wheelScrollLines,
        bool isLogicalItemScrolling,
        bool invertSign,
        double expected)
    {
        double actual = SmoothScrollAxis.ComputeOffsetDelta(
            wheelDelta,
            viewportSize,
            wheelScrollLines,
            isLogicalItemScrolling,
            invertSign);

        Assert.Equal(expected, actual, precision: 6);
    }

    [Fact]
    public void PrecisionInput_AccumulatesSubPixelDeltasAcrossDeferredLayout()
    {
        SmoothScrollAxis axis = new();
        long now = Stopwatch.GetTimestamp();

        axis.ApplyWheelDelta(0.4, actualOffset: 0.0, scrollableExtent: 500.0, isPrecisionInput: true, isLogicalItemScrolling: false, now, out double? first, out bool bubble1);
        axis.ApplyWheelDelta(0.4, actualOffset: 0.0, scrollableExtent: 500.0, isPrecisionInput: true, isLogicalItemScrolling: false, now, out double? second, out bool bubble2);
        axis.ApplyWheelDelta(0.4, actualOffset: 0.0, scrollableExtent: 500.0, isPrecisionInput: true, isLogicalItemScrolling: false, now, out double? third, out bool bubble3);

        Assert.False(axis.IsAnimating);
        Assert.False(bubble1);
        Assert.False(bubble2);
        Assert.False(bubble3);
        Assert.Equal(0.4, first!.Value, precision: 6);
        Assert.Equal(0.8, second!.Value, precision: 6);
        Assert.Equal(1.2, third!.Value, precision: 6);
        Assert.Equal(1.2, axis.TargetOffset, precision: 6);
        Assert.Equal(1.2, axis.CurrentOffset, precision: 6);
    }

    [Fact]
    public void PrecisionInput_ClampsToBoundsAndCancelsActiveDiscreteAnimation()
    {
        SmoothScrollAxis axis = new();
        long now = Stopwatch.GetTimestamp();

        axis.ApplyWheelDelta(120.0, actualOffset: 0.0, scrollableExtent: 100.0, isPrecisionInput: false, isLogicalItemScrolling: false, now, out _, out _);
        Assert.True(axis.IsAnimating);

        axis.StepAnimation(0.016, scrollableExtent: 100.0, out double? stepped);
        Assert.NotNull(stepped);
        double midOffset = stepped.Value;

        axis.ApplyWheelDelta(200.0, actualOffset: midOffset, scrollableExtent: 100.0, isPrecisionInput: true, isLogicalItemScrolling: false, now, out double? clampedUp, out _);
        Assert.False(axis.IsAnimating);
        Assert.Equal(100.0, clampedUp!.Value, precision: 6);

        axis.ApplyWheelDelta(-200.0, actualOffset: 100.0, scrollableExtent: 100.0, isPrecisionInput: true, isLogicalItemScrolling: false, now, out double? clampedDown, out _);
        Assert.Equal(0.0, clampedDown!.Value, precision: 6);
    }

    [Fact]
    public void DiscreteWheel_AnimatesMonotonicallyAndSnapsToTarget()
    {
        SmoothScrollAxis axis = new();
        long now = Stopwatch.GetTimestamp();

        axis.ApplyWheelDelta(48.0, actualOffset: 0.0, scrollableExtent: 500.0, isPrecisionInput: false, isLogicalItemScrolling: false, now, out double? immediate, out bool bubble);

        Assert.Null(immediate);
        Assert.False(bubble);
        Assert.True(axis.IsAnimating);
        Assert.Equal(48.0, axis.TargetOffset, precision: 6);

        double previous = 0.0;
        int steps = 0;
        while (axis.IsAnimating && steps < 120)
        {
            axis.StepAnimation(0.016, scrollableExtent: 500.0, out double? next);
            Assert.NotNull(next);
            Assert.True(next.Value >= previous);
            Assert.True(next.Value <= 48.0);
            previous = next.Value;
            steps++;
        }

        Assert.False(axis.IsAnimating);
        Assert.Equal(48.0, previous, precision: 6);
        Assert.Equal(48.0, axis.CurrentOffset, precision: 6);
    }

    [Fact]
    public void DiscreteWheel_IsFrameRateIndependent()
    {
        SmoothScrollAxis singleStepAxis = new();
        SmoothScrollAxis doubleStepAxis = new();
        long now = Stopwatch.GetTimestamp();

        singleStepAxis.ApplyWheelDelta(96.0, 0.0, 500.0, isPrecisionInput: false, isLogicalItemScrolling: false, now, out _, out _);
        doubleStepAxis.ApplyWheelDelta(96.0, 0.0, 500.0, isPrecisionInput: false, isLogicalItemScrolling: false, now, out _, out _);

        singleStepAxis.StepAnimation(0.016, 500.0, out double? singleResult);
        doubleStepAxis.StepAnimation(0.008, 500.0, out _);
        doubleStepAxis.StepAnimation(0.008, 500.0, out double? doubleResult);

        Assert.Equal(singleResult!.Value, doubleResult!.Value, precision: 6);
    }

    [Fact]
    public void DiscreteWheel_RapidClicksAccumulateAndDirectionReversalAnchorsAtCurrentOffset()
    {
        SmoothScrollAxis axis = new();
        long now = Stopwatch.GetTimestamp();

        axis.ApplyWheelDelta(48.0, 0.0, 500.0, isPrecisionInput: false, isLogicalItemScrolling: false, now, out _, out _);
        axis.ApplyWheelDelta(48.0, 0.0, 500.0, isPrecisionInput: false, isLogicalItemScrolling: false, now, out _, out _);
        Assert.Equal(96.0, axis.TargetOffset, precision: 6);

        axis.StepAnimation(0.016, 500.0, out double? mid);
        Assert.NotNull(mid);

        // Reversing direction mid-animation should anchor target from CurrentOffset, not TargetOffset (96.0).
        axis.ApplyWheelDelta(-12.0, mid.Value, 500.0, isPrecisionInput: false, isLogicalItemScrolling: false, now, out _, out _);
        Assert.Equal(Math.Max(0.0, mid.Value - 12.0), axis.TargetOffset, precision: 6);
    }

    [Fact]
    public void OnScrollChanged_DistinguishesPendingApplyFromExternalScroll()
    {
        SmoothScrollAxis axis = new();
        long now = Stopwatch.GetTimestamp();

        axis.ApplyWheelDelta(96.0, 0.0, 500.0, isPrecisionInput: false, isLogicalItemScrolling: false, now, out _, out _);
        axis.StepAnimation(0.016, 500.0, out double? firstFrame);
        Assert.NotNull(firstFrame);
        Assert.True(axis.IsAnimating);

        // Matching ScrollChanged from our own ScrollToVerticalOffset should keep animation running.
        axis.OnScrollChanged(firstFrame.Value, firstFrame.Value, 500.0);
        Assert.True(axis.IsAnimating);
        Assert.Equal(96.0, axis.TargetOffset, precision: 6);

        // External ScrollToTop() / scrollbar drag cancels animation and syncs both offsets.
        axis.OnScrollChanged(0.0, -firstFrame.Value, 500.0);
        Assert.False(axis.IsAnimating);
        Assert.Equal(0.0, axis.CurrentOffset, precision: 6);
        Assert.Equal(0.0, axis.TargetOffset, precision: 6);

        // Dynamic extent shrink clamps target and current offsets.
        axis.ApplyWheelDelta(300.0, 0.0, 500.0, isPrecisionInput: false, isLogicalItemScrolling: false, now, out _, out _);
        axis.OnScrollChanged(0.0, 0.0, 120.0);
        Assert.Equal(120.0, axis.TargetOffset, precision: 6);
    }

    [Fact]
    public void LogicalItemScrolling_AccumulatesFractionalStepsAndResetsOnReversal()
    {
        SmoothScrollAxis axis = new();
        long now = Stopwatch.GetTimestamp();

        axis.ApplyWheelDelta(0.4, 0.0, 20.0, isPrecisionInput: true, isLogicalItemScrolling: true, now, out double? step1, out _);
        axis.ApplyWheelDelta(0.4, 0.0, 20.0, isPrecisionInput: true, isLogicalItemScrolling: true, now, out double? step2, out _);
        axis.ApplyWheelDelta(0.4, 0.0, 20.0, isPrecisionInput: true, isLogicalItemScrolling: true, now, out double? step3, out _);

        Assert.Null(step1);
        Assert.Null(step2);
        Assert.Equal(1.0, step3!.Value, precision: 6);

        // Remaining fractional +0.2 should be cleared when scrolling in the opposite direction.
        axis.ApplyWheelDelta(-0.4, 1.0, 20.0, isPrecisionInput: true, isLogicalItemScrolling: true, now, out double? reverse1, out _);
        Assert.Null(reverse1);

        axis.ApplyWheelDelta(-0.7, 1.0, 20.0, isPrecisionInput: true, isLogicalItemScrolling: true, now, out double? reverse2, out _);
        Assert.Equal(0.0, reverse2!.Value, precision: 6);
    }

    [Fact]
    public void NestedBoundaryHysteresis_AbsorbsInertiaMidGestureThenBubblesAfterPause()
    {
        SmoothScrollAxis axis = new();
        long t0 = 1_000_000;
        long freq = Stopwatch.Frequency;

        // Zero scrollable extent bubbles immediately.
        axis.ApplyWheelDelta(10.0, 0.0, 0.0, isPrecisionInput: true, isLogicalItemScrolling: false, t0, out _, out bool zeroExtentBubble);
        Assert.True(zeroExtentBubble);

        // Already idle at start boundary bubbles immediately when scrolling up.
        axis.ApplyWheelDelta(-10.0, 0.0, 100.0, isPrecisionInput: true, isLogicalItemScrolling: false, t0, out _, out bool idleBoundaryBubble);
        Assert.True(idleBoundaryBubble);

        // Scroll down within bounds, hit bottom, and keep sending inertia events < 200ms apart.
        axis.ApplyWheelDelta(50.0, 80.0, 100.0, isPrecisionInput: true, isLogicalItemScrolling: false, t0, out double? hitBottom, out bool hitBubble);
        Assert.Equal(100.0, hitBottom!.Value, precision: 6);
        Assert.False(hitBubble);

        long tMidInertia = t0 + (long)(0.05 * freq);
        axis.ApplyWheelDelta(10.0, 100.0, 100.0, isPrecisionInput: true, isLogicalItemScrolling: false, tMidInertia, out double? absorbed, out bool inertiaBubble);
        Assert.Null(absorbed);
        Assert.False(inertiaBubble);

        // After >= 200ms idle pause, scrolling past the boundary bubbles to parent.
        long tAfterPause = tMidInertia + (long)(0.25 * freq);
        axis.ApplyWheelDelta(10.0, 100.0, 100.0, isPrecisionInput: true, isLogicalItemScrolling: false, tAfterPause, out _, out bool pauseBubble);
        Assert.True(pauseBubble);
    }

    [Fact]
    public void ClassifyPrecisionInput_DetectsSubDetentsAndGestureWindow()
    {
        long lastPrecisionTicks = 0;
        long t0 = 10_000_000;
        long freq = Stopwatch.Frequency;

        Assert.False(SmoothScrollHelper.ClassifyPrecisionInput(-120, t0, ref lastPrecisionTicks));
        Assert.True(SmoothScrollHelper.ClassifyPrecisionInput(-6, t0 + (long)(0.01 * freq), ref lastPrecisionTicks));

        // Exact 120 delta within 250ms of sub-detent input stays classified as precision.
        Assert.True(SmoothScrollHelper.ClassifyPrecisionInput(-120, t0 + (long)(0.10 * freq), ref lastPrecisionTicks));

        // Exact 120 delta after >= 250ms pause returns to discrete classification.
        Assert.False(SmoothScrollHelper.ClassifyPrecisionInput(-120, t0 + (long)(0.40 * freq), ref lastPrecisionTicks));
    }

    [Fact]
    public void ScrollViewer_PrecisionTouchpadWheel_ScrollsProportionallyWithoutJumping()
    {
        RunOnStaThread(() =>
        {
            SmoothScrollHelper.Initialize();

            ScrollViewer scrollViewer = CreateMeasuredScrollViewer(viewportHeight: 100, contentHeight: 1000);
            int delta = -6;
            double expected = SmoothScrollAxis.ComputeOffsetDelta(
                delta,
                scrollViewer.ViewportHeight,
                SystemParameters.WheelScrollLines,
                isLogicalItemScrolling: false,
                invertSign: true);

            MouseWheelEventArgs args = new(Mouse.PrimaryDevice, 0, delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent
            };
            scrollViewer.RaiseEvent(args);
            scrollViewer.UpdateLayout();

            Assert.True(args.Handled);
            Assert.Equal(expected, scrollViewer.VerticalOffset, precision: 6);
            if (SystemParameters.WheelScrollLines == 3)
                Assert.Equal(2.4, scrollViewer.VerticalOffset, precision: 6);
        });
    }

    [Fact]
    public void ScrollViewer_WhenSmoothScrollDisabled_UsesDefaultWpfBehavior()
    {
        RunOnStaThread(() =>
        {
            SmoothScrollHelper.Initialize();

            ScrollViewer scrollViewer = CreateMeasuredScrollViewer(viewportHeight: 100, contentHeight: 1000);
            SmoothScrollHelper.SetIsEnabled(scrollViewer, false);

            MouseWheelEventArgs args = new(Mouse.PrimaryDevice, 0, -6)
            {
                RoutedEvent = UIElement.MouseWheelEvent
            };
            scrollViewer.RaiseEvent(args);
            scrollViewer.UpdateLayout();

            Assert.True(args.Handled);
            // Default WPF ScrollViewer ignores sub-detent granularity and jumps by a full wheel step (48 DIPs for 3 lines).
            int expectedLines = SystemParameters.WheelScrollLines > 0 ? SystemParameters.WheelScrollLines : 3;
            Assert.Equal(expectedLines * 16.0, scrollViewer.VerticalOffset, precision: 6);
        });
    }

    private static ScrollViewer CreateMeasuredScrollViewer(double viewportHeight, double contentHeight)
    {
        ScrollViewer scrollViewer = new()
        {
            Width = 200,
            Height = viewportHeight,
            Content = new Border
            {
                Width = 200,
                Height = contentHeight
            }
        };

        scrollViewer.Measure(new Size(200, viewportHeight));
        scrollViewer.Arrange(new Rect(0, 0, 200, viewportHeight));
        scrollViewer.UpdateLayout();
        return scrollViewer;
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? captured = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
            ExceptionDispatchInfo.Capture(captured).Throw();
    }
}
