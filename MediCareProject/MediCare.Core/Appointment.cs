namespace MediCare.Core;

public enum AppointmentStatus
{
    Booked = 0,
    Cancelled = 1
}

public class Appointment
{
    public int Id { get; set; }

    public string PatientId { get; set; } = string.Empty;
    public int TimeslotId { get; set; }
    public Timeslot Timeslot { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;
    public DateTime? CancelledAt { get; set; }
}
