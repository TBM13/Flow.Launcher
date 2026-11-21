using System.Diagnostics;

namespace Flow.Launcher.Helper;

/// <summary>
/// Manages scroll offset state, precision input accumulation, discrete smoothing animation,
/// and nested scroll-chaining hysteresis for a single scroll axis.
/// </summary>
internal sealed class SmoothScrollAxis
{
    internal const double WheelDelta = 120.0;
    internal const double LineHeight = 16.0;
    internal const int DefaultScrollLines = 3;
    internal const double SmoothingSpeed = 18.0;
    internal const double SnapThreshold = 0.25;
    internal const double ExternalScrollTolerance = 0.5;
    internal const double MaxFrameDeltaSeconds = 0.05;
    internal static readonly TimeSpan GestureIdleResetWindow = TimeSpan.FromMilliseconds(200);

    private bool _initialized;
    private double _currentOffset;
    private double _targetOffset;
    private double _lastAppliedOffset;
    private bool _hasPendingApply;
    private bool _isAnimating;
    private double _fractionalItemRemainder;
    private bool _movedInCurrentGesture;
    private long _lastWheelTimestamp;

    public bool IsAnimating => _isAnimating;

    public double CurrentOffset => _currentOffset;

    public double TargetOffset => _targetOffset;

    /// <summary>
    /// Converts a raw Win32 wheel delta into a scroll offset delta in DIPs (or logical items).
    /// </summary>
    public static double ComputeOffsetDelta(
        int wheelDelta,
        double viewportSize,
        int wheelScrollLines,
        bool isLogicalItemScrolling,
        bool invertSign)
    {
        if (wheelDelta == 0 || wheelScrollLines == 0)
            return 0.0;

        double stepPerDetent;
        if (wheelScrollLines < 0)
        {
            stepPerDetent = viewportSize > 0
                ? viewportSize
                : (isLogicalItemScrolling ? DefaultScrollLines : LineHeight * DefaultScrollLines);
        }
        else
        {
            stepPerDetent = isLogicalItemScrolling
                ? wheelScrollLines
                : wheelScrollLines * LineHeight;
        }

        double signedDelta = invertSign ? -(double)wheelDelta : wheelDelta;
        return (signedDelta / WheelDelta) * stepPerDetent;
    }

    /// <summary>
    /// Applies a scroll offset delta from either a precision touchpad or a discrete mouse wheel.
    /// </summary>
    public void ApplyWheelDelta(
        double offsetDelta,
        double actualOffset,
        double scrollableExtent,
        bool isPrecisionInput,
        bool isLogicalItemScrolling,
        long nowTicks,
        out double? offsetToApply,
        out bool shouldBubbleToParent)
    {
        offsetToApply = null;
        shouldBubbleToParent = false;

        if (scrollableExtent <= 0)
        {
            _currentOffset = 0;
            _targetOffset = 0;
            _lastAppliedOffset = 0;
            _hasPendingApply = false;
            _isAnimating = false;
            _fractionalItemRemainder = 0;
            _initialized = true;
            _movedInCurrentGesture = false;
            shouldBubbleToParent = true;
            return;
        }

        if (offsetDelta == 0)
            return;

        if (_lastWheelTimestamp != 0
            && Stopwatch.GetElapsedTime(_lastWheelTimestamp, nowTicks) >= GestureIdleResetWindow)
        {
            _movedInCurrentGesture = false;
        }

        if (!_initialized || (!_isAnimating && !_hasPendingApply))
        {
            double synced = Math.Clamp(actualOffset, 0, scrollableExtent);
            if (_initialized && Math.Abs(synced - _lastAppliedOffset) > ExternalScrollTolerance)
                _fractionalItemRemainder = 0;

            _currentOffset = synced;
            _targetOffset = synced;
            _lastAppliedOffset = synced;
            _initialized = true;
        }

        _currentOffset = Math.Clamp(_currentOffset, 0, scrollableExtent);
        _targetOffset = Math.Clamp(_targetOffset, 0, scrollableExtent);

        bool atStart = _targetOffset <= 0 && _currentOffset <= SnapThreshold;
        bool atEnd = _targetOffset >= scrollableExtent && _currentOffset >= scrollableExtent - SnapThreshold;
        if ((atStart && offsetDelta < 0) || (atEnd && offsetDelta > 0))
        {
            _fractionalItemRemainder = 0;
            if (_movedInCurrentGesture)
            {
                _lastWheelTimestamp = nowTicks;
                shouldBubbleToParent = false;
            }
            else
            {
                shouldBubbleToParent = true;
            }

            return;
        }

        _movedInCurrentGesture = true;
        _lastWheelTimestamp = nowTicks;

        if (isLogicalItemScrolling)
        {
            _isAnimating = false;

            if (_fractionalItemRemainder != 0 && Math.Sign(offsetDelta) != Math.Sign(_fractionalItemRemainder))
                _fractionalItemRemainder = 0;

            double total = _fractionalItemRemainder + offsetDelta;
            double wholeSteps = Math.Truncate(total);
            _fractionalItemRemainder = total - wholeSteps;

            if (wholeSteps != 0)
            {
                double nextOffset = Math.Clamp(_targetOffset + wholeSteps, 0, scrollableExtent);
                if (nextOffset <= 0 || nextOffset >= scrollableExtent)
                    _fractionalItemRemainder = 0;

                _currentOffset = nextOffset;
                _targetOffset = nextOffset;
                _lastAppliedOffset = nextOffset;
                _hasPendingApply = Math.Abs(nextOffset - actualOffset) > 1e-6 || _hasPendingApply;
                offsetToApply = nextOffset;
            }

            return;
        }

        if (isPrecisionInput)
        {
            if (_isAnimating)
            {
                _targetOffset = _currentOffset;
                _isAnimating = false;
            }

            double nextOffset = Math.Clamp(_targetOffset + offsetDelta, 0, scrollableExtent);
            _targetOffset = nextOffset;
            _currentOffset = nextOffset;
            _lastAppliedOffset = nextOffset;
            _hasPendingApply = Math.Abs(nextOffset - actualOffset) > 1e-6 || _hasPendingApply;
            offsetToApply = nextOffset;
            return;
        }

        if (_isAnimating && ((_targetOffset - _currentOffset) * offsetDelta) < 0)
            _targetOffset = _currentOffset;

        _targetOffset = Math.Clamp(_targetOffset + offsetDelta, 0, scrollableExtent);

        if (Math.Abs(_targetOffset - _currentOffset) <= SnapThreshold)
        {
            _currentOffset = _targetOffset;
            _isAnimating = false;
            _lastAppliedOffset = _currentOffset;
            _hasPendingApply = Math.Abs(_currentOffset - actualOffset) > 1e-6 || _hasPendingApply;
            offsetToApply = _currentOffset;
        }
        else
        {
            _isAnimating = true;
        }
    }

