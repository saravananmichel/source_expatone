using System.ComponentModel.DataAnnotations;
namespace ExpatOne.Application.DTOs;

public class VisaDocumentInput
{
    public Guid Id { get; set; }
    [Required, MaxLength(40)] public string Role { get; set; } = "Other supporting document";
}
public class VisaCreateDto
{
    [Required, MaxLength(80)] public string TemplateId { get; set; } = "SG-VISIT-2026-10-12";
    [MaxLength(30)] public List<VisaDocumentInput> Documents { get; set; } = [];
}
public class VisaCheckInput
{
    public bool Confirmed { get; set; }
    public Guid? DocumentId { get; set; }
    [MaxLength(1500)] public string Note { get; set; } = "";
}
public class VisaUpdateDto
{
    public Guid Revision { get; set; }
    public Dictionary<string, string> Values { get; set; } = [];
    public Dictionary<string, VisaCheckInput> Checks { get; set; } = [];
}
public class VisaDocumentsDto
{
    public Guid Revision { get; set; }
    [MaxLength(30)] public List<VisaDocumentInput> Documents { get; set; } = [];
}
public class VisaRevisionDto { public Guid Revision { get; set; } }
