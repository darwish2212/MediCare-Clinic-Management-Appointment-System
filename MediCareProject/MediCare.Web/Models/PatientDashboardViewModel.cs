using MediCare.Core;

namespace MediCare.Web.Models;

public class PatientDashboardViewModel
{
    public List<Appointment> UpcomingAppointments { get; set; } = new();
    public List<Appointment> CompletedAppointments { get; set; } = new();
}
