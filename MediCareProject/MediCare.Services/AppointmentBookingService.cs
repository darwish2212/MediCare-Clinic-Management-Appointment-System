using MediCare.Core;
using MediCare.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediCare.Services;

public class AppointmentBookingService
{
    private static readonly TimeSpan CancellationCutoff = TimeSpan.FromHours(12);
    private readonly ApplicationDbContext _db;

    public AppointmentBookingService(ApplicationDbContext db) => _db = db;

    public Task<List<Specialization>> GetSpecializationsAsync(CancellationToken cancellationToken = default) =>
        _db.Specializations.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public Task<List<Doctor>> GetDoctorsAsync(int specializationId, CancellationToken cancellationToken = default) =>
        _db.Doctors.AsNoTracking()
            .Where(doctor => doctor.SpecializationId == specializationId)
            .Include(doctor => doctor.Specialization)
            .OrderBy(doctor => doctor.Name)
            .ToListAsync(cancellationToken);

    public Task<Doctor?> GetDoctorAsync(int doctorId, CancellationToken cancellationToken = default) =>
        _db.Doctors.AsNoTracking()
            .Include(doctor => doctor.Specialization)
            .SingleOrDefaultAsync(doctor => doctor.Id == doctorId, cancellationToken);

    public Task<List<Timeslot>> GetAvailableTimeslotsAsync(int doctorId, CancellationToken cancellationToken = default) =>
        _db.Timeslots.AsNoTracking()
            .Where(slot => slot.DoctorId == doctorId && !slot.IsBooked && slot.StartsAt > DateTime.Now)
            .Include(slot => slot.Doctor)
            .OrderBy(slot => slot.StartsAt)
            .ToListAsync(cancellationToken);

    public Task<List<Appointment>> GetPatientAppointmentsAsync(string patientId, CancellationToken cancellationToken = default) =>
        _db.Appointments.AsNoTracking()
            .Where(appointment => appointment.PatientId == patientId)
            .Include(appointment => appointment.Timeslot)
                .ThenInclude(timeslot => timeslot.Doctor)
                    .ThenInclude(doctor => doctor.Specialization)
            .OrderBy(appointment => appointment.Timeslot.StartsAt)
            .ToListAsync(cancellationToken);

    public Task<Appointment?> GetPatientAppointmentAsync(int appointmentId, string patientId, CancellationToken cancellationToken = default) =>
        _db.Appointments.AsNoTracking()
            .Where(appointment => appointment.Id == appointmentId && appointment.PatientId == patientId)
            .Include(appointment => appointment.Timeslot)
                .ThenInclude(timeslot => timeslot.Doctor)
                    .ThenInclude(doctor => doctor.Specialization)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<List<Timeslot>> GetAvailableRescheduleTimeslotsAsync(int appointmentId, string patientId, DateTime now, CancellationToken cancellationToken = default)
    {
        var appointment = await _db.Appointments.AsNoTracking()
            .Where(item => item.Id == appointmentId && item.PatientId == patientId)
            .Include(item => item.Timeslot)
                .ThenInclude(timeslot => timeslot.Doctor)
            .SingleOrDefaultAsync(cancellationToken);

        if (appointment is null || appointment.Status != AppointmentStatus.Booked ||
            appointment.Timeslot.StartsAt < now.Add(CancellationCutoff))
            return new List<Timeslot>();

        return await _db.Timeslots.AsNoTracking()
            .Where(slot => slot.DoctorId == appointment.Timeslot.DoctorId &&
                !slot.IsBooked && slot.StartsAt > now)
            .Include(slot => slot.Doctor)
            .OrderBy(slot => slot.StartsAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<AppointmentRescheduleOutcome> RescheduleAsync(
        int appointmentId, string patientId, int newTimeslotId, DateTime now,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var appointment = await _db.Appointments
            .Include(item => item.Timeslot)
                .ThenInclude(timeslot => timeslot.Doctor)
            .SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);

        if (appointment is null) return AppointmentRescheduleOutcome.NotFound;
        if (appointment.PatientId != patientId) return AppointmentRescheduleOutcome.NotAuthorized;
        if (appointment.Status != AppointmentStatus.Booked || appointment.Timeslot.StartsAt < now.Add(CancellationCutoff))
            return AppointmentRescheduleOutcome.RescheduleWindowClosed;

        var newTimeslot = await _db.Timeslots.SingleOrDefaultAsync(
            slot => slot.Id == newTimeslotId && slot.DoctorId == appointment.Timeslot.DoctorId,
            cancellationToken);

        if (newTimeslot is null || newTimeslot.IsBooked || newTimeslot.StartsAt <= now)
            return AppointmentRescheduleOutcome.TimeslotUnavailable;

        appointment.Timeslot.IsBooked = false;
        newTimeslot.IsBooked = true;
        appointment.TimeslotId = newTimeslot.Id;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AppointmentRescheduleOutcome.Rescheduled;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AppointmentRescheduleOutcome.Conflict;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AppointmentRescheduleOutcome.Conflict;
        }
    }

