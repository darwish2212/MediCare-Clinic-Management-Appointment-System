using MediCare.Services;

namespace MediCare.Web.Models;

public class DoctorScheduleViewModel
{
    public bool HasLinkedProfile { get; set; }
    public List<DoctorAppointmentSummary> Appointments { get; set; } = new();
}
