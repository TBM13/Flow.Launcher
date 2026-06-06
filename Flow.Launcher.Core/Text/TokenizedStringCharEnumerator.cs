namespace Flow.Launcher.Core.Text;

public ref struct TokenizedStringCharEnumerator(
    ReadOnlySpan<char> source, ReadOnlySpan<Range> tokenRanges, int startIndex = 0)
{
    private readonly ReadOnlySpan<char> _source = source;
    private readonly ReadOnlySpan<Range> _tokenRanges = tokenRanges;
    private int _tokenRangeIndex = 0;
    private int _charIndex = startIndex - 1;

    public char Previous { get; private set; }
    public char Current { get; private set; }

    public readonly TokenizedStringCharEnumerator GetEnumerator() => this;

    public bool MoveNext()
    {
        while (_tokenRangeIndex < _tokenRanges.Length)
        {
            ReadOnlySpan<char> token = _source[_tokenRanges[_tokenRangeIndex]];
            // Yield chars of the current token
            if (++_charIndex < token.Length)
            {
                Previous = Current;
                Current = token[_charIndex];
                return true;
            }

            // Move to next token
            _tokenRangeIndex++;
            _charIndex = -1;

            // Inject space between tokens
            if (_tokenRangeIndex < _tokenRanges.Length)
            {
                Previous = Current;
                Current = ' ';
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true when the current character is:
    /// <list type="bullet">
    /// <item>The first character of a token</item>
    /// <item>An uppercase letter</item>
    /// <item>A digit</item>
    /// </list>
    /// </summary>
    public readonly bool IsCurrentCharAcronym()
    {
        return (_charIndex == 0)
            || char.IsUpper(Current)
            || char.IsDigit(Current);
    }
}