    public async Task<List<DoctorAppointmentSummary>?> GetDoctorAppointmentsAsync(string doctorUserId, CancellationToken cancellationToken = default)
    {
        var doctorExists = await _db.Doctors.AnyAsync(doctor => doctor.IdentityUserId == doctorUserId, cancellationToken);
        if (!doctorExists) return null;

        return await (from appointment in _db.Appointments.AsNoTracking()
                      join patient in _db.Users.AsNoTracking() on appointment.PatientId equals patient.Id
                      where appointment.Timeslot.Doctor.IdentityUserId == doctorUserId
                      orderby appointment.Timeslot.StartsAt
                      select new DoctorAppointmentSummary
                      {
                          AppointmentId = appointment.Id,
                          PatientEmail = patient.Email ?? "Patient",
                          StartsAt = appointment.Timeslot.StartsAt,
                          EndsAt = appointment.Timeslot.EndsAt,
                          SpecializationName = appointment.Timeslot.Doctor.Specialization.Name,
                          Status = appointment.Status,
                          CancelledAt = appointment.CancelledAt
                      }).ToListAsync(cancellationToken);
    }

    public async Task<AppointmentCancellationOutcome> CancelAsync(
        int appointmentId, string actorUserId, bool isAdmin, DateTime now,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var appointment = await _db.Appointments
            .Include(item => item.Timeslot)
                .ThenInclude(timeslot => timeslot.Doctor)
            .SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);

        if (appointment is null) return AppointmentCancellationOutcome.NotFound;

        var ownsAppointment = appointment.PatientId == actorUserId
            || appointment.Timeslot.Doctor.IdentityUserId == actorUserId;
        if (!isAdmin && !ownsAppointment) return AppointmentCancellationOutcome.NotAuthorized;
        if (appointment.Status != AppointmentStatus.Booked) return AppointmentCancellationOutcome.AlreadyCancelled;
        if (!isAdmin && appointment.Timeslot.StartsAt < now.Add(CancellationCutoff))
            return AppointmentCancellationOutcome.CancellationWindowClosed;

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAt = now;
        appointment.Timeslot.IsBooked = false;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AppointmentCancellationOutcome.Cancelled;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AppointmentCancellationOutcome.Conflict;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AppointmentCancellationOutcome.Conflict;
        }
    }

    public async Task<bool> BookAsync(string patientId, int doctorId, int timeslotId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var slot = await _db.Timeslots
            .SingleOrDefaultAsync(item => item.Id == timeslotId && item.DoctorId == doctorId, cancellationToken);

        if (slot is null || slot.IsBooked || slot.StartsAt <= DateTime.Now)
            return false;

        slot.IsBooked = true;
        _db.Appointments.Add(new Appointment
        {
            PatientId = patientId,
            TimeslotId = slot.Id,
            CreatedAt = DateTime.Now
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
    }
}

public class DoctorAppointmentSummary
{
    public int AppointmentId { get; set; }
    public string PatientEmail { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string SpecializationName { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; }
    public DateTime? CancelledAt { get; set; }
}

public enum AppointmentCancellationOutcome
{
    Cancelled,
    NotFound,
    AlreadyCancelled,
    NotAuthorized,
    CancellationWindowClosed,
    Conflict
}

public enum AppointmentRescheduleOutcome
{
    Rescheduled,
    NotFound,
    NotAuthorized,
    RescheduleWindowClosed,
    TimeslotUnavailable,
    Conflict
}
