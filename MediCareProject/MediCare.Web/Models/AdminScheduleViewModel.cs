using System.ComponentModel.DataAnnotations;
using MediCare.Core;

namespace MediCare.Web.Models;

public class AdminScheduleViewModel
{
    [Required, StringLength(100)] public string SpecializationName { get; set; } = string.Empty;
    [StringLength(500)] public string SpecializationDescription { get; set; } = string.Empty;
    [Required, StringLength(120)] public string DoctorName { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int SpecializationId { get; set; }
    [Range(1, int.MaxValue)] public int DoctorId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public List<Specialization> Specializations { get; set; } = new();
    public List<Doctor> Doctors { get; set; } = new();
    public List<Timeslot> Timeslots { get; set; } = new();
    public List<Appointment> Appointments { get; set; } = new();
    public List<DoctorAccountOption> AvailableDoctorAccounts { get; set; } = new();
    public Dictionary<int, string> LinkedDoctorEmails { get; set; } = new();
}

public class DoctorAccountOption
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
