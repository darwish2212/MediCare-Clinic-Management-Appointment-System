using MediCare.Core;
using MediCare.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediCare.Services;

public class AdminSchedulingService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    public AdminSchedulingService(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public Task<List<Specialization>> GetSpecializationsAsync(CancellationToken token = default) =>
        _db.Specializations.AsNoTracking().OrderBy(x => x.Name).ToListAsync(token);

    public Task<List<Doctor>> GetDoctorsAsync(CancellationToken token = default) =>
        _db.Doctors.AsNoTracking().Include(x => x.Specialization).OrderBy(x => x.Name).ToListAsync(token);

    public Task<List<Timeslot>> GetTimeslotsAsync(CancellationToken token = default) =>
        _db.Timeslots.AsNoTracking().Include(x => x.Doctor).OrderBy(x => x.StartsAt).ToListAsync(token);

    public Task<List<Appointment>> GetAppointmentsAsync(CancellationToken token = default) =>
        _db.Appointments.AsNoTracking()
            .Include(appointment => appointment.Timeslot)
                .ThenInclude(timeslot => timeslot.Doctor)
                    .ThenInclude(doctor => doctor.Specialization)
            .OrderBy(appointment => appointment.Timeslot.StartsAt)
            .ToListAsync(token);

    public async Task<List<IdentityUser>> GetUnlinkedAccountsAsync(CancellationToken token = default)
    {
        var linkedUserIds = await _db.Doctors
            .Where(doctor => doctor.IdentityUserId != null)
            .Select(doctor => doctor.IdentityUserId!)
            .ToListAsync(token);
        var users = await _db.Users.AsNoTracking().OrderBy(user => user.Email).ToListAsync(token);
        var available = new List<IdentityUser>();

        foreach (var user in users)
        {
            if (linkedUserIds.Contains(user.Id) || await _userManager.IsInRoleAsync(user, "Admin"))
                continue;

            available.Add(user);
        }

        return available;
    }

    public async Task<(bool Linked, string? Error)> LinkDoctorAccountAsync(int doctorId, string userId, CancellationToken token = default)
    {
        var doctor = await _db.Doctors.SingleOrDefaultAsync(item => item.Id == doctorId, token);
        if (doctor is null) return (false, "The doctor profile could not be found.");
        if (!string.IsNullOrEmpty(doctor.IdentityUserId)) return (false, "This doctor already has a linked account.");
        if (await _db.Doctors.AnyAsync(item => item.IdentityUserId == userId, token))
            return (false, "This account is already linked to another doctor.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return (false, "The selected account could not be found.");
        if (await _userManager.IsInRoleAsync(user, "Admin"))
            return (false, "An admin account cannot be linked as a doctor.");

        var addedDoctorRole = !await _userManager.IsInRoleAsync(user, "Doctor");
        if (addedDoctorRole)
        {
            var roleResult = await _userManager.AddToRoleAsync(user, "Doctor");
            if (!roleResult.Succeeded) return (false, string.Join(" ", roleResult.Errors.Select(error => error.Description)));
        }

        doctor.IdentityUserId = user.Id;
        try
        {
            await _db.SaveChangesAsync(token);
            return (true, null);
        }
        catch (DbUpdateException)
        {
            if (addedDoctorRole) await _userManager.RemoveFromRoleAsync(user, "Doctor");
            _db.Entry(doctor).State = EntityState.Detached;
            return (false, "The account could not be linked. Refresh the page and try again.");
        }
    }

    public async Task<Dictionary<int, string>> GetLinkedDoctorEmailsAsync(CancellationToken token = default) =>
        await (from doctor in _db.Doctors.AsNoTracking()
               join user in _db.Users.AsNoTracking() on doctor.IdentityUserId equals user.Id
               select new { doctor.Id, Email = user.Email ?? user.UserName ?? "" })
            .ToDictionaryAsync(item => item.Id, item => item.Email, token);

    public async Task<bool> AddSpecializationAsync(string name, string description, CancellationToken token = default)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name) || await _db.Specializations.AnyAsync(x => x.Name.ToLower() == name.ToLower(), token)) return false;
        _db.Specializations.Add(new Specialization { Name = name, Description = description.Trim() });
        await _db.SaveChangesAsync(token);
        return true;
    }

    public async Task<bool> AddDoctorAsync(string name, int specializationId, CancellationToken token = default)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name) || !await _db.Specializations.AnyAsync(x => x.Id == specializationId, token)) return false;
        _db.Doctors.Add(new Doctor { Name = name, SpecializationId = specializationId });
        await _db.SaveChangesAsync(token);
        return true;
    }

    public async Task<(bool Added, string? Error)> AddTimeslotAsync(int doctorId, DateTime startsAt, DateTime endsAt, CancellationToken token = default)
    {
        if (endsAt <= startsAt) return (false, "The end time must be after the start time.");
        if (startsAt <= DateTime.Now) return (false, "Choose a future time.");
        if (!await _db.Doctors.AnyAsync(x => x.Id == doctorId, token)) return (false, "The selected doctor could not be found.");
        if (await _db.Timeslots.AnyAsync(x => x.DoctorId == doctorId && x.StartsAt < endsAt && x.EndsAt > startsAt, token))
            return (false, "This time overlaps another slot for the selected doctor.");
        _db.Timeslots.Add(new Timeslot { DoctorId = doctorId, StartsAt = startsAt, EndsAt = endsAt });
        await _db.SaveChangesAsync(token);
        return (true, null);
    }
}
