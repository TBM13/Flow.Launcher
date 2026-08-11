using Flow.Launcher.Core.Settings;
using Flow.Launcher.PluginSDK.API;

namespace Flow.Launcher.Core.Text;

public class StringMatcher(ISettingsAPI settings) : IStringMatcher
{
    private readonly ISettingsAPI _settings = settings;

    public MatchResult FuzzySearch(ReadOnlySpan<char> query, ReadOnlySpan<char> candidate)
    {
        query = query.Trim();
        candidate = candidate.Trim();

        // Tokenize strings
        Span<Range> qTokens = stackalloc Range[TokenizedString.MaxTokens];
        TokenizedString q = TokenizedString.Tokenize(query, qTokens);
        Span<Range> cTokens = stackalloc Range[TokenizedString.MaxTokens];
        TokenizedString c = TokenizedString.Tokenize(candidate, cTokens);

        return FuzzySearch(q, c);
    }

    public MatchResult FuzzySearchBest(ReadOnlySpan<char> query, ReadOnlySpan<char> c1, ReadOnlySpan<char> c2)
    {
        query = query.Trim();
        c1 = c1.Trim();
        c2 = c2.Trim();

        // Tokenize query
        Span<Range> qTokens = stackalloc Range[TokenizedString.MaxTokens];
        TokenizedString q = TokenizedString.Tokenize(query, qTokens);

        // Tokenize candidates & perform fuzzy searches
        Span<Range> cTokens = stackalloc Range[TokenizedString.MaxTokens];
        TokenizedString c = TokenizedString.Tokenize(c1, cTokens);
        MatchResult res1 = FuzzySearch(q, c);
        c = TokenizedString.Tokenize(c2, cTokens);
        MatchResult res2 = FuzzySearch(q, c);

        return res1.Score >= res2.Score ? res1 : res2;
    }

    public MatchResult FuzzySearchBest(
        ReadOnlySpan<char> query, ReadOnlySpan<char> c1, ReadOnlySpan<char> c2, ReadOnlySpan<char> c3)
    {
        query = query.Trim();
        c1 = c1.Trim();
        c2 = c2.Trim();
        c3 = c3.Trim();

        // Tokenize query
        Span<Range> qTokens = stackalloc Range[TokenizedString.MaxTokens];
        TokenizedString q = TokenizedString.Tokenize(query, qTokens);

        // Tokenize candidates & perform fuzzy searches
        Span<Range> cTokens = stackalloc Range[TokenizedString.MaxTokens];
        TokenizedString c = TokenizedString.Tokenize(c1, cTokens);
        MatchResult res1 = FuzzySearch(q, c);
        c = TokenizedString.Tokenize(c2, cTokens);
        MatchResult res2 = FuzzySearch(q, c);
        c = TokenizedString.Tokenize(c3, cTokens);
        MatchResult res3 = FuzzySearch(q, c);

        MatchResult best = res1.Score >= res2.Score ? res1 : res2;
        return best.Score >= res3.Score ? best : res3;
    }

    public MatchResult FuzzySearchBest(ReadOnlySpan<char> query, params string[] candidates)
    {
        query = query.Trim();

        // Tokenize query
        Span<Range> qTokens = stackalloc Range[TokenizedString.MaxTokens];
        TokenizedString q = TokenizedString.Tokenize(query, qTokens);

        // Tokenize candidates & perform fuzzy searches
        MatchResult best = default;
        Span<Range> cTokens = stackalloc Range[TokenizedString.MaxTokens];
        foreach (string candidate in candidates)
        {
            TokenizedString c = TokenizedString.Tokenize(candidate.AsSpan().Trim(), cTokens);
            MatchResult res = FuzzySearch(q, c);
            if (res.Score > best.Score)
                best = res;
        }
        return best;
    }

