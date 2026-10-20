using System.Security.Claims;
using MediCare.Services;
using MediCare.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediCare.Web.Controllers;

[Authorize(Roles = "Doctor")]
public class DoctorAppointmentsController : Controller
{
    private readonly AppointmentBookingService _bookingService;
    public DoctorAppointmentsController(AppointmentBookingService bookingService) => _bookingService = bookingService;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["CancellationMessage"] = TempData["CancellationMessage"];
        var doctorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(doctorUserId)) return Forbid();

        var appointments = await _bookingService.GetDoctorAppointmentsAsync(doctorUserId, cancellationToken);
        return View(new DoctorScheduleViewModel
        {
            HasLinkedProfile = appointments is not null,
            Appointments = appointments ?? new List<DoctorAppointmentSummary>()
        });
    }
}
