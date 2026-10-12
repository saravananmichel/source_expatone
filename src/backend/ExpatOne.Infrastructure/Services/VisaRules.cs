using System.Globalization;
using System.Text.Json;
using ExpatOne.Application.DTOs;
namespace ExpatOne.Infrastructure.Services;

public class VisaField
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Section { get; set; } = "";
    public string Type { get; set; } = "text";
    public string? When { get; set; }
    public bool Required { get; set; }
    public int MaxLength { get; set; } = 100;
    public List<string>? Options { get; set; }
}
public class VisaCheck
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public bool NeedsDocument { get; set; }
    public bool NoteRequired { get; set; }
    public string? Role { get; set; }
    public string? When { get; set; }
}
public class VisaRulePack
{
    public string Id { get; set; } = "";
    public string ReviewUntil { get; set; } = "";
    public string ReadinessNationality { get; set; } = "";
    public string ReadinessPassportType { get; set; } = "";
    public List<VisaField> Fields { get; set; } = [];
    public List<VisaCheck> Checks { get; set; } = [];
    public List<string> DocumentRoles { get; set; } = [];
}
public class VisaState
{
    public Dictionary<string,string> Values { get; set; } = [];
    public Dictionary<string,VisaCheckInput> Checks { get; set; } = [];
    public List<VisaBoundDocument> Documents { get; set; } = [];
    public Dictionary<string,Guid> Sources { get; set; } = [];
}
public class VisaBoundDocument
{
    public Guid Id { get; set; }
    public string Role { get; set; } = "";
    public string Fingerprint { get; set; } = "";
}
public record VisaCheckResult(string Key, string Label, string Status, Guid? DocumentId);
public record VisaAssessment(string Status, List<string> Blockers, List<VisaCheckResult> Checklist, string TravelNote);
public static class VisaRules
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string SeedJson()
    {
        using var stream = typeof(VisaRules).Assembly.GetManifestResourceStream("ExpatOne.SgVisitRules")!;
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
    public static bool Active(string? condition, IDictionary<string,string> v, DateOnly today)
    {
        string V(string k) => v.TryGetValue(k,out var x) ? x : "";
        var child = DateOnly.TryParseExact(V("birthDate"),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var dob) && dob <= today && dob.AddYears(13)>today;
        return condition switch {
            null => true,
            "spouse" => V("maritalStatus") is "Married" or "Separated" or "Customary",
            "spouseSg" => Active("spouse",v,today) && V("spouseStatus").StartsWith("Singapore"),
            "spouseOther" => Active("spouse",v,today) && V("spouseStatus")=="Other",
            "otherPassport" => V("passportType")=="Other",
            "chinese" => V("nationality").Equals("China",StringComparison.OrdinalIgnoreCase) || V("nationality").Equals("Chinese",StringComparison.OrdinalIgnoreCase),
            "longStay" => V("stayBand")=="More than 30 days", "otherStay" => V("accommodationType")=="Other",
            "history" => V("otherResidence")=="Yes", "child" => child, "companion" => child && V("airlineRepresentative")!="Yes",
            "antecedent" => Enumerable.Range(1,4).Any(i=>V("antecedent"+i)=="Yes"),
            "loi" => V("loiRequired")=="Yes", "translation" => V("translationRequired")=="Yes",
            "loiIndividual" => V("loiRequired")=="Yes" && V("loiContactType")=="Individual",
            "loiCompany" => V("loiRequired")=="Yes" && V("loiContactType")=="Company", _ => false
        };
    }
    public static VisaAssessment Assess(VisaRulePack pack, VisaState state, DateOnly today)
    {
        var blockers = new List<string>(); var v=state.Values;
        string V(string k) => v.GetValueOrDefault(k,"");
        DateOnly? D(string k) => DateOnly.TryParseExact(V(k),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)?d:null;
        foreach(var f in pack.Fields.Where(f=>Active(f.When,v,today)))
        {
            var value=V(f.Key);
            if(f.Required && string.IsNullOrWhiteSpace(value)) { blockers.Add("Complete: "+f.Label); continue; }
            if(value.Length==0) continue;
            if(value.Length>f.MaxLength || (f.Options!=null && !f.Options.Contains(value))) blockers.Add("Invalid value: "+f.Label);
            if(f.Type=="date" && D(f.Key)==null) blockers.Add("Use YYYY-MM-DD: "+f.Label);
            if(f.Type=="number" && (!decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out var n) || n<0)) blockers.Add("Invalid amount: "+f.Label);
            if(f.Type=="email" && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(value)) blockers.Add("Invalid email: "+f.Label);
        }
        if(!V("nationality").Equals(pack.ReadinessNationality,StringComparison.OrdinalIgnoreCase) && !V("nationality").Equals("Indian",StringComparison.OrdinalIgnoreCase)) blockers.Add("Readiness rules currently cover Indian ordinary-passport holders only. Other applicants can prepare drafts, but need a reviewed nationality-specific rule pack.");
        if(V("passportType")!=pack.ReadinessPassportType) blockers.Add("This travel-document type requires a separate eligibility review.");
        if(!DateOnly.TryParse(pack.ReviewUntil,out var reviewUntil) || today>reviewUntil) blockers.Add("The official requirement rules need to be reviewed and republished.");
        if(D("arrivalDate") is { } arrival)
        {
            if(arrival<today) blockers.Add("Arrival date is in the past.");
            if(arrival>today.AddDays(30)) blockers.Add("ICA advises applying within 30 days before arrival. Review closer to departure.");
            if(D("passportExpiryDate") is not { } expiry || expiry<arrival.AddMonths(6)) blockers.Add("Passport needs at least six months validity from entry.");
            if(D("departureDate") is { } departure) {
                var nights=departure.DayNumber-arrival.DayNumber;
                if(nights<0) blockers.Add("Departure precedes arrival.");
                if(nights==30) blockers.Add("The form's stay categories omit exactly 30 days; confirm the correct selection with your filing contact.");
                if(nights>30 && V("stayBand")!="More than 30 days" || nights>=0 && nights<30 && V("stayBand")!="Less than 30 days") blockers.Add("Stay category does not match travel dates.");
            }
        }
        if(D("birthDate") is { } birth && birth>today) blockers.Add("Birth date is in the future.");
        if(D("passportIssueDate") is { } issue && (issue>today || D("passportExpiryDate") is { } exp && exp<=issue)) blockers.Add("Passport issue / expiry dates are inconsistent.");
        if(D("photoDate") is not { } photo || photo>today || photo<today.AddMonths(-3)) blockers.Add("Photograph must have been taken within the past three months.");
        if(V("loiRequired")=="Not confirmed" || V("translationRequired")=="Not confirmed") blockers.Add("Resolve the introduction-letter and translation requirements with the filing contact.");
        if(!state.Documents.Any(d=>d.Role=="Passport")) blockers.Add("Link the applicant's passport.");
        var results = new List<VisaCheckResult>();
        foreach(var c in pack.Checks.Where(c=>Active(c.When,v,today))) {
            var input=state.Checks.GetValueOrDefault(c.Key);
            var valid=input?.Confirmed==true && (!c.NoteRequired || !string.IsNullOrWhiteSpace(input.Note)) &&
                (!c.NeedsDocument || state.Documents.Any(d=>d.Id==input.DocumentId && d.Role==c.Role));
            results.Add(new(c.Key,c.Label,valid?"Verified":"Needs review",input?.DocumentId));
            if(!valid) blockers.Add(c.Label);
        }
        var status=blockers.Count==0?"ChecklistComplete":state.Values.Count==0?"Draft":"InProgress";
        return new(status,blockers,results,"Travel checks are separate: onward travel, sufficient funds, next-destination entry permission and any applicable health requirements. Submit SG Arrival Card within three days including arrival day. This checklist is not visa approval or permission to enter.");
    }
}
