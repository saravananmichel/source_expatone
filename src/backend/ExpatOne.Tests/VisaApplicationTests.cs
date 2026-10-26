using System.Text.Json;
using ExpatOne.Application.DTOs;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace ExpatOne.Tests;

public class VisaApplicationTests : IDisposable
{
    private readonly ExpatOneDbContext db;
    private readonly VisaApplicationService service;
    private readonly Guid owner=Guid.NewGuid(), viewer=Guid.NewGuid(), doc=Guid.NewGuid();
    private readonly HttpClient client=new();
    public VisaApplicationTests() {
        db=new(new DbContextOptionsBuilder<ExpatOneDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection();db.Database.EnsureCreated();
        db.Users.AddRange(new User{Id=owner,Email="owner@visa.test",ExternalId="owner"},new User{Id=viewer,Email="viewer@visa.test",ExternalId="viewer"});
        var type = db.DocumentTypes.Single(t => t.Name == "Passport");
        db.Documents.Add(new Document{Id=doc,UserId=owner,DocumentTypeId=type.Id,DocumentName="Synthetic passport",S3ObjectKey="private/test",DocumentNumber="A1234567",ExpiryDate=DateTime.UtcNow.AddYears(5)});
        db.SaveChanges();service=new(db,client,new ConfigurationBuilder().Build());
    }
    public void Dispose() {db.Dispose();client.Dispose();}
    private static JsonElement J(object value)=>JsonSerializer.SerializeToElement(value,VisaRules.Json);
    private async Task<JsonElement> Create(Guid user)=>J(await service.CreateAsync(user,new VisaCreateDto{Documents=[new(){Id=doc,Role="Passport"}]}));
    private async Task<FamilyRequestDto> Share() {var f=new FamilyService(db);var r=await f.SendAsync(viewer,"owner@visa.test");return await f.RespondAsync(owner,r.Id,new FamilyDecisionDto{Revision=r.Revision,Accept=true,DocumentIds=[doc],AccessExpiresAt=DateTimeOffset.UtcNow.AddHours(1)});}
    [Fact] public async Task PriorityCountryDraftsUseTheirOwnTemplateAndRejectSingaporeExport() {
        var templates = await db.VisaTemplates.Where(t => t.CountryCode != "SG").ToListAsync();
        Assert.Equal(12, templates.Count);
        foreach (var template in templates) {
            var draft = J(await service.CreateAsync(owner, new VisaCreateDto {
                TemplateId = template.Id, Documents = [new() { Id = doc, Role = "Passport" }]
            }));
            Assert.Equal(template.Id, draft.GetProperty("templateId").GetString());
            Assert.DoesNotContain("SG Arrival Card", draft.GetProperty("assessment").GetProperty("travelNote").GetString()!);
            await Assert.ThrowsAsync<ArgumentException>(() => service.ExportAsync(owner,
                draft.GetProperty("id").GetGuid(), draft.GetProperty("revision").GetGuid(), "14a", default));
        }
        (await db.VisaDestinations.FindAsync("VU"))!.Enabled = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(owner,
            new VisaCreateDto { TemplateId = "VU-VISIT-2026-10-26" }));
    }

    [Fact] public async Task PrefillUsesCompletedLabelledOcrWithoutOverwritingUserValues() {
        var document = await db.Documents.FindAsync(doc);
        document!.ExtractionStatus = "COMPLETED";
        document.ExtractedText = """
            Passport Number: OCR999999
            Full Name: OCR TEST APPLICANT
            Date of Birth: 15/06/1985
            Nationality: India
            Sex: Male
            """;
        await db.SaveChangesAsync();

        var a = await Create(owner);
        var id = a.GetProperty("id").GetGuid();
        a = J(await service.UpdateAsync(owner, id, new() {
            Revision = a.GetProperty("revision").GetGuid(),
            Values = new() { { "passportNumber", "MANUAL123" } }
        }));

        a = J(await service.PrefillAsync(owner, id, a.GetProperty("revision").GetGuid()));
        var values = a.GetProperty("values");

        Assert.Equal("MANUAL123", values.GetProperty("passportNumber").GetString());
        Assert.Equal("OCR TEST APPLICANT", values.GetProperty("fullName").GetString());
        Assert.Equal("1985-06-15", values.GetProperty("birthDate").GetString());
        Assert.Equal("India", values.GetProperty("nationality").GetString());
        Assert.Equal("Male", values.GetProperty("sex").GetString());
    }

