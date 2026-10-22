using MediCare.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MediCare.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // إضافة الجدول لقاعدة البيانات
        public DbSet<Specialization> Specializations { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Timeslot> Timeslots { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<PatientPrescription> PatientPrescriptions { get; set; }
        public DbSet<PatientAllergy> PatientAllergies { get; set; }
        public DbSet<PatientDiagnosis> PatientDiagnoses { get; set; }
        public DbSet<PatientLabRecord> PatientLabRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Doctor>()
                .HasOne(doctor => doctor.Specialization)
                .WithMany()
                .HasForeignKey(doctor => doctor.SpecializationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Doctor>()
                .Property(doctor => doctor.IdentityUserId)
                .HasMaxLength(450);

            builder.Entity<Doctor>()
                .HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(doctor => doctor.IdentityUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Doctor>()
                .HasIndex(doctor => doctor.IdentityUserId)
                .IsUnique()
                .HasFilter("[IdentityUserId] IS NOT NULL");

            builder.Entity<Timeslot>()
                .HasOne(timeslot => timeslot.Doctor)
                .WithMany(doctor => doctor.Timeslots)
                .HasForeignKey(timeslot => timeslot.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Timeslot>()
                .HasIndex(timeslot => new { timeslot.DoctorId, timeslot.StartsAt })
                .IsUnique();

            builder.Entity<Appointment>()
                .HasOne(appointment => appointment.Timeslot)
                .WithOne()
                .HasForeignKey<Appointment>(appointment => appointment.TimeslotId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Appointment>()
                .HasIndex(appointment => appointment.TimeslotId)
                .IsUnique()
                .HasFilter("[Status] = 0");

            builder.Entity<PatientPrescription>().HasIndex(record => new { record.PatientId, record.PrescribedAt });
            builder.Entity<PatientAllergy>().HasIndex(record => new { record.PatientId, record.RecordedAt });
            builder.Entity<PatientDiagnosis>().HasIndex(record => new { record.PatientId, record.RecordedAt });
            builder.Entity<PatientLabRecord>().HasIndex(record => new { record.PatientId, record.RecordedAt });
        }
    }
}
