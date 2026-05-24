using System.Text.RegularExpressions;

namespace SmmBot.Core.Extensions;

public static partial class StringExtensions
{
    public static string ToLowerCaseWithUnderscore(this string value)
    {
        return StringExtensions.AnyUpperCasePrecededByNonUnderscoreChar().Replace(value, (MatchEvaluator) (result => "_" + result.ToString().ToLower())).ToLower();
    }
    
    [GeneratedRegex("(?<=[^_])[A-Z]")]
    private static partial Regex AnyUpperCasePrecededByNonUnderscoreChar();
}