    // Current method has two parts, Acronym Match and Fuzzy Search:
    // 
    // Acronym Match:
    // Charater listed below will be considered as acronym
    // 1. Character on index 0
    // 2. Character appears after a space
    // 3. Character that is UpperCase
    // 4. Character that is number
    // 
    // Acronym Match will succeed when all query characters match with acronyms in stringToCompare.
    // If any of the characters in the query isn't matched with stringToCompare, Acronym Match will fail.
    // Score will be calculated based the percentage of all query characters matched with total acronyms in stringToCompare.
    // 
    // Fuzzy Search:
    // Character matching + substring matching;
    // 1. Query search string is split into substrings, separator is whitespace.
    // 2. Check each query substring's characters against full compare string,
    // 3. if a character in the substring is matched, loop back to verify the previous character.
    // 4. If previous character also matches, and is the start of the substring, update list.
    // 5. Once the previous character is verified, move on to the next character in the query substring.
    // 6. Move onto the next substring's characters until all substrings are checked.
    // 7. Consider success and move onto scoring if every char or substring without whitespaces matched
    private MatchResult FuzzySearch(TokenizedString query, TokenizedString candidate)
    {
        if (query.TokenCount == 0 || candidate.TokenCount == 0)
            return default;

        if (query.TokenCount == TokenizedString.MaxTokens
            || candidate.TokenCount == TokenizedString.MaxTokens)
        {
            // TODO: Log error
            return default;
        }

        // =======================================================
        // STRATEGY 1: Acronym Match
        // =======================================================
        int acronymScore = (int)Math.Round(
            AcronymMatch(query, candidate), MidpointRounding.AwayFromZero);
        if (acronymScore >= (int)_settings.QuerySearchPrecision)
        {
            // If acronym match meets search threshold score, return early
            return new MatchResult
            {
                Score = acronymScore,
                IsThresholdMet = true
            };
        }

        // =======================================================
        // STRATEGY 2: Fuzzy Search
        // =======================================================
        int fuzzySearchScore = (int)Math.Round(
            ActualFuzzySearch(query, candidate), MidpointRounding.AwayFromZero);

        bool thresholdMet = fuzzySearchScore >= (int)_settings.QuerySearchPrecision;
        return new MatchResult
        {
            Score = thresholdMet ? fuzzySearchScore : 0,
            IsThresholdMet = thresholdMet
        };
    }

    /// <summary>
    /// Performs an acronym match between the query against the candidate.
    /// <para/>
    /// An acronym is:
    /// <list type="bullet">
    /// <item>The first character of a token.</item>
    /// <item>An uppercase letter.</item>
    /// <item>A digit.</item>
    /// </list>
    /// </summary>
    /// <returns>
    /// The match score (from 0 to 100) if all characters on query sequentially match
    /// with an acronym in candidate. Otherwise, returns 0.
    /// </returns>
    private static double AcronymMatch(in TokenizedString query, in TokenizedString candidate)
    {
        // If query is larger than candidate, it does not make sense to perform an acronym match
        // E.g. query "Visual Studio 2019" should not match "VS 2019"
        if (query.TokenCount == 0 || candidate.TokenCount == 0 || query.CharCount > candidate.CharCount)
            return 0;

        TokenizedStringCharEnumerator queryEnumerator = query.GetCharEnumerator();
        bool matchingQuery = queryEnumerator.MoveNext();
        TokenizedStringCharEnumerator candidateEnumerator = candidate.GetCharEnumerator();
        int candidateAcronymsCount = 0;
        while (candidateEnumerator.MoveNext())
        {
            if (char.IsWhiteSpace(candidateEnumerator.Current))
                continue;
            if (!candidateEnumerator.IsCurrentCharAcronym())
                continue;

            candidateAcronymsCount++;
            if (matchingQuery)
            {
                if (char.IsWhiteSpace(queryEnumerator.Current))
                    queryEnumerator.MoveNext();

                if (char.ToLowerInvariant(queryEnumerator.Current)
                    == char.ToLowerInvariant(candidateEnumerator.Current))
                    matchingQuery = queryEnumerator.MoveNext();
            }
        }

        if (matchingQuery)
            // Failed to match a character in query
            return 0;

        // Success: The whole query is made of matched acronyms
        // E.g: query "vs 2019" should match "Visual Studio 2019"
        double acronymScore = (query.CharCountWithoutSpaces * 100.0) / candidateAcronymsCount;
        return acronymScore;
    }

