using ExpatOne.Api.Services;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpatOne.Tests;

public class KnowledgeSeedTests
{
    private (ExpatOneDbContext context, KnowledgeIngestionService ingestion, KnowledgeSeedService seed) CreateServices()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase($"SeedTests_{Guid.NewGuid()}")
            .Options;
        var context = new ExpatOneDbContext(options);

        var mockAi = new Mock<IAIService>();
        mockAi.Setup(a => a.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(new float[768]);

        var ingestionLogger = new Mock<ILogger<KnowledgeIngestionService>>().Object;
        var ingestion = new KnowledgeIngestionService(context, mockAi.Object, ingestionLogger);

        var seedLogger = new Mock<ILogger<KnowledgeSeedService>>().Object;
        var seed = new KnowledgeSeedService(ingestion, seedLogger);

        return (context, ingestion, seed);
    }

    [Fact]
    public async Task Seed_CreatesSourcesAndChunks()
    {
        var (ctx, _, seed) = CreateServices();

        var (sources, chunks, dupes) = await seed.SeedImmigrationBatch1Async();

        Assert.True(sources > 0, "Should create sources");
        Assert.True(chunks > 0, "Should create chunks");
        Assert.Equal(0, dupes);

        var dbSources = await ctx.GovernmentSources.CountAsync();
        Assert.Equal(sources, dbSources);

        var dbChunks = await ctx.GovernmentKnowledge.CountAsync();
        Assert.Equal(chunks, dbChunks);
    }

    [Fact]
    public async Task Seed_IsIdempotent()
    {
        var (_, _, seed) = CreateServices();

        var first = await seed.SeedImmigrationBatch1Async();
        Assert.True(first.sourcesCreated > 0);
        Assert.True(first.chunksCreated > 0);

        var second = await seed.SeedImmigrationBatch1Async();
        Assert.Equal(0, second.sourcesCreated);
        Assert.Equal(0, second.chunksCreated);
        Assert.Equal(first.sourcesCreated, second.duplicatesSkipped);
    }

    [Fact]
    public async Task Seed_AllChunksHaveCategory()
    {
        var (ctx, _, seed) = CreateServices();
        await seed.SeedImmigrationBatch1Async();

        var chunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(chunks, c => Assert.Equal("immigration", c.Category));
    }

    [Fact]
    public async Task Seed_AllChunksHaveSourceProvenance()
    {
        var (ctx, _, seed) = CreateServices();
        await seed.SeedImmigrationBatch1Async();

        var chunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(chunks, c =>
        {
            Assert.NotNull(c.GovernmentSourceId);
            Assert.NotNull(c.SourceUrl);
            Assert.NotNull(c.Department);
            Assert.Equal("MY", c.CountryCode);
            Assert.NotNull(c.ContentHash);
        });
    }

    [Fact]
    public async Task Seed_SourcesHaveUrls()
    {
        var (ctx, _, seed) = CreateServices();
        await seed.SeedImmigrationBatch1Async();

        var sources = await ctx.GovernmentSources.ToListAsync();
        Assert.All(sources, s =>
        {
            Assert.NotNull(s.Url);
            Assert.True(s.Url!.StartsWith("https://"), $"Source URL should be HTTPS: {s.Url}");
            Assert.True(s.IsActive);
            Assert.Equal("MY", s.CountryCode);
        });
    }

    [Fact]
    public async Task SeedTax_CreatesSourcesAndChunks()
    {
        var (ctx, _, seed) = CreateServices();

        var (sources, chunks, dupes) = await seed.SeedTaxBatch2Async();

        Assert.True(sources > 0, "Should create tax sources");
        Assert.True(chunks > 0, "Should create tax chunks");
        Assert.Equal(0, dupes);

        var dbChunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(dbChunks, c => Assert.Equal("tax", c.Category));
    }

    [Fact]
    public async Task SeedTax_IsIdempotent()
    {
        var (_, _, seed) = CreateServices();

        var first = await seed.SeedTaxBatch2Async();
        Assert.True(first.sourcesCreated > 0);

        var second = await seed.SeedTaxBatch2Async();
        Assert.Equal(0, second.sourcesCreated);
        Assert.Equal(0, second.chunksCreated);
        Assert.Equal(first.sourcesCreated, second.duplicatesSkipped);
    }