    [Fact] public async Task PrefillReadsSplitLinePassportOcrAndMrz()
    {
        var document = await db.Documents.FindAsync(doc);
        document!.ExtractionStatus = "COMPLETED";
        document.ExtractedText = """
            Surname / Nom
            MICHEL RAJ
            Given Name
            SARAVANAN
            Date of Birth / Sex
            09/11/1983 M
            Nationality
            INDIAN
            Place of Issue
            CHENNAI
            Date of Issue / Date of Expiry
            23/12/2021 22/12/2031
            V507
            3795<5
            """;
        await db.SaveChangesAsync();

        var application = await Create(owner);
        var id = application.GetProperty("id").GetGuid();
        application = J(await service.PrefillAsync(
            owner, id, application.GetProperty("revision").GetGuid()));

        var values = application.GetProperty("values");
        Assert.Equal("V5073795", values.GetProperty("passportNumber").GetString());
        Assert.Equal("SARAVANAN MICHEL RAJ", values.GetProperty("fullName").GetString());
        Assert.Equal("1983-11-09", values.GetProperty("birthDate").GetString());
        Assert.Equal("India", values.GetProperty("nationality").GetString());
        Assert.Equal("Male", values.GetProperty("sex").GetString());
        Assert.Equal("2021-12-23", values.GetProperty("passportIssueDate").GetString());
        Assert.Equal("2031-12-22", values.GetProperty("passportExpiryDate").GetString());
        Assert.Equal("CHENNAI", values.GetProperty("passportIssuePlace").GetString());
    }

