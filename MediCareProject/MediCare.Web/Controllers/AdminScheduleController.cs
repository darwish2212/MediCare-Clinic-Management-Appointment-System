using MediCare.Services;
using MediCare.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediCare.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminScheduleController : Controller
{
    private readonly AdminSchedulingService _service;
    public AdminScheduleController(AdminSchedulingService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Index(string? message, CancellationToken token)
    {
        ViewData["Message"] = message;
        return View(new AdminScheduleViewModel
        {
            Specializations = await _service.GetSpecializationsAsync(token),
            Doctors = await _service.GetDoctorsAsync(token),
            Timeslots = await _service.GetTimeslotsAsync(token),
            Appointments = await _service.GetAppointmentsAsync(token),
            AvailableDoctorAccounts = (await _service.GetUnlinkedAccountsAsync(token))
                .Select(user => new DoctorAccountOption { Id = user.Id, Email = user.Email ?? user.UserName ?? "" }).ToList(),
            LinkedDoctorEmails = await _service.GetLinkedDoctorEmailsAsync(token)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSpecialization(AdminScheduleViewModel model, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(model.SpecializationName) || model.SpecializationName.Length > 100 || model.SpecializationDescription.Length > 500)
            return RedirectToAction(nameof(Index), new { message = "Enter a valid specialization name and description." });
        var added = await _service.AddSpecializationAsync(model.SpecializationName, model.SpecializationDescription, token);
        return RedirectToAction(nameof(Index), new { message = added ? "Specialization added." : "That specialization already exists." });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDoctor(AdminScheduleViewModel model, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(model.DoctorName) || model.DoctorName.Length > 120 || model.SpecializationId < 1)
            return RedirectToAction(nameof(Index), new { message = "Enter a doctor name and choose a specialization." });
        var added = await _service.AddDoctorAsync(model.DoctorName, model.SpecializationId, token);
        return RedirectToAction(nameof(Index), new { message = added ? "Doctor added." : "The selected specialization could not be found." });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTimeslot(AdminScheduleViewModel model, CancellationToken token)
    {
        var result = await _service.AddTimeslotAsync(model.DoctorId, model.StartsAt, model.EndsAt, token);
        return RedirectToAction(nameof(Index), new { message = result.Added ? "Time slot added." : result.Error });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkDoctorAccount(int doctorId, string userId, CancellationToken token)
    {
        if (doctorId < 1 || string.IsNullOrWhiteSpace(userId))
            return RedirectToAction(nameof(Index), new { message = "Choose a doctor profile and an account." });

        var result = await _service.LinkDoctorAccountAsync(doctorId, userId, token);
        return RedirectToAction(nameof(Index), new { message = result.Linked ? "Doctor account linked." : result.Error });
    }
}
