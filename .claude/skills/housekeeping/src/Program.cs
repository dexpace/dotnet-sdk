// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Housekeeping;

/// <summary>
/// The entry point: <c>probe</c> (stage 1, read-only), <c>apply</c> (stage 2, <c>git mv</c> only) or <c>fences</c>
/// (compiles the documentation's C# fences in a scratch directory outside the repository).
/// </summary>
/// <remarks>
/// <code>
/// dotnet run --project .claude/skills/housekeeping/src -- probe [--only links,citations] [--json] [--warn-only]
/// dotnet run --project .claude/skills/housekeeping/src -- apply [--delivery mvp] [--phase 5a] [--rename a=b] [--write]
/// dotnet run --project .claude/skills/housekeeping/src -- fences [--root PATH] [--libs DIR]
/// </code>
/// </remarks>
internal static class Program
{
    private const string Usage =
        "usage: dotnet run --project .claude/skills/housekeeping/src -- <probe|apply|fences> [options]\n" +
        "  probe   read-only: report documentation drift (always first)\n" +
        "  apply   drain docs/superpowers/ into docs/work/ with git mv (dry run unless --write)\n" +
        "  fences  compile the C# fences of the documentation that name Dexpace.Sdk (build Release first)\n" +
        "Pass --help after a command for its options.";

    /// <summary>Process entry point.</summary>
    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>Dispatches to a stage; 2 on an unknown or missing command.</summary>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count == 0)
        {
            error.WriteLine(Usage);
            return 2;
        }

        var rest = args.Skip(1).ToList();
        switch (args[0])
        {
            case "probe":
                return ProbeCli.Run(rest, output, error);
            case "apply":
                return ApplyCli.Run(rest, output, error);
            case "fences":
                return FencesCli.Run(rest, output, error);
            case "-h" or "--help":
                output.WriteLine(Usage);
                return 0;
            default:
                error.WriteLine($"unknown command: {args[0]}\n{Usage}");
                return 2;
        }
    }
}
