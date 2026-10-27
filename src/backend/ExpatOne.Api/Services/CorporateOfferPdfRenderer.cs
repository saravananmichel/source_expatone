using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ExpatOne.Api.Services;

/// <summary>Renders plain-text HR-reviewed offer drafts into a readable PDF.</summary>
public static class CorporateOfferPdfRenderer
{
    public static byte[] Render(string text) => Render(text, null, null, null);

    /// <summary>Render with an optional company branding snapshot.</summary>
    public static byte[] Render(string text, string? companyName,
        string? companyContactLine, byte[]? logoBytes,
        IReadOnlyList<(string Category, string Name, string? Description)>? benefits = null,
        byte[]? employeeSignature = null, DateTime? signedAt = null,
        string? originalPdfSha256 = null)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 20000)
            throw new ArgumentException("Offer text must contain 1 to 20,000 characters.");
        // QuestPDF Community license eligibility must be confirmed before production use.
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(42);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(header =>
                {
                    if (logoBytes is { Length: > 0 })
                        header.Item().Height(60).AlignLeft().Image(logoBytes).FitArea();
                    if (!string.IsNullOrWhiteSpace(companyName))
                        header.Item().PaddingTop(6).Text(companyName).Bold().FontSize(15);
                    if (!string.IsNullOrWhiteSpace(companyContactLine))
                        header.Item().PaddingTop(3).Text(companyContactLine)
                            .FontSize(8).FontColor(Colors.Grey.Darken2);
                    header.Item().PaddingTop(12).Text(
                        employeeSignature is { Length: > 0 }
                            ? "EMPLOYMENT OFFER — SIGNED ACKNOWLEDGEMENT"
                            : "EMPLOYMENT OFFER")
                        .Bold().FontSize(13);
                });
                page.Content().PaddingVertical(18).Column(column =>
                {
                    if (employeeSignature is { Length: > 0 })
                    {
                        column.Item().Text("I confirm that I reviewed and accept the original issued employment offer identified by the SHA-256 fingerprint below. The original PDF, not this acknowledgement, contains the governing offer terms.").LineHeight(1.45f);
                    }
                    else
                    {
                        column.Item().Text(text).LineHeight(1.45f);
                    }
                    if (employeeSignature is not { Length: > 0 } && benefits is { Count: > 0 })
                    {
                        column.Item().PaddingTop(18).Text("SELECTED EMPLOYEE BENEFITS").Bold();
                        column.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(5);
                            });
                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1).Padding(5).Text("Category").Bold();
                                header.Cell().BorderBottom(1).Padding(5).Text("Benefit").Bold();
                                header.Cell().BorderBottom(1).Padding(5).Text("Terms").Bold();
                            });
                            foreach (var benefit in benefits)
                            {
                                table.Cell().Padding(5).Text(benefit.Category);
                                table.Cell().Padding(5).Text(benefit.Name);
                                table.Cell().Padding(5).Text(benefit.Description ?? "");
                            }
                        });
                    }
                    if (!string.IsNullOrWhiteSpace(originalPdfSha256))
                    {
                        column.Item().PaddingTop(20).Text("ORIGINAL ISSUED DOCUMENT REFERENCE")
                            .Bold().FontSize(9);
                        column.Item().PaddingTop(4).Text(
                            "SHA-256: " + originalPdfSha256).FontSize(8);
                        column.Item().PaddingTop(4).Text(
                            "This acknowledgement refers to the separately preserved original issued PDF. " +
                            "The original document remains available in the employee wallet.").FontSize(8);
                    }
                    column.Item().PaddingTop(28).AlignRight().Width(230).Column(signature =>
                    {
                        signature.Item().Text("EMPLOYEE ACCEPTANCE").Bold().FontSize(10);
                        signature.Item().PaddingTop(8).Text(
                            "I have reviewed this offer and acknowledge the terms presented.");
                        if (employeeSignature is { Length: > 0 })
                            signature.Item().PaddingTop(12).Height(70).Image(employeeSignature).FitArea();
                        else
                            signature.Item().PaddingTop(35).LineHorizontal(1);
                        signature.Item().PaddingTop(5).Text("Employee signature");
                        signature.Item().PaddingTop(12).Text("Name: ________________________");
                        signature.Item().PaddingTop(8).Text(signedAt.HasValue
                            ? "Date (UTC): " + signedAt.Value.ToString("yyyy-MM-dd HH:mm")
                            : "Date: __________________________");
                    });
                });
                page.Footer().AlignCenter().Text(employeeSignature is { Length: > 0 }
                    ? "Generated by ExpatOne • Drawn signature recorded; not a cryptographic digital signature"
                    : "Generated by ExpatOne • Unsigned offer")
                    .FontSize(8).FontColor(Colors.Grey.Darken2);
            });
        }).GeneratePdf();
    }
}
