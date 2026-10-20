using System.Security.Claims;
using MediCare.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediCare.Web.Controllers;

[Authorize(Roles = "Patient,Doctor,Admin")]
[Route("Appointments")]
public class AppointmentCancellationsController : Controller
{
    private readonly AppointmentBookingService _bookingService;

    public AppointmentCancellationsController(AppointmentBookingService bookingService) => _bookingService = bookingService;

    [HttpPost("Cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int appointmentId, string? returnTo, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        var outcome = await _bookingService.CancelAsync(
            appointmentId, userId, User.IsInRole("Admin"), DateTime.Now, cancellationToken);

        TempData["CancellationMessage"] = outcome switch
        {
            AppointmentCancellationOutcome.Cancelled => "The appointment was cancelled and its time is available again.",
            AppointmentCancellationOutcome.NotFound => "The appointment could not be found.",
            AppointmentCancellationOutcome.AlreadyCancelled => "This appointment has already been cancelled.",
            AppointmentCancellationOutcome.NotAuthorized => "You are not allowed to cancel this appointment.",
            AppointmentCancellationOutcome.CancellationWindowClosed => "Appointments can only be cancelled at least 12 hours before they start.",
            _ => "The appointment could not be cancelled. Refresh the page and try again."
        };

        return returnTo switch
        {
            "Admin" when User.IsInRole("Admin") => RedirectToAction("Index", "AdminSchedule"),
            "Doctor" when User.IsInRole("Doctor") => RedirectToAction("Index", "DoctorAppointments"),
            _ => RedirectToAction("MyAppointments", "Appointments")
        };
    }
}
