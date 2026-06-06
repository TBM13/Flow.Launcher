namespace Flow.Launcher.Core.Text;

public ref struct TokenizedStringEnumerator(ReadOnlySpan<char> source, ReadOnlySpan<Range> tokenRanges)
{
    private readonly ReadOnlySpan<char> _source = source;
    private readonly ReadOnlySpan<Range> _tokenRanges = tokenRanges;
    private int _tokenRangeIndex = 0;

    public ReadOnlySpan<char> Current { get; private set; }

    public readonly TokenizedStringEnumerator GetEnumerator() => this;

    public bool MoveNext()
    {
        if (_tokenRangeIndex++ < _tokenRanges.Length)
        {
            Current = _source[_tokenRanges[_tokenRangeIndex]];
            return true;
        }

        return false;
    }
}