    // TODO: Improve this mess after adding tests
    private static double ActualFuzzySearch(in TokenizedString query, in TokenizedString candidate)
    {
        // If query is larger than candidate, it does not make sense to perform an acronym match
        // E.g. query "Visual Studio 2019" should not match "VS 2019"
        if (query.TokenCount == 0 || candidate.TokenCount == 0 || query.CharCount > candidate.CharCount)
            return 0;

        int currentQTokenIndex = 0;
        ReadOnlySpan<char> currentQToken = query[currentQTokenIndex];
        int currentQTokenChar = 0;

        int firstMatchIndex = -1;
        int lastMatchIndex = 0;
        bool matchFoundInPreviousLoop = false;
        bool allQueryTokensMatched = true;

        Span<int> spaceIndices = stackalloc int[candidate.TokenCount];
        int spaceIndicesIndex = 0;

        TokenizedStringCharEnumerator candidateEnumerator = candidate.GetCharEnumerator();
        int t = -1;
        while (candidateEnumerator.MoveNext())
        {
            t++;

            char ct = char.ToLowerInvariant(candidateEnumerator.Current);
            // To maintain a list of indices which correspond to spaces in the string to compare
            // To populate the list only for the first query substring
            if (ct == ' ' && currentQTokenIndex == 0)
                spaceIndices[spaceIndicesIndex++] = t;

            if (ct != char.ToLowerInvariant(currentQToken[currentQTokenChar]))
            {
                matchFoundInPreviousLoop = false;
                continue;
            }

            // first matched char will become the start of the compared string
            if (firstMatchIndex < 0)
                firstMatchIndex = t;

            if (currentQTokenChar == 0)
                // first letter of current word
                matchFoundInPreviousLoop = true;
            else if (!matchFoundInPreviousLoop)
            {
                // we want to verify that there is not a better match if this is not a full word
                // in order to do so we need to verify all previous chars are part of the pattern
                int startIndexToVerify = t - currentQTokenChar;

                if (AllPreviousCharsMatched(startIndexToVerify, currentQTokenChar, candidate, currentQToken))
                {
                    matchFoundInPreviousLoop = true;

                    // if it's the beginning character of the first query token that is matched then we need to update start index
                    if (currentQTokenIndex == 0)
                        firstMatchIndex = startIndexToVerify;
                }
            }

            lastMatchIndex = t + 1;
            currentQTokenChar++;

            // Advance to next query token
            if (currentQTokenChar == currentQToken.Length)
            {
                // if any of the tokens was not matched then consider as all are not matched
                allQueryTokensMatched &= matchFoundInPreviousLoop;

                currentQTokenIndex++;
                if (currentQTokenIndex == query.TokenCount)
                    break;

                currentQToken = query[currentQTokenIndex];
                currentQTokenChar = 0;
            }
        }

        // Check if all query tokens had at least one character matched in candidate
        if (currentQTokenIndex == query.TokenCount)
        {
            // closest space index to the left of the first matched char
            int nearestSpaceIndex = CalculateClosestSpaceIndex(
                spaceIndices, spaceIndicesIndex, firstMatchIndex);

            // firstMatchIndex - nearestSpaceIndex - 1 is the index of the first matched char
            // preceded by a space e.g. 'world' matching 'hello world' firstIndex would be 0 not 6 
            // giving more weight than 'we or donald' by allowing the distance calculation to treat the starting position at before the space.
            int score = CalculateSearchScore(query, candidate,
                firstMatchIndex - nearestSpaceIndex - 1, candidate.TokenCount,
                lastMatchIndex - firstMatchIndex, allQueryTokensMatched);

            return score;
        }

        return 0;
    }

    private static bool AllPreviousCharsMatched(
        int startIndexToVerify, int currentQueryTokenCharIndex,
        in TokenizedString candidate, ReadOnlySpan<char> currentQueryToken)
    {
        TokenizedStringCharEnumerator candidateEnum = candidate.GetCharEnumerator(startIndexToVerify);
        for (int i = 0; i < currentQueryTokenCharIndex; i++)
        {
            candidateEnum.MoveNext();

            char c = char.ToLowerInvariant(candidateEnum.Current);
            if (c != char.ToLowerInvariant(currentQueryToken[i]))
                return false;
        }

        return true;
    }

    private static int CalculateClosestSpaceIndex(
        Span<int> spaceIndices, int spaceIndicesLength, int firstMatchIndex)
    {
        int closestSpaceIndex = -1;

        // spaceIndices should be ordered asc
        foreach (int index in spaceIndices[..spaceIndicesLength])
        {
            if (index < firstMatchIndex)
                closestSpaceIndex = index;
            else
                break;
        }

        return closestSpaceIndex;
    }

    private static int CalculateSearchScore(
        in TokenizedString query, in TokenizedString candidate,
        int firstIndex, int candidateTokensCount, int matchLen,
        bool allQueryTokensMatched)
    {
        // A match found near the beginning of a string is scored more than a match found near the end
        // A match is scored more if the characters in the patterns are closer to each other, 
        // while the score is lower if they are more spread out
        var score = 100 * (query.CharCount + 1) / ((1 + firstIndex) + (matchLen + 1));

        // Give more weight to a match that is closer to the start of the string. 
        // if the first matched char is immediately after space and all strings are contained in the compare string e.g. 'world' matching 'hello world'
        // and 'world hello', because both have 'world' immediately preceded by space, their firstIndex will be 0 when distance is calculated,
        // to prevent them scoring the same, we adjust the score by deducting the number of spaces it has from the start of the string, so 'world hello'
        // will score slightly higher than 'hello world' because 'hello world' has one additional space.
        if (firstIndex == 0 && allQueryTokensMatched)
            score -= candidateTokensCount;

        // A match with less characters assigning more weights
        if (candidate.CharCount - query.CharCount < 5)
        {
            score += 20;
        }
        else if (candidate.CharCount - query.CharCount < 10)
        {
            score += 10;
        }

        if (allQueryTokensMatched)
        {
            int count = query.CharCountWithoutSpaces;
            //10 per char is too much for long query strings, this threshhold is to avoid where long strings will override the other results too much
            int threshold = 4;
            if (count <= threshold)
            {
                score += count * 10;
            }
            else
            {
                score += threshold * 10 + (count - threshold) * 5;
            }
        }

        return score;
    }
}
