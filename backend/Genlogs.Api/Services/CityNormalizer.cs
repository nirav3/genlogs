using System.Text.RegularExpressions;

namespace Genlogs.Api.Services;

/// <summary>
/// Normalizes free-text city input into the canonical keys stored on seeded <see cref="Models.Lane"/> rows,
/// per carrier-lookup spec.md's case/whitespace/trailing-qualifier-insensitive matching requirement.
/// </summary>
public static partial class CityNormalizer
{
    public static class Canonical
    {
        public const string NewYorkCity = "new york city";
        public const string WashingtonDc = "washington dc";
        public const string SanFrancisco = "san francisco";
        public const string LosAngeles = "los angeles";
        public const string DefaultFallback = "*";
    }

    // Maps a fully-normalized (lowercase, comma-stripped, whitespace-collapsed) input directly to its
    // canonical lane key. Kept as an explicit table rather than a generic geocoder — this exercise has a
    // fixed, small set of known lane endpoints.
    private static readonly Dictionary<string, string> DirectAliases = new(StringComparer.Ordinal)
    {
        [Canonical.NewYorkCity] = Canonical.NewYorkCity,
        ["nyc"] = Canonical.NewYorkCity,
        [Canonical.WashingtonDc] = Canonical.WashingtonDc,
        [Canonical.SanFrancisco] = Canonical.SanFrancisco,
        [Canonical.LosAngeles] = Canonical.LosAngeles,
    };

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = input.Trim().ToLowerInvariant().Replace(',', ' ');
        normalized = WhitespaceRegex().Replace(normalized, " ").Trim();

        if (DirectAliases.TryGetValue(normalized, out var canonical))
        {
            return canonical;
        }

        // Strip a trailing 2-letter state/country qualifier (e.g. "new york city ny" -> "new york city")
        // and retry — covers requirements' "trailing state/country qualifier" case without stripping
        // qualifiers that are actually part of the canonical name (e.g. "washington dc" itself, which is
        // matched directly above before this ever runs).
        var lastSpaceIndex = normalized.LastIndexOf(' ');
        if (lastSpaceIndex > 0)
        {
            var suffix = normalized[(lastSpaceIndex + 1)..];
            var remainder = normalized[..lastSpaceIndex];
            if (suffix.Length == 2 && DirectAliases.TryGetValue(remainder, out var canonicalFromRemainder))
            {
                return canonicalFromRemainder;
            }
        }

        return normalized;
    }
}
