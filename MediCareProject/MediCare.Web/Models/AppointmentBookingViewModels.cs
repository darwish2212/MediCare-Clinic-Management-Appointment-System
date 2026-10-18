using System.ComponentModel.DataAnnotations;

namespace MediCare.Web.Models;

public class SelectTimeslotViewModel
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public int SpecializationId { get; set; }
    public string SpecializationName { get; set; } = string.Empty;
    public List<TimeslotOptionViewModel> Timeslots { get; set; } = new();
}

public class TimeslotOptionViewModel
{
    public int Id { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
}

public class BookAppointmentViewModel
{
    [Range(1, int.MaxValue)]
    public int DoctorId { get; set; }

    [Range(1, int.MaxValue)]
    public int TimeslotId { get; set; }
}

public class RescheduleAppointmentViewModel
{
    public int AppointmentId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string SpecializationName { get; set; } = string.Empty;
    public DateTime CurrentStartsAt { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose an available time.")]
    public int NewTimeslotId { get; set; }

    public List<TimeslotOptionViewModel> Timeslots { get; set; } = new();
}
