// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;
using System.Globalization;
using System.Text;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The results of one suite run against one subject, with the per-requirement worst-of view, the appendix B view and a
/// rendering that lists every waiver and every gap (design section C, §9.3: "so the gap stays visible").
/// </summary>
public sealed class ConformanceReport
{
    private readonly IReadOnlyList<ConformanceWaiver> _waivers;

    private ConformanceReport(string subjectName, IReadOnlyList<ConformanceResult> results, IReadOnlyList<ConformanceWaiver> waivers)
    {
        SubjectName = subjectName;
        Results = results;
        _waivers = waivers;
        IsGreen = results.All(result => result.Status is not (ConformanceStatus.Failed or ConformanceStatus.Errored));
        ByRequirement = WorstBy(results, result => result.Assertion.RequirementIds);
        ByAppendixBItem = FrozenDictionary<string, ConformanceStatus>.Empty;
    }

    /// <summary>
    /// What a green report does not prove: the fixture speaks plaintext HTTP/1.1 on the loopback interface, so TLS,
    /// HTTP/2 and connect-timeout behaviour are outside it (P8a-12).
    /// </summary>
    public static string Preamble { get; } =
        "Transport conformance report. A green run does not prove: TLS or certificate handling (the fixture is plaintext "
        + "HTTP/1.1 on the loopback interface); HTTP/2 or HTTP/3; connect-timeout behaviour (a local listener cannot "
        + "portably accept slowly); or any requirement no assertion cites. It proves that every assertion that ran met its "
        + "clause or is waived below, and nothing more.";

    /// <summary>The name of the subject the run exercised.</summary>
    public string SubjectName { get; }

    /// <summary>Every result of the run, in the order the assertions ran.</summary>
    public IReadOnlyList<ConformanceResult> Results { get; }

    /// <summary>Whether the run has no <see cref="ConformanceStatus.Failed"/> and no <see cref="ConformanceStatus.Errored"/> result.</summary>
    public bool IsGreen { get; }

    /// <summary>The worst status among the results whose assertion cites each requirement; a requirement no result cites is absent.</summary>
    public IReadOnlyDictionary<string, ConformanceStatus> ByRequirement { get; }

    /// <summary>
    /// The worst status per appendix B checklist item (<c>B.6.1</c>..<c>B.7.6</c> in phase 8a), over the items that have at
    /// least one cited requirement with a result.
    /// </summary>
    public IReadOnlyDictionary<string, ConformanceStatus> ByAppendixBItem { get; }

    /// <summary>Builds the report of a run.</summary>
    /// <param name="subjectName">The subject's name.</param>
    /// <param name="results">The results; copied.</param>
    /// <returns>The report. Its waiver list is the waivers the results carry.</returns>
    public static ConformanceReport Create(string subjectName, IEnumerable<ConformanceResult> results)
    {
        ArgumentNullException.ThrowIfNull(subjectName);
        ArgumentNullException.ThrowIfNull(results);
        var copy = results.ToArray();
        return new(subjectName, copy, [.. copy.Select(result => result.Waiver).OfType<ConformanceWaiver>().Distinct()]);
    }

    /// <summary>Builds the report of a run that was configured with <paramref name="waivers"/>, listing every one of them.</summary>
    /// <param name="subjectName">The subject's name.</param>
    /// <param name="results">The results; copied.</param>
    /// <param name="waivers">Every waiver the run was configured with, applied or not.</param>
    internal static ConformanceReport Create(string subjectName, IEnumerable<ConformanceResult> results, IEnumerable<ConformanceWaiver> waivers)
    {
        ArgumentNullException.ThrowIfNull(subjectName);
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(waivers);
        return new(subjectName, results.ToArray(), [.. waivers]);
    }

