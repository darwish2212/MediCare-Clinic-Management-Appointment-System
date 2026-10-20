using System.Security.Claims;
using MediCare.Services;
using MediCare.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediCare.Web.Controllers;

[Authorize(Roles = "Patient")]
public class AppointmentsController : Controller
{
    private readonly AppointmentBookingService _bookingService;

    public AppointmentsController(AppointmentBookingService bookingService) => _bookingService = bookingService;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await _bookingService.GetSpecializationsAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> MyAppointments(CancellationToken cancellationToken)
    {
        var patientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(patientId)) return Forbid();

        ViewData["AppointmentMessage"] = TempData["AppointmentMessage"] ?? TempData["CancellationMessage"];
        return View(await _bookingService.GetPatientAppointmentsAsync(patientId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Reschedule(int appointmentId, CancellationToken cancellationToken)
    {
        var patientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(patientId)) return Forbid();

        var appointment = await _bookingService.GetPatientAppointmentAsync(appointmentId, patientId, cancellationToken);
        if (appointment is null) return NotFound();
        if (appointment.Status != MediCare.Core.AppointmentStatus.Booked ||
            appointment.Timeslot.StartsAt < DateTime.Now.AddHours(12))
        {
            TempData["AppointmentMessage"] = "Appointments can only be rescheduled at least 12 hours before they start.";
            return RedirectToAction(nameof(MyAppointments));
        }

        var model = new RescheduleAppointmentViewModel
        {
            AppointmentId = appointment.Id,
            DoctorName = appointment.Timeslot.Doctor.Name,
            SpecializationName = appointment.Timeslot.Doctor.Specialization.Name,
            CurrentStartsAt = appointment.Timeslot.StartsAt,
            Timeslots = (await _bookingService.GetAvailableRescheduleTimeslotsAsync(
                    appointment.Id, patientId, DateTime.Now, cancellationToken))
                .Select(slot => new TimeslotOptionViewModel { Id = slot.Id, StartsAt = slot.StartsAt, EndsAt = slot.EndsAt })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(RescheduleAppointmentViewModel model, CancellationToken cancellationToken)
    {
        var patientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(patientId)) return Forbid();

        if (!ModelState.IsValid)
        {
            TempData["AppointmentMessage"] = "Choose a valid available time and try again.";
            return RedirectToAction(nameof(Reschedule), new { appointmentId = model.AppointmentId });
        }

        var outcome = await _bookingService.RescheduleAsync(
            model.AppointmentId, patientId, model.NewTimeslotId, DateTime.Now, cancellationToken);

        TempData["AppointmentMessage"] = outcome switch
        {
            AppointmentRescheduleOutcome.Rescheduled => "Your appointment has been rescheduled.",
            AppointmentRescheduleOutcome.NotFound => "The appointment could not be found.",
            AppointmentRescheduleOutcome.NotAuthorized => "You are not allowed to reschedule this appointment.",
            AppointmentRescheduleOutcome.RescheduleWindowClosed => "Appointments can only be rescheduled at least 12 hours before they start.",
            AppointmentRescheduleOutcome.TimeslotUnavailable => "That time is no longer available. Please choose another time.",
            _ => "The appointment could not be rescheduled. Refresh the page and try again."
        };

        return RedirectToAction(nameof(MyAppointments));
    }

    [HttpGet]
    public async Task<IActionResult> Doctors(int specializationId, CancellationToken cancellationToken)
    {
        var specializations = await _bookingService.GetSpecializationsAsync(cancellationToken);
        var specialization = specializations.SingleOrDefault(item => item.Id == specializationId);
        if (specialization is null) return NotFound();

        ViewData["Specialization"] = specialization;
        return View(await _bookingService.GetDoctorsAsync(specializationId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Timeslots(int doctorId, CancellationToken cancellationToken)
    {
        var doctor = await _bookingService.GetDoctorAsync(doctorId, cancellationToken);
        if (doctor is null) return NotFound();

        var model = new SelectTimeslotViewModel
        {
            DoctorId = doctor.Id,
            DoctorName = doctor.Name,
            SpecializationId = doctor.SpecializationId,
            SpecializationName = doctor.Specialization.Name,
            Timeslots = (await _bookingService.GetAvailableTimeslotsAsync(doctor.Id, cancellationToken))
                .Select(slot => new TimeslotOptionViewModel { Id = slot.Id, StartsAt = slot.StartsAt, EndsAt = slot.EndsAt })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(BookAppointmentViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return RedirectToAction(nameof(Timeslots), new { doctorId = model.DoctorId });

        var patientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(patientId)) return Forbid();

        var booked = await _bookingService.BookAsync(patientId, model.DoctorId, model.TimeslotId, cancellationToken);
        if (!booked)
            TempData["BookingMessage"] = "The selected time is no longer available. Please choose another time.";
        else
            TempData["BookingMessage"] = "Your appointment has been booked.";

        return RedirectToAction(nameof(Timeslots), new { doctorId = model.DoctorId });
    }
}
