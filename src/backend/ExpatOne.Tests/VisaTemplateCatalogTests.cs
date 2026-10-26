using System.Text.Json;
using ExpatOne.Application.DTOs;
using ExpatOne.Infrastructure.Services;

namespace ExpatOne.Tests;

public class VisaTemplateCatalogTests
{
    public static IEnumerable<object[]> PreparationPacks() => VisaRules.AllSeedJson()
        .Select(json => JsonSerializer.Deserialize<VisaRulePack>(json, VisaRules.Json)!)
        .Where(pack => pack.ValidationProfile == "preparation-v1")
        .Select(pack => new object[] { pack.Id });

    private static VisaRulePack Find(string id) => VisaRules.AllSeedJson()
        .Select(json => JsonSerializer.Deserialize<VisaRulePack>(json, VisaRules.Json)!)
        .Single(pack => pack.Id == id);

    [Fact]
    public void CatalogContainsTwelveUniquePriorityPacksAndOriginalSingaporePack()
    {
        var packs = VisaRules.AllSeedJson().Select(json => JsonSerializer.Deserialize<VisaRulePack>(json, VisaRules.Json)!).ToList();
        Assert.Equal(13, packs.Count);
        Assert.Equal(packs.Count, packs.Select(p => p.Id).Distinct().Count());
        Assert.Single(packs.Where(p => p.IsSingaporeVisit));
        foreach (var country in new[] { "MY", "VU", "IN", "CN" })
            foreach (var purpose in new[] { "Tourism", "Employment", "Dependent" })
                Assert.Single(packs.Where(p => p.Country == country && p.Purpose == purpose));
    }

    [Theory]
    [MemberData(nameof(PreparationPacks))]
    public void PreparationChecklistDoesNotApplySingaporeRules(string id)
    {
        var pack = Find(id);
        var today = new DateOnly(2026, 10, 26);
        var state = CompleteState(pack, today);
        var assessment = VisaRules.Assess(pack, state, today);
        Assert.Empty(assessment.Blockers);
        Assert.Equal("ChecklistComplete", assessment.Status);
        Assert.DoesNotContain("SG Arrival Card", assessment.TravelNote);
        // A completed preparation checklist still needs an explicit route review.
        state.Values["routeConfirmed"] = "Not confirmed";
        Assert.NotEmpty(VisaRules.Assess(pack, state, today).Blockers);
    }

    [Theory]
    [MemberData(nameof(PreparationPacks))]
    public void StaleGuidanceAndMissingEvidenceBlockCompletion(string id)
    {
        var pack = Find(id);
        var today = new DateOnly(2026, 10, 26);
        var state = CompleteState(pack, today);
        Assert.Contains(VisaRules.Assess(pack, state, new DateOnly(2027, 1, 25)).Blockers,
            b => b.Contains("republished"));
        state.Documents.RemoveAll(d => d.Role == "Passport");
        Assert.Contains(VisaRules.Assess(pack, state, today).Blockers, b => b.Contains("passport", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConditionalEvidenceTracksAnswersAndUnknownConditionsStayInactive()
    {
        var values = new Dictionary<string, string> { ["medicalRequired"] = "No" };
        Assert.False(VisaRules.Active("eq:medicalRequired:Yes", values, new DateOnly(2026, 10, 26)));
        values["medicalRequired"] = "Yes";
        Assert.True(VisaRules.Active("eq:medicalRequired:Yes", values, new DateOnly(2026, 10, 26)));
        Assert.False(VisaRules.Active("eq:medicalRequired", values, new DateOnly(2026, 10, 26)));
        Assert.False(VisaRules.Active("unknown", values, new DateOnly(2026, 10, 26)));
    }

    private static VisaState CompleteState(VisaRulePack pack, DateOnly today)
    {
        var state = new VisaState();
        foreach (var f in pack.Fields)
        {
            state.Values[f.Key] = f.Type switch {
                "date" => f.Key switch {
                    "birthDate" => "1985-01-01", "passportIssueDate" => "2020-01-01",
                    "passportExpiryDate" or "sponsorPassExpiry" => "2035-01-01",
                    "departureDate" => "2026-12-31", "employmentEndDate" => "2027-12-01",
                    _ => "2026-12-01"
                },
                "email" => "applicant@example.test", "number" => "10000",
                "choice" => f.Options!.Contains("No") ? "No" : f.Options.First(),
                _ => "Synthetic test value"
            };
        }
        state.Values["nationality"] = "Malaysia";
        state.Values["routeConfirmed"] = "Yes";
        foreach (var check in pack.Checks.Where(c => VisaRules.Active(c.When, state.Values, today)))
        {
            Guid? document = null;
            if (check.NeedsDocument)
            {
                var bound = state.Documents.SingleOrDefault(d => d.Role == check.Role);
                if (bound == null) { bound = new() { Id = Guid.NewGuid(), Role = check.Role!, Fingerprint = "test" }; state.Documents.Add(bound); }
                document = bound.Id;
            }
            state.Checks[check.Key] = new VisaCheckInput { Confirmed = true, Note = "Synthetic route review for test only", DocumentId = document };
        }
        return state;
    }
}
