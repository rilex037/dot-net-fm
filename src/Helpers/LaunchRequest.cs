namespace DotNetFM;

public enum LaunchRequestKind
{
    None,
    Open,
    Select
}

public sealed record LaunchRequest
{
    public LaunchRequestKind Kind { get; init; }

    public string Path { get; init; } = "";

    public static LaunchRequest Parse(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i].Trim().Trim('"').Trim();
            if (token.Length == 0) continue;

            string lower = token.ToLowerInvariant();

            if (lower.StartsWith("/select,", StringComparison.Ordinal) || lower.StartsWith("-select,", StringComparison.Ordinal) || lower.StartsWith("--select,", StringComparison.Ordinal))
                return FromSelectOperand(token.Substring(token.IndexOf(',') + 1), JoinPathArgs(args, i + 1));

            if (lower.StartsWith("/select=", StringComparison.Ordinal) || lower.StartsWith("-select=", StringComparison.Ordinal) || lower.StartsWith("--select=", StringComparison.Ordinal))
                return FromSelectOperand(token.Substring(token.IndexOf('=') + 1), JoinPathArgs(args, i + 1));

            if (lower is "/select" or "-select" or "--select")
                return FromSelectOperand("", JoinPathArgs(args, i + 1));

            if (token[0] == '/' || token[0] == '-')
                continue;

            string suffix = JoinPathArgs(args, i + 1);
            return new LaunchRequest
            {
                Kind = LaunchRequestKind.Open,
                Path = suffix.Length == 0 ? token : token + " " + suffix
            };
        }

        return new LaunchRequest();
    }

    public string ToWire() => Kind switch
    {
        LaunchRequestKind.Select => "select\t" + Path,
        LaunchRequestKind.Open => "open\t" + Path,
        _ => ""
    };

    public static LaunchRequest FromWire(string? line)
    {
        string raw = (line ?? "").Trim();
        if (raw.Length == 0) return new LaunchRequest();

        if (raw.StartsWith("select\t", StringComparison.Ordinal))
            return FromSelectOperand(raw.Substring(7), "");

        if (raw.StartsWith("open\t", StringComparison.Ordinal))
            return new LaunchRequest { Kind = LaunchRequestKind.Open, Path = raw.Substring(5).Trim() };

        return new LaunchRequest { Kind = LaunchRequestKind.Open, Path = raw };
    }

    private static LaunchRequest FromSelectOperand(string head, string suffix)
    {
        string path = (suffix.Length == 0 ? head : head + " " + suffix).Trim().Trim('"').Trim();
        return path.Length == 0
            ? new LaunchRequest()
            : new LaunchRequest { Kind = LaunchRequestKind.Select, Path = path };
    }

    private static string JoinPathArgs(string[] args, int startIndex)
    {
        var parts = new List<string>();
        for (int i = startIndex; i < args.Length; i++)
        {
            string token = args[i].Trim().Trim('"').Trim();
            if (token.Length == 0) continue;
            if (token[0] == '/' || token[0] == '-') break;
            parts.Add(token);
        }
        return string.Join(" ", parts);
    }
}