    /// <summary>
    /// Advances discrete mouse-wheel interpolation by <paramref name="dtSeconds"/>.
    /// Returns true if animation remains active.
    /// </summary>
    public bool StepAnimation(double dtSeconds, double scrollableExtent, out double? offsetToApply)
    {
        offsetToApply = null;
        if (!_isAnimating)
            return false;

        if (scrollableExtent <= 0)
        {
            _currentOffset = 0;
            _targetOffset = 0;
            _lastAppliedOffset = 0;
            _isAnimating = false;
            _hasPendingApply = false;
            return false;
        }

        _targetOffset = Math.Clamp(_targetOffset, 0, scrollableExtent);
        _currentOffset = Math.Clamp(_currentOffset, 0, scrollableExtent);

        double clampedDt = Math.Clamp(dtSeconds, 0.0, MaxFrameDeltaSeconds);
        double remaining = _targetOffset - _currentOffset;
        if (Math.Abs(remaining) <= SnapThreshold)
        {
            _currentOffset = _targetOffset;
            _isAnimating = false;
        }
        else
        {
            double factor = 1.0 - Math.Exp(-SmoothingSpeed * clampedDt);
            _currentOffset += remaining * factor;

            if (Math.Abs(_targetOffset - _currentOffset) <= SnapThreshold)
            {
                _currentOffset = _targetOffset;
                _isAnimating = false;
            }
        }

        _lastAppliedOffset = _currentOffset;
        _hasPendingApply = true;
        offsetToApply = _currentOffset;
        return _isAnimating;
    }

    /// <summary>
    /// Synchronizes axis state when WPF reports a scroll or extent change.
    /// </summary>
    public void OnScrollChanged(double actualOffset, double offsetChange, double scrollableExtent)
    {
        double maxExtent = Math.Max(0, scrollableExtent);
        double clampedActual = Math.Clamp(actualOffset, 0, maxExtent);

        if (!_initialized)
        {
            _currentOffset = clampedActual;
            _targetOffset = clampedActual;
            _lastAppliedOffset = clampedActual;
            _initialized = true;
            return;
        }

        if (offsetChange != 0)
        {
            if (_hasPendingApply && Math.Abs(actualOffset - _lastAppliedOffset) <= ExternalScrollTolerance)
            {
                _hasPendingApply = false;
                _targetOffset = Math.Clamp(_targetOffset, 0, maxExtent);
                _currentOffset = Math.Clamp(_currentOffset, 0, maxExtent);
            }
            else
            {
                _isAnimating = false;
                _hasPendingApply = false;
                _fractionalItemRemainder = 0;
                _currentOffset = clampedActual;
                _targetOffset = clampedActual;
                _lastAppliedOffset = clampedActual;
            }
        }
        else
        {
            _targetOffset = Math.Clamp(_targetOffset, 0, maxExtent);
            _currentOffset = Math.Clamp(_currentOffset, 0, maxExtent);

            if (_hasPendingApply && Math.Abs(actualOffset - _lastAppliedOffset) <= ExternalScrollTolerance)
                _hasPendingApply = false;

            if (_isAnimating && Math.Abs(_targetOffset - _currentOffset) <= SnapThreshold)
            {
                _isAnimating = false;
                _currentOffset = _targetOffset;
            }
        }
    }

    /// <summary>
    /// Cancels any active animation and resets pending state to the current actual offset.
    /// </summary>
    public void CancelAnimation(double actualOffset, double scrollableExtent)
    {
        double clamped = Math.Clamp(actualOffset, 0, Math.Max(0, scrollableExtent));
        _isAnimating = false;
        _hasPendingApply = false;
        _fractionalItemRemainder = 0;
        _movedInCurrentGesture = false;
        _currentOffset = clamped;
        _targetOffset = clamped;
        _lastAppliedOffset = clamped;
    }
}
