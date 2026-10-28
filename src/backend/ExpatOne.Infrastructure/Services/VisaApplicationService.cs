using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ExpatOne.Application.DTOs;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace ExpatOne.Infrastructure.Services;

public class VisaApplicationService(ExpatOneDbContext db, HttpClient client, IConfiguration config)
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json,VisaRules.Json)!;
    private static string Write<T>(T item) => JsonSerializer.Serialize(item,VisaRules.Json);
    public async Task<object> CatalogAsync() => new {
        countries=await db.VisaDestinations.AsNoTracking().OrderBy(x=>x.Name).ToListAsync(),
        templates=(await db.VisaTemplates.AsNoTracking().ToListAsync()).Select(t=>new {t.Id,t.CountryCode,t.Purpose,rules=Read<JsonElement>(t.RulesJson)}),
        purposes=new[]{"Tourism","Employment","Dependent","Renewal"}
    };
    private async Task<VisaApplication> Find(Guid user,Guid id) => await db.VisaApplications.SingleOrDefaultAsync(a=>a.Id==id && a.UserId==user)
        ?? throw new KeyNotFoundException("Application not found.");
    private async Task<VisaRulePack> Pack(VisaApplication a) => Read<VisaRulePack>((await db.VisaTemplates.SingleAsync(t=>t.Id==a.TemplateId)).RulesJson);
    private static void Revision(VisaApplication a,Guid revision) { if(a.Revision!=revision) throw new InvalidOperationException("Application changed. Reload before saving."); }
    private async Task Save(VisaApplication a,VisaState s) {
        a.StateJson=Write(s); a.Revision=Guid.NewGuid();
        try { await db.SaveChangesAsync(); } catch(DbUpdateConcurrencyException) { throw new InvalidOperationException("Application changed. Reload before saving."); }
    }
    private sealed record Access(Document Document,string Fingerprint,DateTime? Deadline);
    private async Task<Access?> Accessible(Guid user,Guid id)
    {
        var d=await db.Documents.AsNoTracking().SingleOrDefaultAsync(d=>d.Id==id && d.Status!=DocumentStatus.PendingUpload);
        if(d==null) return null;
        DateTime? deadline=null; string grant="owner";
        if(d.UserId!=user) {
            var now=DateTime.UtcNow;
            var g=await db.FamilyDocumentGrants.AsNoTracking().Where(g=>g.DocumentId==id && g.Request.RequesterUserId==user && g.Request.RecipientUserId==d.UserId &&
                g.Request.StatusId==FamilyRequestStates.Accepted && g.Request.AllowVisaApplicationUse && (g.Request.AccessExpiresAt==null || g.Request.AccessExpiresAt>now))
                .Select(g=>new{g.FamilyRequestId,g.Request.Revision,g.Request.AccessExpiresAt}).FirstOrDefaultAsync();
            if(g==null) return null; deadline=g.AccessExpiresAt; grant=$"{g.FamilyRequestId}:{g.Revision}";
        }
        var version=await db.DocumentVersions.AsNoTracking().SingleOrDefaultAsync(v=>v.DocumentId==id && v.IsCurrent);
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{d.Id}|{d.S3ObjectKey}|{d.UpdatedAt.Ticks}|{version?.Id}|{version?.Sha256Hash}|{grant}")));
        return new(d,fingerprint,deadline);
    }
    private async Task<List<Access>?> Validate(Guid user,VisaState state) {
        var result=new List<Access>();
        foreach(var d in state.Documents) { var a=await Accessible(user,d.Id); if(a==null || a.Fingerprint!=d.Fingerprint) return null; result.Add(a); }
        return result;
    }
    private async Task<List<VisaBoundDocument>> Bind(Guid user,List<VisaDocumentInput> input,VisaRulePack pack)
    {
        if(input.Count>30 || input.Select(d=>d.Id).Distinct().Count()!=input.Count || input.Count(d=>d.Role=="Passport")>1)
            throw new ArgumentException("Select up to 30 different documents and only one primary applicant passport.");
        var result=new List<VisaBoundDocument>();
        foreach(var d in input) {
            if(!pack.DocumentRoles.Contains(d.Role)) throw new ArgumentException("Unknown document role.");
            var access=await Accessible(user,d.Id) ?? throw new KeyNotFoundException("A selected document is unavailable or its sharing has expired.");
            result.Add(new(){Id=d.Id,Role=d.Role,Fingerprint=access.Fingerprint});
        }
        return result;
    }
    public async Task<object> CreateAsync(Guid user,VisaCreateDto input) {
        var template = await db.VisaTemplates.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == input.TemplateId)
            ?? throw new ArgumentException("This country and purpose are not enabled yet.");

        var destinationEnabled = await db.VisaDestinations.AsNoTracking()
            .AnyAsync(d => d.Code == template.CountryCode && d.Enabled);

        if (!destinationEnabled)
            throw new ArgumentException("This country and purpose are not enabled yet.");
        var state=new VisaState{Documents=await Bind(user,input.Documents,Read<VisaRulePack>(template.RulesJson))};
        var a=new VisaApplication{Id=Guid.NewGuid(),UserId=user,TemplateId=template.Id,StateJson=Write(state)};
        db.VisaApplications.Add(a);await db.SaveChangesAsync();return await GetAsync(user,a.Id);
    }
    public async Task<object> ListAsync(Guid user) {
        var apps=await db.VisaApplications.AsNoTracking().Where(a=>a.UserId==user).OrderByDescending(a=>a.UpdatedAt).ToListAsync();
        var result=new List<object>();
        foreach(var a in apps) { var state=Read<VisaState>(a.StateJson); var valid=await Validate(user,state)!=null;
            var assessment=VisaRules.Assess(await Pack(a),state,Today);
            result.Add(new{a.Id,a.TemplateId,a.UpdatedAt,status=!valid?"AccessBlocked":a.Submitted&&assessment.Blockers.Count==0?"Submitted":assessment.Status}); }
        return result;
    }
    public async Task<object> GetAsync(Guid user,Guid id) {
        var a=await Find(user,id);var s=Read<VisaState>(a.StateJson);var accesses=await Validate(user,s);var pack=await Pack(a);
        if(accesses==null) return new { a.Id,a.TemplateId,a.Revision,accessBlocked=true,status="AccessBlocked",
            message="A linked document was changed, deleted, revoked or expired. Reset linked documents to clear the old form, or ask its owner to restore access." };
        var assessment=VisaRules.Assess(pack,s,Today);
        return new { a.Id,a.TemplateId,a.Revision,accessBlocked=false,values=s.Values,checks=s.Checks,sources=s.Sources,
            documents=s.Documents.Select(d=>new{d.Id,d.Role,name=accesses.Single(x=>x.Document.Id==d.Id).Document.DocumentName}),
            activeFields=pack.Fields.Where(f=>VisaRules.Active(f.When,s.Values,Today)).Select(f=>f.Key),
            assessment,status=a.Submitted&&assessment.Blockers.Count==0?"Submitted":assessment.Status,
            accessValidUntilUtc=accesses.Where(x=>x.Deadline!=null).Select(x=>x.Deadline).Min(),
            serverUtc=DateTime.UtcNow };
    }
    public async Task<object> DocumentsAsync(Guid user,Guid id,VisaDocumentsDto input) {
        var a=await Find(user,id);Revision(a,input.Revision);var s=Read<VisaState>(a.StateJson);var pack=await Pack(a);
        var bindings=await Bind(user,input.Documents,pack);
        // Adding evidence preserves edits. Removing, changing a role/version or restoring a grant clears derived content.
        var reset=s.Documents.Any(old=>!bindings.Any(n=>n.Id==old.Id && n.Role==old.Role && n.Fingerprint==old.Fingerprint));
        if(reset) s=new VisaState();s.Documents=bindings;s.Checks.Clear();a.Submitted=false;
        await Save(a,s);return await GetAsync(user,id);
    }
    public async Task<object> UpdateAsync(Guid user,Guid id,VisaUpdateDto input) {
        var a=await Find(user,id);Revision(a,input.Revision);var s=Read<VisaState>(a.StateJson);
        if(await Validate(user,s)==null) throw new InvalidOperationException("Linked document access or content changed. Reset the application documents.");
        var pack=await Pack(a);
        if(input.Values==null || input.Checks==null || input.Values.Count>100 || input.Checks.Count>30 ||
           input.Values.Any(x=>x.Value==null || !pack.Fields.Any(f=>f.Key==x.Key && x.Value.Length<=f.MaxLength)) ||
           input.Checks.Any(x=>x.Value==null || !pack.Checks.Any(c=>c.Key==x.Key) || x.Value.Note==null || x.Value.Note.Length>1500 || x.Value.DocumentId!=null && !s.Documents.Any(d=>d.Id==x.Value.DocumentId)))
            throw new ArgumentException("Invalid form fields or checklist evidence.");
        var changed=input.Values.Count!=s.Values.Count || input.Values.Any(x=>s.Values.GetValueOrDefault(x.Key)!=x.Value);
        s.Values=input.Values.ToDictionary(x=>x.Key,x=>x.Value.Trim());
        s.Checks=changed?[]:input.Checks; a.Submitted=false;
        await Save(a,s);return await GetAsync(user,id);
    }

    private static string? LabelledOcrValue(string text, params string[] labels)
    {
        var alternatives = string.Join("|", labels.Select(Regex.Escape));
        var pattern = $@"(?im)^\s*(?:{alternatives})\s*[:#\-]\s*(?<value>[^\r\n]{{1,100}})\s*$";
        var match = Regex.Match(text, pattern);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static string? OcrDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy",
                              "dd-MM-yyyy", "d-M-yyyy", "dd MMM yyyy",
                              "d MMM yyyy", "dd MMMM yyyy", "d MMMM yyyy" };
        return DateTime.TryParseExact(value.Trim(), formats,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("yyyy-MM-dd") : null;
    }

    private static string? OcrFirstDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var match = Regex.Match(value, @"\b\d{1,2}[/-]\d{1,2}[/-]\d{4}\b");
        return match.Success ? match.Value : null;
    }


    private static string? OcrNextLineValue(string text, string label)
    {
        var lines = text.Split(new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < lines.Length - 1; i++)
        {
            if (lines[i].Contains(label, StringComparison.OrdinalIgnoreCase))
            {
                var value = lines[i + 1].Trim();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }
        return null;
    }

    private static string? OcrPassportNumber(string text)
    {
        var labelled = LabelledOcrValue(
            text, "Passport Number", "Passport No", "Passport No.");
        if (!string.IsNullOrWhiteSpace(labelled)) return labelled;

        var compact = Regex.Replace(text, @"\s+", "");
        var match = Regex.Match(compact, @"([A-Z]\d{7,8})<\d",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? OcrPassportName(string text)
    {
        var labelled = LabelledOcrValue(text, "Full Name");
        if (!string.IsNullOrWhiteSpace(labelled)) return labelled;

        var surname = OcrNextLineValue(text, "Surname");
        var given = OcrNextLineValue(text, "Given Name");

        if (string.IsNullOrWhiteSpace(surname) ||
            string.IsNullOrWhiteSpace(given)) return null;

        return $"{given} {surname}".Trim();
    }

    private static string? OcrPassportDate(string text, bool expiry)
    {
        var labelled = OcrDate(LabelledOcrValue(text,
            expiry ? new[] { "Date of Expiry", "Expiry Date", "Expiration Date" }
                   : new[] { "Date of Issue", "Issue Date" }));
        if (labelled != null) return labelled;

        var lines = text.Split(new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < lines.Length - 1; i++)
        {
            if (lines[i].Contains("Date of Issue", StringComparison.OrdinalIgnoreCase)
                && lines[i].Contains("Date of Expiry", StringComparison.OrdinalIgnoreCase))
            {
                var dates = Regex.Matches(lines[i + 1],
                    @"\b\d{1,2}[/-]\d{1,2}[/-]\d{4}\b");
                var index = expiry ? 1 : 0;
                if (dates.Count > index)
                    return OcrDate(dates[index].Value);
            }
        }

        return null;
    }

    private static string? OcrPassportSex(string text)
    {
        var labelled = LabelledOcrValue(text, "Sex", "Gender");
        if (!string.IsNullOrWhiteSpace(labelled))
        {
            if (labelled.Equals("M", StringComparison.OrdinalIgnoreCase)) return "Male";
            if (labelled.Equals("F", StringComparison.OrdinalIgnoreCase)) return "Female";
            return labelled;
        }

        var value = OcrNextLineValue(text, "Sex");
        var match = value == null ? Match.Empty : Regex.Match(value, @"\b([MF])\b",
            RegexOptions.IgnoreCase);
        if (!match.Success) return null;

        return match.Groups[1].Value.Equals("M", StringComparison.OrdinalIgnoreCase)
            ? "Male" : "Female";
    }

    public async Task<object> PrefillAsync(Guid user,Guid id,Guid revision) {
        var a=await Find(user,id);Revision(a,revision);var s=Read<VisaState>(a.StateJson);
        var access=await Validate(user,s) ?? throw new InvalidOperationException("Document access or content changed. Reset linked documents.");
        if(!s.Documents.Any(d=>d.Role=="Passport")) throw new ArgumentException("Choose one applicant passport role before prefilling.");
        var pack=await Pack(a);
        void Suggest(string key,string? value,Guid doc) {
            var field=pack.Fields.SingleOrDefault(f=>f.Key==key);
            if(field==null || string.IsNullOrWhiteSpace(value) || !string.IsNullOrWhiteSpace(s.Values.GetValueOrDefault(key))) return;
            value=value.Trim();
            if(field.Type=="date") { if(!DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)) return;value=date.ToString("yyyy-MM-dd"); }
            if(value.Length>field.MaxLength || field.Options!=null && !field.Options.Contains(value)) return;
            s.Values[key]=value;s.Sources[key]=doc;
        }
        foreach(var bound in s.Documents.Where(d=>d.Role is "Passport" or "Itinerary" or "Accommodation")) {
            var d=access.Single(x=>x.Document.Id==bound.Id).Document;
            var version=await db.DocumentVersions.AsNoTracking().SingleOrDefaultAsync(v=>v.DocumentId==d.Id && v.IsCurrent);
            var runs=await db.DocumentAnalysisRuns.AsNoTracking().Where(r=>r.DocumentId==d.Id && r.UserId==d.UserId && r.ObjectKey==d.S3ObjectKey && (r.Status=="COMPLETED" || r.Status=="REQUIRES_REVIEW"))
                .OrderByDescending(r=>r.CreatedAt).ToListAsync();
            DocumentAnalysisDto? analysis=null;
            foreach(var run in runs) {
                if(version!=null && (run.DocumentVersionId!=version.Id || version.Sha256Hash!=null && version.Sha256Hash!=run.ContentHash)) continue;
                try { var candidate=Read<DocumentAnalysisDto>(run.ResultJson??"null"); if(candidate?.Provider=="Local" && candidate.Evidence.Count>0) { analysis=candidate;break; } }catch(JsonException){}
            }
            if(bound.Role=="Passport") {
                Suggest("fullName",analysis?.PersonName,d.Id);
                Suggest("passportNumber",analysis?.DocumentNumber,d.Id);
                Suggest("passportIssueDate",analysis?.IssueDate,d.Id);
                Suggest("passportExpiryDate",analysis?.ExpiryDate,d.Id);
            }
            var aliases=bound.Role switch {
                "Passport"=>new Dictionary<string,string>{{"date of birth","birthDate"},{"birth date","birthDate"},{"nationality","nationality"},{"sex","sex"},{"country of birth","birthCountry"},{"place of issue","passportIssuePlace"}},
                "Itinerary"=>new Dictionary<string,string>{{"arrival date","arrivalDate"},{"departure date","departureDate"}},
                _=>new Dictionary<string,string>{{"hotel name","sgBuilding"},{"postal code","sgPostal"},{"street name","sgStreet"},{"contact number","sgPhone"}}
            };
            foreach(var item in analysis?.KeyInformation??[]) if(item.IsExtracted && aliases.TryGetValue(item.Label.Trim().ToLowerInvariant(),out var key)) Suggest(key,item.Value,d.Id);

            // OCR is a fallback only: Suggest never overwrites a populated answer.
            if(d.ExtractionStatus=="COMPLETED" && !string.IsNullOrWhiteSpace(d.ExtractedText)) {
                var text = d.ExtractedText;
                if(bound.Role=="Passport") {
                    Suggest("passportNumber", OcrPassportNumber(text), d.Id);
                    Suggest("fullName", OcrPassportName(text), d.Id);
                    Suggest("passportIssueDate", OcrPassportDate(text, false), d.Id);
                    Suggest("passportExpiryDate", OcrPassportDate(text, true), d.Id);
                    Suggest("birthDate", OcrDate(LabelledOcrValue(text,"Date of Birth","Birth Date"))
                        ?? OcrDate(OcrFirstDate(OcrNextLineValue(text,"Date of Birth"))), d.Id);

                    var nationality = LabelledOcrValue(text,"Nationality")
                        ?? OcrNextLineValue(text,"Nationality");
                    if (nationality != null &&
                        nationality.Contains("INDIAN", StringComparison.OrdinalIgnoreCase))
                        nationality = "India";
                    Suggest("nationality", nationality, d.Id);

                    Suggest("sex", OcrPassportSex(text), d.Id);
                    Suggest("passportIssuePlace",
                        LabelledOcrValue(text,"Place of Issue")
                        ?? OcrNextLineValue(text,"Place of Issue"), d.Id);
                    Suggest("birthCountry",
                        LabelledOcrValue(text,"Country of Birth"), d.Id);
                } else if(bound.Role=="Itinerary") {
                    Suggest("arrivalDate", OcrDate(LabelledOcrValue(text,"Arrival Date")), d.Id);
                    Suggest("departureDate", OcrDate(LabelledOcrValue(text,"Departure Date")), d.Id);
                } else if(bound.Role=="Accommodation") {
                    Suggest("sgBuilding", LabelledOcrValue(text,"Hotel Name"), d.Id);
                    Suggest("sgPostal", LabelledOcrValue(text,"Postal Code"), d.Id);
                    Suggest("sgStreet", LabelledOcrValue(text,"Street Name"), d.Id);
                    Suggest("sgPhone", LabelledOcrValue(text,"Contact Number","Telephone Number"), d.Id);
                }
            }
            if (bound.Role=="Passport")
            {
                Suggest("passportExpiryDate",d.ExpiryDate?.ToString("yyyy-MM-dd"),d.Id);
                Suggest("passportNumber",d.DocumentNumber,d.Id);
            }
        }
        // No answers about race, religion, history, declarations or signature are inferred.
        if(await Validate(user,s)==null) throw new InvalidOperationException("Document sharing expired during prefilling.");
        s.Checks.Clear();a.Submitted=false;await Save(a,s);return await GetAsync(user,id);
    }
    public async Task<object> ExportAsync(Guid user,Guid id,Guid revision,string form,CancellationToken ct) {
        if(form is not ("14a" or "v39a")) throw new ArgumentException("Unknown form.");
        var a=await Find(user,id);Revision(a,revision);var s=Read<VisaState>(a.StateJson);
        if(await Validate(user,s)==null) throw new InvalidOperationException("Document access or content changed. Export is blocked.");
        if(form=="v39a" && s.Values.GetValueOrDefault("loiRequired")!="Yes") throw new ArgumentException("Enable V39A in the checklist setup first.");
        var pack=await Pack(a);
        if (!pack.IsSingaporeVisit) throw new ArgumentException("Official PDF export is not available for this template. Use the linked official filing portal.");
        var values=s.Values.Where(v=>pack.Fields.Any(f=>f.Key==v.Key && VisaRules.Active(f.When,s.Values,Today))).ToDictionary(x=>x.Key,x=>x.Value);
        var uri=new Uri(config["DocumentIntelligence:ServiceUrl"]??"http://localhost:8090/");
        if(!uri.IsLoopback) throw new InvalidOperationException("Form export requires the private local document service.");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(30));ct=timeout.Token;
        using var req=new HttpRequestMessage(HttpMethod.Post,new Uri(uri,"visa/forms/"+form));
        req.Headers.Add("X-Service-Key",config["DocumentIntelligence:ServiceKey"]);
        req.Content=JsonContent.Create(new{templateId=a.TemplateId,values});
        using var res=await client.SendAsync(req,ct);
        if(!res.IsSuccessStatusCode) throw new InvalidOperationException("Form export unavailable. Check the document-ai form service and field values.");
        var output=await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken:ct);
        if(await Validate(user,s)==null) throw new InvalidOperationException("Document sharing expired during export.");
        return output;
    }
    public async Task<object> SubmitAsync(Guid user,Guid id,Guid revision) {
        var a=await Find(user,id);Revision(a,revision);var s=Read<VisaState>(a.StateJson);
        if(await Validate(user,s)==null || VisaRules.Assess(await Pack(a),s,Today).Blockers.Count>0) throw new InvalidOperationException("Resolve application checklist items first.");
        a.Submitted=true;await Save(a,s);return await GetAsync(user,id);
    }
    public async Task DeleteAsync(Guid user,Guid id,Guid revision) { var a=await Find(user,id);Revision(a,revision);db.Remove(a);await db.SaveChangesAsync(); }
}
