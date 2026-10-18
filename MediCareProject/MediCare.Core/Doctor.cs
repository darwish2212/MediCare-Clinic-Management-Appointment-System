using System.ComponentModel.DataAnnotations;

namespace MediCare.Core;

public class Doctor
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    public string? IdentityUserId { get; set; }

    public int SpecializationId { get; set; }
    public Specialization Specialization { get; set; } = null!;

    public ICollection<Timeslot> Timeslots { get; set; } = new List<Timeslot>();
}
