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
        // Google Places (and most real-world address data) names the city "New York",
        // never "New York City" — the locality's actual name doesn't include "City".
        ["new york"] = Canonical.NewYorkCity,
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

        // Real-world formatted addresses (Google Places' formattedAddress in particular) commonly
        // append a 2-letter state code, "usa", or both after the city name — e.g. "New York, NY, USA"
        // normalizes to "new york ny usa". Strip one trailing qualifier at a time and retry the alias
        // lookup after each strip, since a single address can carry more than one (state AND country).
        // Bounded to 2 strips: at most a state code and a country qualifier.
        var candidate = normalized;
        for (var i = 0; i < 2; i++)
        {
            var lastSpaceIndex = candidate.LastIndexOf(' ');
            if (lastSpaceIndex <= 0)
            {
                break;
            }

            var suffix = candidate[(lastSpaceIndex + 1)..];
            var isQualifier = suffix.Length == 2 || suffix == "usa";
            if (!isQualifier)
            {
                break;
            }

            candidate = candidate[..lastSpaceIndex];
            if (DirectAliases.TryGetValue(candidate, out var canonicalFromRemainder))
            {
                return canonicalFromRemainder;
            }
        }

        return normalized;
    }
}
