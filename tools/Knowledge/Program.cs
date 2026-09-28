// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Tools.Knowledge;

/// <summary>
/// One executable, three commands. The default is the query CLI over <c>docs/knowledge/</c>; a first
/// argument of <c>verify-structure</c> runs the blocking structure gate and <c>drift</c> the hand-run
/// staleness report. They replace <c>scripts/knowledge.rb</c>, <c>verify_knowledge_structure.rb</c> and
/// <c>knowledge_drift.rb</c> from the Ruby port, and share one parser so the three can never disagree
/// about what an entry is.
/// </summary>
internal static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    public static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Count > 0 && args[0] is "verify-structure" or "drift")
        {
            return RunCompanion(args[0], [.. args.Skip(1)], stdout, stderr);
        }

        return new Cli(stdout, stderr).Run(args);
    }

    // The companions take only --root and --help. Exit 2 when the command itself cannot run (a bad flag, a
    // missing manifest or appendix C, a parse hole), exactly as the Ruby scripts did.
    private static int RunCompanion(string command, List<string> args, TextWriter stdout, TextWriter stderr)
    {
        var help = command == "drift" ? DriftReport.Help : StructureGate.Help;
        try
        {
            string? root = null;
            for (var i = 0; i < args.Count; i++)
            {
                switch (args[i])
                {
                    case "-h" or "--help":
                        stdout.Write(help);
                        return 0;
                    case "--root" when i + 1 < args.Count:
                        root = args[++i];
                        break;
                    case var arg when arg.StartsWith("--root=", StringComparison.Ordinal):
                        root = arg["--root=".Length..];
                        break;
                    case "--root":
                        throw new UsageException("missing argument: --root");
                    default:
                        throw new UsageException($"invalid option: {args[i]}");
                }
            }

            var paths = root is not null ? new KnowledgePaths(root) : KnowledgePaths.Default();
            return command == "drift"
                ? new DriftReport(paths, stdout).Run()
                : new StructureGate(paths, stdout, stderr).Run();
        }
        catch (Exception e) when (e is UsageException or NotHarvestedException)
        {
            stderr.Write(e.Message + "\n");
            return 2;
        }
    }
}
