using System.Text.RegularExpressions;

namespace FuturesFilter.Example;

/// <summary>
/// Validates parent symbology format before making API calls
/// </summary>
public static partial class ParentSymbolValidator
{
    // Parent symbol format: [ROOT].FUT, [ROOT].OPT, or [ROOT].SPOT
    [GeneratedRegex(@"^[A-Z0-9]+\.(FUT|OPT|SPOT)$", RegexOptions.Compiled)]
    private static partial Regex ParentSymbolRegex();

    /// <summary>
    /// Validates a parent symbol format
    /// </summary>
    /// <param name="symbol">The symbol to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidParentSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return false;

        return ParentSymbolRegex().IsMatch(symbol);
    }

    /// <summary>
    /// Validates a parent symbol and throws if invalid
    /// </summary>
    /// <param name="symbol">The symbol to validate</param>
    /// <exception cref="ArgumentException">Thrown if symbol format is invalid</exception>
    public static void ValidateParentSymbol(string symbol)
    {
        if (!IsValidParentSymbol(symbol))
        {
            throw new ArgumentException(
                $"Invalid parent symbol format: '{symbol}'. " +
                $"Expected format: '[ROOT].FUT', '[ROOT].OPT', or '[ROOT].SPOT'. " +
                $"Examples: ES.FUT, QQQ.OPT, BTCUSD.SPOT",
                nameof(symbol));
        }
    }

    /// <summary>
    /// Validates multiple parent symbols
    /// </summary>
    public static void ValidateParentSymbols(IEnumerable<string> symbols)
    {
        foreach (var symbol in symbols)
        {
            ValidateParentSymbol(symbol);
        }
    }

    /// <summary>
    /// Gets the root and type from a parent symbol
    /// </summary>
    /// <param name="symbol">Parent symbol (e.g., "ES.FUT")</param>
    /// <returns>Tuple of (root, type) or null if invalid</returns>
    public static (string Root, string Type)? ParseParentSymbol(string symbol)
    {
        if (!IsValidParentSymbol(symbol))
            return null;

        var parts = symbol.Split('.');
        return (parts[0], parts[1]);
    }

    /// <summary>
    /// Suggests corrections for common typos in parent symbols
    /// </summary>
    public static string? SuggestCorrection(string invalidSymbol)
    {
        if (string.IsNullOrWhiteSpace(invalidSymbol))
            return null;

        // Common typos
        var suggestions = new Dictionary<string, string>
        {
            { "FU4T", "FUT" },
            { "FU7", "FUT" },
            { "0PT", "OPT" },
            { "OP7", "OPT" },
            { "SP0T", "SPOT" },
            { "FUTURES", "FUT" },
            { "FUTURE", "FUT" },
            { "OPTION", "OPT" },
            { "OPTIONS", "OPT" },
        };

        var parts = invalidSymbol.Split('.');
        if (parts.Length != 2)
            return null;

        var root = parts[0];
        var type = parts[1].ToUpperInvariant();

        // Try to find a suggestion for the type
        if (suggestions.TryGetValue(type, out var correctedType))
        {
            return $"{root}.{correctedType}";
        }

        // Check if it's close to a valid type
        if (type.Contains("FUT") || type.Contains("FU"))
            return $"{root}.FUT";
        if (type.Contains("OPT") || type.Contains("OP"))
            return $"{root}.OPT";
        if (type.Contains("SPOT") || type.Contains("SP"))
            return $"{root}.SPOT";

        return null;
    }
}
