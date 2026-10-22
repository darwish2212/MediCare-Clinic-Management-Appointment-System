using System.Security.Claims;
using MediCare.Core;
using MediCare.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediCare.Web.Controllers;

[Authorize(Roles = "Patient")]
public class MedicalRecordsController : Controller
{
    private readonly ApplicationDbContext _db;

    public MedicalRecordsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Prescriptions(CancellationToken cancellationToken)
    {
        var patientId = GetPatientId();
        if (patientId is null) return Forbid();
        return View(await _db.PatientPrescriptions.AsNoTracking()
            .Where(record => record.PatientId == patientId)
            .OrderByDescending(record => record.PrescribedAt).ToListAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Allergies(CancellationToken cancellationToken)
    {
        var patientId = GetPatientId();
        if (patientId is null) return Forbid();
        return View(await _db.PatientAllergies.AsNoTracking()
            .Where(record => record.PatientId == patientId)
            .OrderBy(record => record.Substance).ToListAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Diagnoses(CancellationToken cancellationToken)
    {
        var patientId = GetPatientId();
        if (patientId is null) return Forbid();
        return View(await _db.PatientDiagnoses.AsNoTracking()
            .Where(record => record.PatientId == patientId)
            .OrderByDescending(record => record.RecordedAt).ToListAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> LabRecords(CancellationToken cancellationToken)
    {
        var patientId = GetPatientId();
        if (patientId is null) return Forbid();
        var records = await _db.PatientLabRecords.AsNoTracking()
            .Where(record => record.PatientId == patientId)
            .OrderByDescending(record => record.RecordedAt)
            .Select(record => new PatientLabRecord
            {
                Id = record.Id,
                PatientId = record.PatientId,
                Title = record.Title,
                RecordedAt = record.RecordedAt,
                FileName = record.FileName,
                ContentType = record.ContentType
            }).ToListAsync(cancellationToken);
        return View(records);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadLabRecord(int id, CancellationToken cancellationToken)
    {
        var patientId = GetPatientId();
        if (patientId is null) return Forbid();
        var record = await _db.PatientLabRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.PatientId == patientId, cancellationToken);
        if (record is null) return NotFound();

        var contentType = record.ContentType == "application/pdf" ? record.ContentType : "application/octet-stream";
        return File(record.FileContent, contentType, Path.GetFileName(record.FileName));
    }

    private string? GetPatientId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
