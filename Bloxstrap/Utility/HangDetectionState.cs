namespace Claudestrap.Utility
{
    public sealed class HangDetectionState
    {
        private readonly DateTimeOffset _started;
        private readonly TimeSpan _warmup;
        private readonly TimeSpan _threshold;
        private DateTimeOffset? _unresponsiveSince;
        private bool _reported;

        public HangDetectionState(DateTimeOffset started, TimeSpan warmup, TimeSpan threshold)
        {
            _started = started;
            _warmup = warmup;
            _threshold = threshold;
        }

        public bool Observe(DateTimeOffset now, bool responding, bool hasWindow)
        {
            if (_reported) return false;

            if (responding || !hasWindow || now - _started < _warmup)
            {
                _unresponsiveSince = null;
                return false;
            }

            _unresponsiveSince ??= now;
            if (now - _unresponsiveSince.Value < _threshold) return false;

            _reported = true;
            return true;
        }
    }
}
