using System.ComponentModel.DataAnnotations;

namespace MediCare.Core;

public class PatientPrescription
{
    public int Id { get; set; }
    [Required, MaxLength(450)] public string PatientId { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string MedicationName { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Instructions { get; set; }
    [Required, MaxLength(120)] public string PrescribedBy { get; set; } = string.Empty;
    public DateTime PrescribedAt { get; set; }
}

public class PatientAllergy
{
    public int Id { get; set; }
    [Required, MaxLength(450)] public string PatientId { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string Substance { get; set; } = string.Empty;
    [MaxLength(500)] public string? RecordedReaction { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class PatientDiagnosis
{
    public int Id { get; set; }
    [Required, MaxLength(450)] public string PatientId { get; set; } = string.Empty;
    [Required, MaxLength(240)] public string Description { get; set; } = string.Empty;
    [Required, MaxLength(120)] public string RecordedBy { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}

public class PatientLabRecord
{
    public int Id { get; set; }
    [Required, MaxLength(450)] public string PatientId { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
    [Required, MaxLength(255)] public string FileName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string ContentType { get; set; } = "application/pdf";
    [Required] public byte[] FileContent { get; set; } = Array.Empty<byte>();
}
