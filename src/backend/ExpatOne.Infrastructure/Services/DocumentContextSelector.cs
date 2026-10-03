using System.Text.Json;
using System.Text.RegularExpressions;
using ExpatOne.Application.DTOs;
namespace ExpatOne.Infrastructure.Services;

public static class DocumentContextSelector
{
    private static readonly HashSet<string> Stop = new("a an the this that is are was were be do does did what which who when where how can could would should my me i you your in of for to on at and or about document please tell".Split(' '));
    private static HashSet<string> Tokens(string text) => Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+")
        .Select(m => m.Value.Length > 3 && m.Value.EndsWith('s') ? m.Value[..^1] : m.Value)
        .Where(t => !Stop.Contains(t)).ToHashSet();

    public static List<DocumentContextDto> Select(DocumentAnalysisDto analysis, string question)
    {
        var candidates = new List<DocumentContextDto>();
        // Source text, never generated summaries, is the authoritative answer context.
        if (analysis.SemanticDocument is { } semantic && semantic.TryGetProperty("pages", out var pages))
        foreach (var page in pages.EnumerateArray())
        {
            var number = page.GetProperty("page").GetInt32();
            if (page.TryGetProperty("text", out var text))
            {
                var value = text.GetString() ?? "";
                for (var offset = 0; offset < value.Length; offset += 700)
                {
                    var chunk = value.Substring(offset, Math.Min(1000, value.Length-offset));
                    if (!string.IsNullOrWhiteSpace(chunk)) candidates.Add(new($"p{number}-{offset}", number, chunk));
                }
            }
            if (page.TryGetProperty("blocks", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
            foreach (var block in blocks.EnumerateArray())
                if (block.TryGetProperty("text", out var textBlock) && textBlock.GetString() is { Length: > 0 and <= 1000 } blockText)
                    candidates.Add(new($"p{number}-b{candidates.Count}", number, blockText));
        }
        foreach (var e in analysis.Evidence.Where(e => e.Page > 0 && !string.IsNullOrWhiteSpace(e.SourceText)))
            candidates.Add(new(e.Id, e.Page, e.SourceText[..Math.Min(1000,e.SourceText.Length)]));
        candidates = candidates.DistinctBy(c => (c.Page, c.Text)).ToList();
        var query = Tokens(question);
        var indexed = candidates.Select(c => (Context:c, Words:Tokens(c.Text))).ToList();
        var selected = indexed.Select(c => (c.Context, Score:query.Where(c.Words.Contains)
            .Sum(t => Math.Log(1.0 + indexed.Count / (1.0 + indexed.Count(x => x.Words.Contains(t)))))))
            .Where(c => c.Score > 0).OrderByDescending(c => c.Score).ThenBy(c => c.Context.Page).ToList();
        // Broad overview questions get bounded page coverage; unrelated questions fail closed.
        if (selected.Count == 0 && Regex.IsMatch(question, @"summary|summarize|overview|key|explain", RegexOptions.IgnoreCase))
            selected = indexed.GroupBy(c => c.Context.Page).Select(g => (g.First().Context, Score:0.0)).ToList();
        var result = new List<DocumentContextDto>(); var budget = 6000;
        foreach (var candidate in selected)
        {
            if (result.Count == 6) break;
            if (result.Any(c => c.Page == candidate.Context.Page && c.Text.Contains(candidate.Context.Text, StringComparison.Ordinal))) continue;
            if (candidate.Context.Text.Length > budget) continue;
            result.Add(candidate.Context); budget -= candidate.Context.Text.Length;
        }
        return result;
    }
}
