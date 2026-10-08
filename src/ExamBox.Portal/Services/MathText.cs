using System.Net;
using System.Text.RegularExpressions;

namespace ExamBox.Services;

/// <summary>
/// Turns the plain text teachers type in Excel into readable maths: x^2 becomes x with a raised 2, \frac{1}{2} a stacked
/// fraction, \sqrt{x} a root, and names such as \pi, \times or \le become their symbols. Everything else is shown as typed.
/// </summary>
public static class MathText
{
    private static readonly Dictionary<string, string> Symbols = new()
    {
        ["pi"] = "\u03C0", ["theta"] = "\u03B8", ["alpha"] = "\u03B1", ["beta"] = "\u03B2", ["gamma"] = "\u03B3", ["delta"] = "\u03B4",
        ["lambda"] = "\u03BB", ["mu"] = "\u03BC", ["sigma"] = "\u03C3", ["omega"] = "\u03C9", ["phi"] = "\u03C6", ["Delta"] = "\u0394",
        ["Sigma"] = "\u03A3", ["Omega"] = "\u03A9", ["times"] = "\u00D7", ["div"] = "\u00F7", ["pm"] = "\u00B1", ["mp"] = "\u2213",
        ["le"] = "\u2264", ["leq"] = "\u2264", ["ge"] = "\u2265", ["geq"] = "\u2265", ["neq"] = "\u2260", ["ne"] = "\u2260",
        ["approx"] = "\u2248", ["infty"] = "\u221E", ["degree"] = "\u00B0", ["cdot"] = "\u00B7", ["therefore"] = "\u2234",
        ["angle"] = "\u2220", ["perp"] = "\u22A5", ["parallel"] = "\u2225", ["rightarrow"] = "\u2192", ["to"] = "\u2192",
        ["sum"] = "\u2211", ["int"] = "\u222B", ["in"] = "\u2208", ["cup"] = "\u222A", ["cap"] = "\u2229", ["subset"] = "\u2282",
        ["emptyset"] = "\u2205", ["sqrt"] = "\u221A", ["circ"] = "\u00B0", ["triangle"] = "\u25B3",
    };

    public static string Render(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var s = text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        // fractions: \frac{a}{b}
        s = Regex.Replace(s, @"\\frac\{([^{}]*)\}\{([^{}]*)\}", "<span class=\"frac\"><span>$1</span><span>$2</span></span>");
        // roots: \sqrt{x} or sqrt(x)
        s = Regex.Replace(s, @"\\sqrt\{([^{}]*)\}", "\u221A<span class=\"rad\">$1</span>");
        s = Regex.Replace(s, @"\bsqrt\(([^()]*)\)", "\u221A<span class=\"rad\">$1</span>");
        // named symbols
        s = Regex.Replace(s, @"\\([A-Za-z]+)", m => Symbols.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
        s = s.Replace("&lt;=", "\u2264").Replace("&gt;=", "\u2265").Replace("!=", "\u2260");
        // powers and indices: x^2, x^{n+1}, x^-1, a_1, a_{12}
        s = Regex.Replace(s, @"\^\{([^{}]*)\}", "<sup>$1</sup>");
        s = Regex.Replace(s, @"\^(-?[A-Za-z0-9]+)", "<sup>$1</sup>");
        s = Regex.Replace(s, @"_\{([^{}]*)\}", "<sub>$1</sub>");
        s = Regex.Replace(s, @"(?<=[A-Za-z0-9\)])_([A-Za-z0-9]+)", "<sub>$1</sub>");
        return s;
    }
}