    [Fact] public async Task OwnerScopeAndMissingDocumentAreEnforced() {
        var a=await Create(owner);var id=a.GetProperty("id").GetGuid();
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>service.GetAsync(viewer,id));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Create(viewer));
    }
    [Fact] public async Task SharedPrefillIsHiddenAfterExpiryAndExportIsBlocked() {
        var r=await Share();var a=await Create(viewer);var id=a.GetProperty("id").GetGuid();
        a=J(await service.PrefillAsync(viewer,id,a.GetProperty("revision").GetGuid()));
        Assert.Equal("A1234567",a.GetProperty("values").GetProperty("passportNumber").GetString());
        (await db.FamilyRequests.FindAsync(r.Id))!.AccessExpiresAt=DateTime.UtcNow.AddSeconds(-1);await db.SaveChangesAsync();
        var blocked=J(await service.GetAsync(viewer,id));Assert.True(blocked.GetProperty("accessBlocked").GetBoolean());Assert.False(blocked.TryGetProperty("values",out _));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ExportAsync(viewer,id,a.GetProperty("revision").GetGuid(),"14a",default));
    }
    [Fact] public async Task RevocationAndVersionChangeRequireReset() {
        var r=await Share();var a=await Create(viewer);var id=a.GetProperty("id").GetGuid();
        await new FamilyService(db).EndAsync(owner,r.Id,r.Revision);
        Assert.True(J(await service.GetAsync(viewer,id)).GetProperty("accessBlocked").GetBoolean());
        a=J(await service.DocumentsAsync(viewer,id,new(){Revision=a.GetProperty("revision").GetGuid(),Documents=[]}));
        Assert.False(a.GetProperty("accessBlocked").GetBoolean());Assert.Empty(a.GetProperty("values").EnumerateObject());
        var own=await Create(owner);(await db.Documents.FindAsync(doc))!.S3ObjectKey="private/new";await db.SaveChangesAsync();
        Assert.True(J(await service.GetAsync(owner,own.GetProperty("id").GetGuid())).GetProperty("accessBlocked").GetBoolean());
    }
    [Fact] public async Task FieldEditsClearChecksAndStaleRevisionIsRejected() {
        var a=await Create(owner);var id=a.GetProperty("id").GetGuid();var old=a.GetProperty("revision").GetGuid();
        a=J(await service.UpdateAsync(owner,id,new(){Revision=old,Values=new(){{"fullName","SAMPLE"}},Checks=new(){{"formReviewed",new(){Confirmed=true}}}}));
        Assert.Empty(a.GetProperty("checks").EnumerateObject());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.PrefillAsync(owner,id,old));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SubmitAsync(owner,id,a.GetProperty("revision").GetGuid()));
    }
    [Fact] public async Task TwoPrimaryPassportsAndUnknownFieldsAreRejected() {
        await Assert.ThrowsAsync<ArgumentException>(()=>service.CreateAsync(owner,new(){Documents=[new(){Id=doc,Role="Passport"},new(){Id=doc,Role="Passport"}]}));
        var a=await Create(owner);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.UpdateAsync(owner,a.GetProperty("id").GetGuid(),new(){Revision=a.GetProperty("revision").GetGuid(),Values=new(){{"signature","FORGED"}}}));
    }
    [Fact] public void ReadinessRequiresAllApplicableEvidenceAndValidDates() {
        var pack=JsonSerializer.Deserialize<VisaRulePack>(VisaRules.SeedJson(),VisaRules.Json)!;
        var today=new DateOnly(2026,10,12);
        var s=new VisaState{Values=new(){
            {"nationality","India"},{"passportType","International Passport"},{"birthDate","1985-06-15"},{"maritalStatus","Single"},
            {"arrivalDate","2026-11-01"},{"departureDate","2026-11-10"},{"passportExpiryDate","2030-01-01"},{"passportIssueDate","2020-01-01"},
            {"sgBlock","1"},{"stayBand","Less than 30 days"},{"photoDate","2026-10-01"},{"loiRequired","No"},{"translationRequired","No"},{"otherResidence","No"}}};
        for(int i=1;i<=4;i++)s.Values["antecedent"+i]="No";
        foreach(var f in pack.Fields.Where(f=>f.Required && VisaRules.Active(f.When,s.Values,today))) {
            if(s.Values.ContainsKey(f.Key))continue;
            s.Values[f.Key]=f.Options?.First()??(f.Type=="email"?"test@example.com":f.Type=="number"?"0":"Sample");
        }
        foreach(var c in pack.Checks.Where(c=>VisaRules.Active(c.When,s.Values,today))) {
            Guid? id=c.NeedsDocument?Guid.NewGuid():null;
            if(id!=null)s.Documents.Add(new(){Id=id.Value,Role=c.Role!});
            s.Checks[c.Key]=new(){Confirmed=true,DocumentId=id,Note="Reviewed current official route requirements; no additional documents requested."};
        }
        Assert.Empty(VisaRules.Assess(pack,s,today).Blockers);
        s.Values["passportExpiryDate"]="2027-04-30";
        Assert.Contains(VisaRules.Assess(pack,s,today).Blockers,b=>b.Contains("six months"));
        s.Values["passportExpiryDate"]="2030-01-01";s.Values["loiRequired"]="Not confirmed";
        Assert.NotEmpty(VisaRules.Assess(pack,s,today).Blockers);
        s.Values["loiRequired"]="No";s.Values["nationality"]="Malaysia";
        Assert.Contains(VisaRules.Assess(pack,s,today).Blockers,b=>b.Contains("Indian ordinary"));
        Assert.Contains(VisaRules.Assess(pack,s,new(2027,1,11)).Blockers,b=>b.Contains("republished"));
    }
}