    [Fact]
    public async Task SeedTax_AllChunksHaveProvenance()
    {
        var (ctx, _, seed) = CreateServices();
        await seed.SeedTaxBatch2Async();

        var chunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(chunks, c =>
        {
            Assert.NotNull(c.GovernmentSourceId);
            Assert.NotNull(c.SourceUrl);
            Assert.StartsWith("https://www.hasil.gov.my/", c.SourceUrl!);
            Assert.NotNull(c.Department);
            Assert.Contains("LHDN", c.Department!);
        });
    }

    [Fact]
    public async Task SeedBothBatches_NoCrossContamination()
    {
        var (ctx, _, seed) = CreateServices();

        await seed.SeedImmigrationBatch1Async();
        await seed.SeedTaxBatch2Async();

        var immigrationChunks = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "immigration")
            .CountAsync();
        var taxChunks = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "tax")
            .CountAsync();

        Assert.True(immigrationChunks > 0);
        Assert.True(taxChunks > 0);

        var allSources = await ctx.GovernmentSources.ToListAsync();
        var hasilSources = allSources.Where(s => s.Url?.Contains("hasil.gov.my") == true).ToList();
        var esdSources = allSources.Where(s => s.Url?.Contains("imi.gov.my") == true).ToList();

        Assert.True(hasilSources.Count > 0);
        Assert.True(esdSources.Count > 0);
    }

    [Fact]
    public async Task SeedDriving_CreatesSourcesAndChunks()
    {
        var (ctx, _, seed) = CreateServices();

        var (sources, chunks, dupes) = await seed.SeedDrivingBatch3Async();

        Assert.True(sources > 0, "Should create driving sources");
        Assert.True(chunks > 0, "Should create driving chunks");
        Assert.Equal(0, dupes);

        var dbChunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(dbChunks, c => Assert.Equal("driving", c.Category));
    }

    [Fact]
    public async Task SeedDriving_IsIdempotent()
    {
        var (_, _, seed) = CreateServices();

        var first = await seed.SeedDrivingBatch3Async();
        Assert.True(first.sourcesCreated > 0);

        var second = await seed.SeedDrivingBatch3Async();
        Assert.Equal(0, second.sourcesCreated);
        Assert.Equal(0, second.chunksCreated);
        Assert.Equal(first.sourcesCreated, second.duplicatesSkipped);
    }

    [Fact]
    public async Task SeedAllThreeBatches_CorrectCategories()
    {
        var (ctx, _, seed) = CreateServices();

        await seed.SeedImmigrationBatch1Async();
        await seed.SeedTaxBatch2Async();
        await seed.SeedDrivingBatch3Async();

        var categories = await ctx.GovernmentKnowledge
            .Select(k => k.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        Assert.Contains("driving", categories);
        Assert.Contains("immigration", categories);
        Assert.Contains("tax", categories);
    }

    [Fact]
    public async Task SeedEmployment_CreatesSourcesAndChunks()
    {
        var (ctx, _, seed) = CreateServices();

        var (sources, chunks, dupes) = await seed.SeedEmploymentBatch4Async();

        Assert.True(sources > 0, "Should create employment sources");
        Assert.True(chunks > 0, "Should create employment chunks");
        Assert.Equal(0, dupes);

        var dbChunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(dbChunks, c => Assert.Equal("employment", c.Category));
    }

    [Fact]
    public async Task SeedEmployment_IsIdempotent()
    {
        var (_, _, seed) = CreateServices();

        var first = await seed.SeedEmploymentBatch4Async();
        Assert.True(first.sourcesCreated > 0);

        var second = await seed.SeedEmploymentBatch4Async();
        Assert.Equal(0, second.sourcesCreated);
        Assert.Equal(0, second.chunksCreated);
        Assert.Equal(first.sourcesCreated, second.duplicatesSkipped);
    }

    [Fact]
    public async Task SeedEmployment_AllChunksHaveProvenance()
    {
        var (ctx, _, seed) = CreateServices();
        await seed.SeedEmploymentBatch4Async();

        var chunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(chunks, c =>
        {
            Assert.NotNull(c.GovernmentSourceId);
            Assert.NotNull(c.SourceUrl);
            Assert.NotNull(c.Department);
            Assert.Equal("MY", c.CountryCode);
            Assert.NotNull(c.ContentHash);
        });
    }

    [Fact]
    public async Task SeedAllFourBatches_CorrectCategories()
    {
        var (ctx, _, seed) = CreateServices();

        await seed.SeedImmigrationBatch1Async();
        await seed.SeedTaxBatch2Async();
        await seed.SeedDrivingBatch3Async();
        await seed.SeedEmploymentBatch4Async();

        var categories = await ctx.GovernmentKnowledge
            .Select(k => k.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        Assert.Contains("driving", categories);
        Assert.Contains("employment", categories);
        Assert.Contains("immigration", categories);
        Assert.Contains("tax", categories);
    }

    [Fact]
    public async Task SeedEmployment_NoCrossContaminationWithOtherBatches()
    {
        var (ctx, _, seed) = CreateServices();

        await seed.SeedImmigrationBatch1Async();
        await seed.SeedTaxBatch2Async();
        await seed.SeedDrivingBatch3Async();
        await seed.SeedEmploymentBatch4Async();

        var employmentChunks = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "employment")
            .CountAsync();
        var immigrationChunks = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "immigration")
            .CountAsync();
        var taxChunks = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "tax")
            .CountAsync();
        var drivingChunks = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "driving")
            .CountAsync();

        Assert.True(employmentChunks > 0);
        Assert.True(immigrationChunks > 0);
        Assert.True(taxChunks > 0);
        Assert.True(drivingChunks > 0);
    }

    [Fact]
    public async Task SeedEmployment_AllSourceUrlsAreHttpOrHttps()
    {
        var (ctx, _, seed) = CreateServices();
        await seed.SeedEmploymentBatch4Async();

        var sources = await ctx.GovernmentSources.Where(s => s.IsActive).ToListAsync();
        Assert.All(sources, s =>
        {
            Assert.NotNull(s.Url);
            Assert.True(
                s.Url!.StartsWith("https://") || s.Url.StartsWith("http://"),
                $"Source URL should be HTTP/HTTPS: {s.Url}");
            Assert.True(s.IsActive);
            Assert.Equal("MY", s.CountryCode);
        });
    }

    [Fact]
    public async Task SeedEmployment_DeactivatesOldSyntheticSources()
    {
        var (ctx, ingestion, seed) = CreateServices();

        var syntheticSource = await ingestion.RegisterSourceAsync(new RegisterSourceDto
        {
            Name = "Employment Act 1955 — Core Employee Rights",
            Url = "http://jtksm.mohr.gov.my/ms/akta-dan-peraturan/akta-kerja-1955",
            Department = "JTKSM",
            CountryCode = "MY",
        });

        await seed.SeedEmploymentBatch4Async();

        var deactivated = await ctx.GovernmentSources.FindAsync(syntheticSource.Id);
        Assert.NotNull(deactivated);
        Assert.False(deactivated!.IsActive);
    }

    [Fact]
    public async Task SeedEmployment_NoInventedUrlsRemainActive()
    {
        var (ctx, _, seed) = CreateServices();
        await seed.SeedEmploymentBatch4Async();

        string[] inventedUrls =
        [
            "http://jtksm.mohr.gov.my/ms/akta-dan-peraturan/akta-kerja-1955",
            "http://jtksm.mohr.gov.my/ms/akta-dan-peraturan/akta-kerja-1955/fasal-penamatan",
            "http://jtksm.mohr.gov.my/ms/perkhidmatan/penggajian-pekerja-asing/seksyen-60k",
            "http://jtksm.mohr.gov.my/ms/perkhidmatan/penggajian-pekerja-asing",
            "http://jtksm.mohr.gov.my/ms/akta-dan-peraturan/kontrak-perkhidmatan",
            "https://www.mohr.gov.my/index.php/en/legislation/acts",
            "https://www.kwsp.gov.my/",
            "https://jpp.mohr.gov.my/index.php/en/",
            "https://perkeso.gov.my/en/lindung-kerjaya/",
        ];

        var activeSources = await ctx.GovernmentSources
            .Where(s => s.IsActive)
            .ToListAsync();

        foreach (var url in inventedUrls)
        {
            Assert.DoesNotContain(activeSources, s => s.Url == url);
        }
    }

    [Fact]
    public async Task SeedEmployment_ReplacedSourceContentRemovesOldChunks()
    {
        var (ctx, ingestion, seed) = CreateServices();

        var oldSource = await ingestion.RegisterSourceAsync(new RegisterSourceDto
        {
            Name = "Employment Act 1955 — Core Employee Rights",
            Url = "http://jtksm.mohr.gov.my/ms/akta-dan-peraturan/akta-kerja-1955",
            Department = "JTKSM",
            CountryCode = "MY",
        });
        await ingestion.IngestSourceAsync(oldSource.Id, new IngestContentDto
        {
            Title = "Old synthetic content",
            Category = "employment",
            Content = "This is old synthetic content that should be removed.",
        });

        var oldChunksBefore = await ctx.GovernmentKnowledge
            .Where(k => k.GovernmentSourceId == oldSource.Id)
            .CountAsync();
        Assert.True(oldChunksBefore > 0);

        await seed.SeedEmploymentBatch4Async();

        var oldSource2 = await ctx.GovernmentSources.FindAsync(oldSource.Id);
        Assert.False(oldSource2!.IsActive);

        var activeChunks = await ctx.GovernmentKnowledge
            .Where(k => k.GovernmentSourceId == oldSource.Id)
            .CountAsync();
        Assert.True(activeChunks >= 0);
    }

    [Fact]
    public async Task SeedEmployment_OtherBatchesRemainIntact()
    {
        var (ctx, _, seed) = CreateServices();

        await seed.SeedImmigrationBatch1Async();
        await seed.SeedTaxBatch2Async();
        await seed.SeedDrivingBatch3Async();

        var immigrationBefore = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "immigration").CountAsync();
        var taxBefore = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "tax").CountAsync();
        var drivingBefore = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "driving").CountAsync();

        await seed.SeedEmploymentBatch4Async();

        var immigrationAfter = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "immigration").CountAsync();
        var taxAfter = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "tax").CountAsync();
        var drivingAfter = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "driving").CountAsync();

        Assert.Equal(immigrationBefore, immigrationAfter);
        Assert.Equal(taxBefore, taxAfter);
        Assert.Equal(drivingBefore, drivingAfter);
    }

    [Fact]
    public async Task SeedPerkeso_CreatesSourcesAndChunks()
    {
        var (ctx, _, seed) = CreateServices();

        var (sources, chunks, dupes) = await seed.SeedPerkesoBatch5Async();

        Assert.True(sources > 0, "Should create PERKESO sources");
        Assert.True(chunks > 0, "Should create PERKESO chunks");
        Assert.Equal(0, dupes);

        var dbChunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(dbChunks, c => Assert.Equal("employment", c.Category));
    }

    [Fact]
    public async Task SeedPerkeso_IsIdempotent()
    {
        var (_, _, seed) = CreateServices();

        var first = await seed.SeedPerkesoBatch5Async();
        Assert.True(first.sourcesCreated > 0);

        var second = await seed.SeedPerkesoBatch5Async();
        Assert.Equal(0, second.sourcesCreated);
        Assert.Equal(0, second.chunksCreated);
        Assert.Equal(first.sourcesCreated, second.duplicatesSkipped);
    }

    [Fact]
    public async Task SeedPerkeso_AllChunksHaveProvenance()
    {
        var (ctx, _, seed) = CreateServices();
        await seed.SeedPerkesoBatch5Async();

        var chunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(chunks, c =>
        {
            Assert.NotNull(c.GovernmentSourceId);
            Assert.NotNull(c.SourceUrl);
            Assert.StartsWith("https://www.perkeso.gov.my/", c.SourceUrl!);
            Assert.NotNull(c.Department);
            Assert.Contains("PERKESO", c.Department!);
            Assert.Equal("MY", c.CountryCode);
            Assert.NotNull(c.ContentHash);
        });
    }

    [Fact]
    public async Task SeedPerkeso_AllSourceUrlsAreHttps()
    {
        var (ctx, _, seed) = CreateServices();
        await seed.SeedPerkesoBatch5Async();

        var sources = await ctx.GovernmentSources.Where(s => s.IsActive).ToListAsync();
        Assert.All(sources, s =>
        {
            Assert.NotNull(s.Url);
            Assert.True(s.Url!.StartsWith("https://"), $"Source URL should be HTTPS: {s.Url}");
            Assert.True(s.IsActive);
            Assert.Equal("MY", s.CountryCode);
        });
    }

    [Fact]
    public async Task SeedPerkeso_NoCrossContaminationWithOtherBatches()
    {
        var (ctx, _, seed) = CreateServices();

        await seed.SeedImmigrationBatch1Async();
        await seed.SeedTaxBatch2Async();
        await seed.SeedDrivingBatch3Async();
        await seed.SeedEmploymentBatch4Async();

        var immigrationBefore = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "immigration").CountAsync();
        var taxBefore = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "tax").CountAsync();
        var drivingBefore = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "driving").CountAsync();
        var employmentBefore = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "employment").CountAsync();

        await seed.SeedPerkesoBatch5Async();

        var immigrationAfter = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "immigration").CountAsync();
        var taxAfter = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "tax").CountAsync();
        var drivingAfter = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "driving").CountAsync();

        Assert.Equal(immigrationBefore, immigrationAfter);
        Assert.Equal(taxBefore, taxAfter);
        Assert.Equal(drivingBefore, drivingAfter);

        var employmentAfter = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "employment").CountAsync();
        Assert.True(employmentAfter > employmentBefore,
            "PERKESO batch should add employment chunks on top of Batch 4");
    }

    [Fact]
    public async Task SeedAllFiveBatches_CorrectCategories()
    {
        var (ctx, _, seed) = CreateServices();

        await seed.SeedImmigrationBatch1Async();
        await seed.SeedTaxBatch2Async();
        await seed.SeedDrivingBatch3Async();
        await seed.SeedEmploymentBatch4Async();
        await seed.SeedPerkesoBatch5Async();

        var categories = await ctx.GovernmentKnowledge
            .Select(k => k.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        Assert.Contains("driving", categories);
        Assert.Contains("employment", categories);
        Assert.Contains("immigration", categories);
        Assert.Contains("tax", categories);
    }

    [Fact]
    public async Task SeedHealthcare_CreatesSourcesAndChunks()
    {
        var (ctx, _, seed) = CreateServices();

        var (sources, chunks, dupes) = await seed.SeedHealthcareBatch6Async();

        Assert.True(sources > 0, "Should create healthcare sources");
        Assert.True(chunks > 0, "Should create healthcare chunks");
        Assert.Equal(0, dupes);

        var dbChunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(dbChunks, c => Assert.Equal("healthcare", c.Category));
    }

    [Fact]
    public async Task SeedHealthcare_IsIdempotent()
    {
        var (_, _, seed) = CreateServices();

        var first = await seed.SeedHealthcareBatch6Async();
        Assert.True(first.sourcesCreated > 0);

        var second = await seed.SeedHealthcareBatch6Async();
        Assert.Equal(0, second.sourcesCreated);
        Assert.Equal(0, second.chunksCreated);
        Assert.Equal(first.sourcesCreated, second.duplicatesSkipped);
    }

    [Fact]
    public async Task SeedHealthcare_AllChunksHaveProvenance()
    {
        var (ctx, _, seed) = CreateServices();
        await seed.SeedHealthcareBatch6Async();

        var chunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(chunks, c =>
        {
            Assert.NotNull(c.GovernmentSourceId);
            Assert.NotNull(c.SourceUrl);
            Assert.StartsWith("https://www.imi.gov.my/", c.SourceUrl!);
            Assert.NotNull(c.Department);
            Assert.Contains("Immigration", c.Department!);
            Assert.Equal("MY", c.CountryCode);
            Assert.NotNull(c.ContentHash);
        });
    }

    [Fact]
    public async Task SeedHealthcare_AllSourceUrlsAreHttps()
    {
        var (ctx, _, seed) = CreateServices();
        await seed.SeedHealthcareBatch6Async();

        var sources = await ctx.GovernmentSources.Where(s => s.IsActive).ToListAsync();
        Assert.All(sources, s =>
        {
            Assert.NotNull(s.Url);
            Assert.True(
                s.Url!.StartsWith("https://") || s.Url.StartsWith("http://"),
                $"URL should be HTTP/HTTPS: {s.Url}");
        });
    }

    [Fact]
    public async Task SeedHealthcare_NoCrossContaminationWithOtherBatches()
    {
        var (ctx, _, seed) = CreateServices();

        await seed.SeedImmigrationBatch1Async();
        await seed.SeedTaxBatch2Async();
        await seed.SeedDrivingBatch3Async();
        await seed.SeedEmploymentBatch4Async();
        await seed.SeedPerkesoBatch5Async();

        var immigrationBefore = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "immigration").CountAsync();
        var taxBefore = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "tax").CountAsync();
        var drivingBefore = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "driving").CountAsync();
        var employmentBefore = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "employment").CountAsync();

        await seed.SeedHealthcareBatch6Async();

        Assert.Equal(immigrationBefore, await ctx.GovernmentKnowledge
            .Where(k => k.Category == "immigration").CountAsync());
        Assert.Equal(taxBefore, await ctx.GovernmentKnowledge
            .Where(k => k.Category == "tax").CountAsync());
        Assert.Equal(drivingBefore, await ctx.GovernmentKnowledge
            .Where(k => k.Category == "driving").CountAsync());
        Assert.Equal(employmentBefore, await ctx.GovernmentKnowledge
            .Where(k => k.Category == "employment").CountAsync());

        var healthcareAfter = await ctx.GovernmentKnowledge
            .Where(k => k.Category == "healthcare").CountAsync();
        Assert.True(healthcareAfter > 0, "Healthcare batch should create chunks");
    }

    [Fact]
    public async Task SeedAllSixBatches_CorrectCategories()
    {
        var (ctx, _, seed) = CreateServices();

        await seed.SeedImmigrationBatch1Async();
        await seed.SeedTaxBatch2Async();
        await seed.SeedDrivingBatch3Async();
        await seed.SeedEmploymentBatch4Async();
        await seed.SeedPerkesoBatch5Async();
        await seed.SeedHealthcareBatch6Async();

        var categories = await ctx.GovernmentKnowledge
            .Select(k => k.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        Assert.Contains("driving", categories);
        Assert.Contains("employment", categories);
        Assert.Contains("healthcare", categories);
        Assert.Contains("immigration", categories);
        Assert.Contains("tax", categories);
    }

    [Fact]
    public async Task SeedEducation_CreatesSourcesAndChunks()
    {
        var (ctx, _, seed) = CreateServices();

        var (sources, chunks, dupes) = await seed.SeedEducationBatch7Async();

        Assert.True(sources > 0, "Should create education sources");
        Assert.True(chunks > 0, "Should create education chunks");
        Assert.Equal(0, dupes);

        var dbChunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(dbChunks, c => Assert.Equal("education", c.Category));
    }

    [Fact]
    public async Task SeedEducation_IsIdempotent()
    {
        var (_, _, seed) = CreateServices();

        var first = await seed.SeedEducationBatch7Async();
        Assert.True(first.sourcesCreated > 0);

        var second = await seed.SeedEducationBatch7Async();
        Assert.Equal(0, second.sourcesCreated);
        Assert.Equal(0, second.chunksCreated);
        Assert.Equal(first.sourcesCreated, second.duplicatesSkipped);
    }

    [Fact]
    public async Task SeedEducation_AllChunksHaveProvenance()
    {
        var (ctx, _, seed) = CreateServices();
        await seed.SeedEducationBatch7Async();

        var chunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(chunks, c =>
        {
            Assert.NotNull(c.GovernmentSourceId);
            Assert.NotNull(c.SourceUrl);
            Assert.Contains("imi.gov.my", c.SourceUrl!);
            Assert.NotNull(c.Department);
            Assert.Contains("Immigration", c.Department!);
            Assert.Equal("MY", c.CountryCode);
            Assert.NotNull(c.ContentHash);
        });
    }

    [Fact]
    public async Task SeedEducation_NoCrossContaminationWithOtherBatches()
    {
        var (ctx, _, seed) = CreateServices();

        await seed.SeedImmigrationBatch1Async();
        await seed.SeedTaxBatch2Async();
        await seed.SeedDrivingBatch3Async();
        await seed.SeedEmploymentBatch4Async();
        await seed.SeedPerkesoBatch5Async();
        await seed.SeedHealthcareBatch6Async();

        var countsBefore = new Dictionary<string, int>
        {
            ["immigration"] = await ctx.GovernmentKnowledge.Where(k => k.Category == "immigration").CountAsync(),
            ["tax"] = await ctx.GovernmentKnowledge.Where(k => k.Category == "tax").CountAsync(),
            ["driving"] = await ctx.GovernmentKnowledge.Where(k => k.Category == "driving").CountAsync(),
            ["employment"] = await ctx.GovernmentKnowledge.Where(k => k.Category == "employment").CountAsync(),
            ["healthcare"] = await ctx.GovernmentKnowledge.Where(k => k.Category == "healthcare").CountAsync(),
        };

        await seed.SeedEducationBatch7Async();

        Assert.Equal(countsBefore["immigration"],
            await ctx.GovernmentKnowledge.Where(k => k.Category == "immigration").CountAsync());
        Assert.Equal(countsBefore["tax"],
            await ctx.GovernmentKnowledge.Where(k => k.Category == "tax").CountAsync());
        Assert.Equal(countsBefore["driving"],
            await ctx.GovernmentKnowledge.Where(k => k.Category == "driving").CountAsync());
        Assert.Equal(countsBefore["employment"],
            await ctx.GovernmentKnowledge.Where(k => k.Category == "employment").CountAsync());
        Assert.Equal(countsBefore["healthcare"],
            await ctx.GovernmentKnowledge.Where(k => k.Category == "healthcare").CountAsync());

        Assert.True(await ctx.GovernmentKnowledge
            .Where(k => k.Category == "education").CountAsync() > 0);
    }

    [Fact]
    public async Task SeedAllSevenBatches_CorrectCategories()
    {
        var (ctx, _, seed) = CreateServices();

        await seed.SeedImmigrationBatch1Async();
        await seed.SeedTaxBatch2Async();
        await seed.SeedDrivingBatch3Async();
        await seed.SeedEmploymentBatch4Async();
        await seed.SeedPerkesoBatch5Async();
        await seed.SeedHealthcareBatch6Async();
        await seed.SeedEducationBatch7Async();

        var categories = await ctx.GovernmentKnowledge
            .Select(k => k.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        Assert.Contains("driving", categories);
        Assert.Contains("education", categories);
        Assert.Contains("employment", categories);
        Assert.Contains("healthcare", categories);
        Assert.Contains("immigration", categories);
        Assert.Contains("tax", categories);
    }

    [Fact]
    public async Task SeedCustoms_CreatesSourcesAndChunks()
    {
        var (ctx, _, seed) = CreateServices();

        var (sources, chunks, dupes) = await seed.SeedCustomsBatch8Async();

        Assert.True(sources > 0, "Should create customs sources");
        Assert.True(chunks > 0, "Should create customs chunks");
        Assert.Equal(0, dupes);

        var dbChunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(dbChunks, c => Assert.Equal("customs", c.Category));
    }

    [Fact]
    public async Task SeedCustoms_IsIdempotent()
    {
        var (_, _, seed) = CreateServices();

        var first = await seed.SeedCustomsBatch8Async();
        Assert.True(first.sourcesCreated > 0);

        var second = await seed.SeedCustomsBatch8Async();
        Assert.Equal(0, second.sourcesCreated);
        Assert.Equal(0, second.chunksCreated);
        Assert.Equal(first.sourcesCreated, second.duplicatesSkipped);
    }

    [Fact]
    public async Task SeedCustoms_AllChunksHaveProvenance()
    {
        var (ctx, _, seed) = CreateServices();
        await seed.SeedCustomsBatch8Async();

        var chunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(chunks, c =>
        {
            Assert.NotNull(c.GovernmentSourceId);
            Assert.NotNull(c.SourceUrl);
            Assert.Contains("customs.gov.my", c.SourceUrl!);
            Assert.NotNull(c.Department);
            Assert.Contains("Customs", c.Department!);
            Assert.Equal("MY", c.CountryCode);
            Assert.NotNull(c.ContentHash);
        });
    }

    [Fact]
    public async Task SeedCustoms_NoCrossContaminationWithOtherBatches()
    {
        var (ctx, _, seed) = CreateServices();

        await seed.SeedImmigrationBatch1Async();
        await seed.SeedTaxBatch2Async();
        await seed.SeedDrivingBatch3Async();
        await seed.SeedEmploymentBatch4Async();

        var countsBefore = new Dictionary<string, int>
        {
            ["immigration"] = await ctx.GovernmentKnowledge.Where(k => k.Category == "immigration").CountAsync(),
            ["tax"] = await ctx.GovernmentKnowledge.Where(k => k.Category == "tax").CountAsync(),
            ["driving"] = await ctx.GovernmentKnowledge.Where(k => k.Category == "driving").CountAsync(),
            ["employment"] = await ctx.GovernmentKnowledge.Where(k => k.Category == "employment").CountAsync(),
        };

        await seed.SeedCustomsBatch8Async();

        foreach (var cat in countsBefore.Keys)
            Assert.Equal(countsBefore[cat],
                await ctx.GovernmentKnowledge.Where(k => k.Category == cat).CountAsync());

        Assert.True(await ctx.GovernmentKnowledge
            .Where(k => k.Category == "customs").CountAsync() > 0);
    }

    [Fact]
    public async Task SeedAllEightBatches_CorrectCategories()
    {
        var (ctx, _, seed) = CreateServices();

        await seed.SeedImmigrationBatch1Async();
        await seed.SeedTaxBatch2Async();
        await seed.SeedDrivingBatch3Async();
        await seed.SeedEmploymentBatch4Async();
        await seed.SeedPerkesoBatch5Async();
        await seed.SeedHealthcareBatch6Async();
        await seed.SeedEducationBatch7Async();
        await seed.SeedCustomsBatch8Async();

        var categories = await ctx.GovernmentKnowledge
            .Select(k => k.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        Assert.Contains("customs", categories);
        Assert.Contains("driving", categories);
        Assert.Contains("education", categories);
        Assert.Contains("employment", categories);
        Assert.Contains("healthcare", categories);
        Assert.Contains("immigration", categories);
        Assert.Contains("tax", categories);
    }

    [Fact]
    public async Task Seed_ExistingPhase6Source_NotDuplicated()
    {
        var (ctx, ingestion, seed) = CreateServices();

        await ingestion.RegisterSourceAsync(new RegisterSourceDto
        {
            Name = "Employment Pass Salary Policy Effective 1 June 2026",
            Url = "https://esd.imi.gov.my/portal/latest-news/announcement/announcement-266-ep-salary-policy-2026/",
            Department = "Immigration Department of Malaysia",
            CountryCode = "MY",
        });

        var (sources, _, _) = await seed.SeedImmigrationBatch1Async();

        var allSources = await ctx.GovernmentSources.ToListAsync();
        var salaryPolicySources = allSources.Where(s =>
            s.Url?.Contains("announcement-266") == true).ToList();

        Assert.Single(salaryPolicySources);
    }

    // ====== Batch 9: Government Services ======

    [Fact]
    public async Task SeedGovernmentServices_CreatesSourcesAndChunks()
    {
        var (ctx, _, seed) = CreateServices();

        var (sources, chunks, dupes) = await seed.SeedGovernmentServicesBatch9Async();

        Assert.True(sources > 0, "Should create government-services sources");
        Assert.True(chunks > 0, "Should create government-services chunks");
        Assert.Equal(0, dupes);

        var dbChunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(dbChunks, c => Assert.Equal("government-services", c.Category));
    }

    [Fact]
    public async Task SeedGovernmentServices_IsIdempotent()
    {
        var (_, _, seed) = CreateServices();

        var first = await seed.SeedGovernmentServicesBatch9Async();
        Assert.True(first.sourcesCreated > 0);

        var second = await seed.SeedGovernmentServicesBatch9Async();
        Assert.Equal(0, second.sourcesCreated);
        Assert.Equal(0, second.chunksCreated);
        Assert.Equal(first.sourcesCreated, second.duplicatesSkipped);
    }

    [Fact]
    public async Task SeedGovernmentServices_AllChunksHaveProvenance()
    {
        var (ctx, _, seed) = CreateServices();
        await seed.SeedGovernmentServicesBatch9Async();

        var chunks = await ctx.GovernmentKnowledge.ToListAsync();
        Assert.All(chunks, c =>
        {
            Assert.NotNull(c.GovernmentSourceId);
            Assert.NotNull(c.SourceUrl);
            Assert.Contains("jpn.gov.my", c.SourceUrl!);
            Assert.NotNull(c.Department);
            Assert.Contains("National Registration Department", c.Department!);
            Assert.Equal("MY", c.CountryCode);
            Assert.NotNull(c.ContentHash);
        });
    }

    [Fact]
    public async Task SeedGovernmentServices_NoCrossContaminationWithOtherBatches()
    {
        var (ctx, _, seed) = CreateServices();

        await seed.SeedImmigrationBatch1Async();
        await seed.SeedTaxBatch2Async();
        await seed.SeedCustomsBatch8Async();

        var countsBefore = new Dictionary<string, int>
        {
            ["immigration"] = await ctx.GovernmentKnowledge.Where(k => k.Category == "immigration").CountAsync(),
            ["tax"] = await ctx.GovernmentKnowledge.Where(k => k.Category == "tax").CountAsync(),
            ["customs"] = await ctx.GovernmentKnowledge.Where(k => k.Category == "customs").CountAsync(),
        };

        await seed.SeedGovernmentServicesBatch9Async();

        foreach (var cat in countsBefore.Keys)
            Assert.Equal(countsBefore[cat],
                await ctx.GovernmentKnowledge.Where(k => k.Category == cat).CountAsync());

        Assert.True(await ctx.GovernmentKnowledge
            .Where(k => k.Category == "government-services").CountAsync() > 0);
    }

    [Fact]
    public async Task SeedAllNineBatches_CorrectCategories()
    {
        var (ctx, _, seed) = CreateServices();

        await seed.SeedImmigrationBatch1Async();
        await seed.SeedTaxBatch2Async();
        await seed.SeedDrivingBatch3Async();
        await seed.SeedEmploymentBatch4Async();
        await seed.SeedPerkesoBatch5Async();
        await seed.SeedHealthcareBatch6Async();
        await seed.SeedEducationBatch7Async();
        await seed.SeedCustomsBatch8Async();
        await seed.SeedGovernmentServicesBatch9Async();

        var categories = await ctx.GovernmentKnowledge
            .Select(k => k.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        Assert.Contains("customs", categories);
        Assert.Contains("driving", categories);
        Assert.Contains("education", categories);
        Assert.Contains("employment", categories);
        Assert.Contains("government-services", categories);
        Assert.Contains("healthcare", categories);
        Assert.Contains("immigration", categories);
        Assert.Contains("tax", categories);
    }
}
