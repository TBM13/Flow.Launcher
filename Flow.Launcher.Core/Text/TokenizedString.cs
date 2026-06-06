namespace Flow.Launcher.Core.Text;

/// <summary>
/// Represents a string that has been tokenized into multiple parts based on whitespace.
/// </summary>
public readonly ref struct TokenizedString
{
    private readonly ReadOnlySpan<char> _source;
    private readonly ReadOnlySpan<Range> _tokenRanges;

    /// <summary>
    /// The total amount of tokens.
    /// </summary>
    public int TokenCount => _tokenRanges.Length;
    /// <summary>
    /// The total amount of characters the tokenized string has, including spaces between tokens.
    /// </summary>
    public int CharCount { get; }
    /// <summary>
    /// The total amount of characters the tokenized string has, excluding spaces between tokens.
    /// </summary>
    public int CharCountWithoutSpaces { get; }

    private TokenizedString(ReadOnlySpan<char> source, ReadOnlySpan<Range> tokenRanges)
    {
        _source = source;
        _tokenRanges = tokenRanges;

        foreach (Range tokenRange in _tokenRanges)
            CharCount += tokenRange.End.Value - tokenRange.Start.Value;

        CharCountWithoutSpaces = CharCount;

        // Include spaces between tokens
        if (_tokenRanges.Length > 1)
            CharCount += _tokenRanges.Length - 1;
    }

    /// <summary>
    /// Splits the input string into tokens based on whitespace and stores their ranges in the provided span.
    /// </summary>
    public static TokenizedString Tokenize(ReadOnlySpan<char> str, Span<Range> tokenRanges)
    {
        int tokenCount = 0;
        int tokenStart = -1;

        for (int i = 0; i < str.Length; i++)
        {
            if (char.IsWhiteSpace(str[i]))
            {
                if (tokenStart != -1)
                {
                    tokenRanges[tokenCount++] = new Range(tokenStart, i);
                    tokenStart = -1;
                    if (tokenCount >= tokenRanges.Length)
                        break;
                }
            }
            else if (tokenStart == -1)
                tokenStart = i;
        }

        if (tokenStart != -1 && tokenCount < tokenRanges.Length)
            tokenRanges[tokenCount++] = new Range(tokenStart, str.Length);

        return new TokenizedString(str, tokenRanges[..tokenCount]);
    }

    public TokenizedStringEnumerator GetEnumerator() => new(_source, _tokenRanges);
    public TokenizedStringCharEnumerator GetCharEnumerator(int startIndex = 0)
        => new(_source, _tokenRanges, startIndex);

    public override string ToString() => _source.ToString();

    public ReadOnlySpan<char> this[int index] => _source[_tokenRanges[index]];
}
