using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MediCare.Web.Models;
using MediCare.Data;
using Microsoft.EntityFrameworkCore;

namespace MediCare.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;

    public HomeController(ApplicationDbContext db) => _db = db;
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
    [Authorize(Roles = "Admin")]
    public IActionResult AdminDashboard()
    {
        return View();
    }

    [Authorize(Roles = "Doctor")]
    public IActionResult DoctorDashboard()
    {
        return View();
    }

    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> PatientDashboard(CancellationToken cancellationToken)
    {
        var patientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(patientId)) return Forbid();

        var now = DateTime.Now;
        var appointments = _db.Appointments.AsNoTracking()
            .Where(item => item.PatientId == patientId && item.Status == MediCare.Core.AppointmentStatus.Booked)
            .Include(item => item.Timeslot).ThenInclude(slot => slot.Doctor).ThenInclude(doctor => doctor.Specialization);

        var model = new PatientDashboardViewModel
        {
            UpcomingAppointments = await appointments.Where(item => item.Timeslot.StartsAt >= now)
                .OrderBy(item => item.Timeslot.StartsAt).Take(5).ToListAsync(cancellationToken),
            CompletedAppointments = await appointments.Where(item => item.Timeslot.StartsAt < now)
                .OrderByDescending(item => item.Timeslot.StartsAt).Take(5).ToListAsync(cancellationToken)
        };
        return View(model);
    }

}