    /// <summary>
    /// Renders the preamble, the verdict, the per-requirement and appendix B tables, every waiver, every assertion that was
    /// not exercised and every failure.
    /// </summary>
    public override string ToString()
    {
        var text = new StringBuilder(Preamble).AppendLine().AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"Subject: {SubjectName}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Verdict: {(IsGreen ? "GREEN" : "RED")} ({Count(ConformanceStatus.Failed)} failed, {Count(ConformanceStatus.Errored)} errored, {Count(ConformanceStatus.Waived)} waived, {Count(ConformanceStatus.NotExercised)} not exercised, {Count(ConformanceStatus.Vacuous)} vacuous, {Count(ConformanceStatus.Passed)} passed)");
        AppendTable(text, "Requirements", ByRequirement, NaturalOrder);
        AppendTable(text, "Appendix B items", ByAppendixBItem, string.CompareOrdinal);
        AppendWaivers(text);
        AppendResults(text, "Not exercised", ConformanceStatus.NotExercised);
        AppendResults(text, "Failures", ConformanceStatus.Failed, ConformanceStatus.Errored);
        return text.ToString();
    }

    private int Count(ConformanceStatus status) => Results.Count(result => result.Status == status);

    private void AppendWaivers(StringBuilder text)
    {
        if (_waivers.Count == 0)
        {
            return;
        }

        text.AppendLine().AppendLine("Waivers");
        foreach (var waiver in _waivers.OrderBy(w => w.RequirementId, Comparer<string>.Create(NaturalOrder)))
        {
            var scope = waiver.Assertion is null ? string.Empty : $" assertion={waiver.Assertion}";
            scope += waiver.Face is null ? string.Empty : $" face={waiver.Face}";
            var owner = waiver.Owner is null ? "permanent" : $"owner {waiver.Owner}";
            text.AppendLine(CultureInfo.InvariantCulture, $"  {waiver.RequirementId} [{owner}]{scope}: {waiver.Reason}");
        }
    }

    private void AppendResults(StringBuilder text, string heading, params ConformanceStatus[] statuses)
    {
        var rows = Results.Where(result => statuses.Contains(result.Status)).ToArray();
        if (rows.Length == 0)
        {
            return;
        }

        text.AppendLine().AppendLine(heading);
        foreach (var row in rows)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"  {row.Assertion.Name} ({row.Face}) {row.Status}: {row.Detail}");
        }
    }

    private static void AppendTable(StringBuilder text, string heading, IReadOnlyDictionary<string, ConformanceStatus> table, Comparison<string> order)
    {
        if (table.Count == 0)
        {
            return;
        }

        text.AppendLine().AppendLine(heading);
        var keys = table.Keys.ToList();
        keys.Sort(order);
        var width = keys.Max(key => key.Length);
        foreach (var key in keys)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"  {key.PadRight(width)} {table[key]}");
        }
    }

    internal static FrozenDictionary<string, ConformanceStatus> WorstBy(
        IEnumerable<ConformanceResult> results,
        Func<ConformanceResult, IEnumerable<string>> keys)
    {
        var worst = new Dictionary<string, ConformanceStatus>(StringComparer.Ordinal);
        foreach (var result in results)
        {
            foreach (var key in keys(result))
            {
                worst[key] = worst.TryGetValue(key, out var current) ? StatusSeverity.Worst([current, result.Status]) : result.Status;
            }
        }

        return worst.ToFrozenDictionary(StringComparer.Ordinal);
    }

    // PREFIX-n ordered by prefix, then by the number: TRANSPORT-2 before TRANSPORT-10. Keys without a numeric tail
    // (B.6.1) fall back to ordinal order.
    private static int NaturalOrder(string left, string right)
    {
        var (leftPrefix, leftNumber) = Split(left);
        var (rightPrefix, rightNumber) = Split(right);
        var byPrefix = string.CompareOrdinal(leftPrefix, rightPrefix);
        return byPrefix != 0 ? byPrefix : leftNumber.CompareTo(rightNumber);
    }

    private static (string Prefix, int Number) Split(string id)
    {
        var dash = id.LastIndexOf('-');
        return dash > 0 && int.TryParse(id.AsSpan(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? (id[..dash], number)
            : (id, 0);
    }
}
