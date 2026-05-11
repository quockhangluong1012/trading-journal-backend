namespace TradingJournal.Modules.Trades.Common;

internal static class LessonTagSerializer
{
    private const int MaxTagLength = 32;

    public static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> normalizedTags = [];

        foreach (string tag in tags)
        {
            string normalizedTag = NormalizeTag(tag);

            if (string.IsNullOrWhiteSpace(normalizedTag) || !seen.Add(normalizedTag))
            {
                continue;
            }

            normalizedTags.Add(normalizedTag);
        }

        return normalizedTags;
    }

    public static string Serialize(IEnumerable<string>? tags)
    {
        List<string> normalizedTags = NormalizeTags(tags);

        return normalizedTags.Count == 0
            ? string.Empty
            : $"|{string.Join("|", normalizedTags)}|";
    }

    public static List<string> Deserialize(string? tagsText)
    {
        if (string.IsNullOrWhiteSpace(tagsText))
        {
            return [];
        }

        return NormalizeTags(tagsText.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static string NormalizeTag(string? rawTag)
    {
        if (string.IsNullOrWhiteSpace(rawTag))
        {
            return string.Empty;
        }

        string compacted = string.Join(' ', rawTag
            .Replace("|", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return compacted.Length <= MaxTagLength
            ? compacted
            : compacted[..MaxTagLength].Trim();
    }

    public static string BuildContainsToken(string tag)
    {
        string normalizedTag = NormalizeTag(tag);

        return string.IsNullOrWhiteSpace(normalizedTag)
            ? string.Empty
            : $"|{normalizedTag}|";
    }
}