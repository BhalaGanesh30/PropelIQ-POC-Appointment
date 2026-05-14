using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Enums;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Seed;

/// <summary>
/// Idempotent seed data for the PropelIQ platform.
/// Seeds reference data (admin user) and mock patient records for development.
/// Checks for existence before inserting to support repeated database update runs.
/// </summary>
public static class AppDbContextSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken ct = default)
    {
        if (!await context.Users.AnyAsync(u => u.Email == "admin@propeliq.local", ct))
        {
            context.Users.Add(new User
            {
                Email = "admin@propeliq.local",
                PasswordHash = "CHANGE_ME_BEFORE_PRODUCTION",
                Role = "Admin",
                FirstName = "System",
                LastName = "Admin"
            });
        }

        if (!await context.Patients.AnyAsync(p => p.MRN == "MOCK-001", ct))
        {
            var mockUser = await context.Users
                .FirstOrDefaultAsync(u => u.Email == "mock.patient@propeliq.local", ct);

            if (mockUser is null)
            {
                mockUser = new User
                {
                    Email = "mock.patient@propeliq.local",
                    PasswordHash = "CHANGE_ME_BEFORE_PRODUCTION",
                    Role = "Patient",
                    FirstName = "Mock",
                    LastName = "Patient"
                };
                context.Users.Add(mockUser);
                await context.SaveChangesAsync(ct);
            }

            context.Patients.Add(new Patient
            {
                UserId = mockUser.Id,
                FirstName = "Mock",
                LastName = "Patient",
                DateOfBirth = new DateOnly(1985, 6, 15),
                MRN = "MOCK-001",
                ContactPreferences = new ContactPreferences
                {
                    SmsEnabled = true,
                    EmailEnabled = true,
                    PreferredLanguage = "en"
                }
            });
        }

        // Seed a small set of future slots for local/demo environments so
        // slot-search pages do not render an empty state on first boot.
        var hasFutureSlots = await context.AppointmentSlots
            .AnyAsync(s => s.StartTime > DateTimeOffset.UtcNow, ct);

        if (!hasFutureSlots)
        {
            var baseDay = DateTimeOffset.UtcNow.Date.AddDays(1);
            var seedSlots = new List<AppointmentSlot>();

            // Day 1: General 30-minute slots (3)
            seedSlots.Add(new AppointmentSlot
            {
                StartTime = new DateTimeOffset(baseDay.AddHours(9), TimeSpan.Zero),
                EndTime = new DateTimeOffset(baseDay.AddHours(9).AddMinutes(30), TimeSpan.Zero),
                Duration = SlotDuration.Thirty,
                Type = AppointmentType.General,
                MaxCapacity = 1,
                CurrentBookings = 0,
                ProviderName = "Dr. Seed A",
                Location = "Main Clinic - Room 101"
            });
            seedSlots.Add(new AppointmentSlot
            {
                StartTime = new DateTimeOffset(baseDay.AddHours(10), TimeSpan.Zero),
                EndTime = new DateTimeOffset(baseDay.AddHours(10).AddMinutes(30), TimeSpan.Zero),
                Duration = SlotDuration.Thirty,
                Type = AppointmentType.General,
                MaxCapacity = 1,
                CurrentBookings = 0,
                ProviderName = "Dr. Seed A",
                Location = "Main Clinic - Room 101"
            });
            seedSlots.Add(new AppointmentSlot
            {
                StartTime = new DateTimeOffset(baseDay.AddHours(11), TimeSpan.Zero),
                EndTime = new DateTimeOffset(baseDay.AddHours(11).AddMinutes(30), TimeSpan.Zero),
                Duration = SlotDuration.Thirty,
                Type = AppointmentType.General,
                MaxCapacity = 1,
                CurrentBookings = 0,
                ProviderName = "Dr. Seed A",
                Location = "Main Clinic - Room 101"
            });

            // Day 2: Specialist 60-minute slots (2)
            var day2 = baseDay.AddDays(1);
            seedSlots.Add(new AppointmentSlot
            {
                StartTime = new DateTimeOffset(day2.AddHours(9), TimeSpan.Zero),
                EndTime = new DateTimeOffset(day2.AddHours(10), TimeSpan.Zero),
                Duration = SlotDuration.Sixty,
                Type = AppointmentType.Specialist,
                MaxCapacity = 1,
                CurrentBookings = 0,
                ProviderName = "Dr. Seed B",
                Location = "Specialty Wing - Room 204"
            });
            seedSlots.Add(new AppointmentSlot
            {
                StartTime = new DateTimeOffset(day2.AddHours(14), TimeSpan.Zero),
                EndTime = new DateTimeOffset(day2.AddHours(15), TimeSpan.Zero),
                Duration = SlotDuration.Sixty,
                Type = AppointmentType.Specialist,
                MaxCapacity = 1,
                CurrentBookings = 0,
                ProviderName = "Dr. Seed B",
                Location = "Specialty Wing - Room 204"
            });

            // Day 3: Follow-up 15-minute slots (3)
            var day3 = baseDay.AddDays(2);
            seedSlots.Add(new AppointmentSlot
            {
                StartTime = new DateTimeOffset(day3.AddHours(8), TimeSpan.Zero),
                EndTime = new DateTimeOffset(day3.AddHours(8).AddMinutes(15), TimeSpan.Zero),
                Duration = SlotDuration.Fifteen,
                Type = AppointmentType.FollowUp,
                MaxCapacity = 1,
                CurrentBookings = 0,
                ProviderName = "Dr. Seed C",
                Location = "Annex - Room 12"
            });
            seedSlots.Add(new AppointmentSlot
            {
                StartTime = new DateTimeOffset(day3.AddHours(8).AddMinutes(30), TimeSpan.Zero),
                EndTime = new DateTimeOffset(day3.AddHours(8).AddMinutes(45), TimeSpan.Zero),
                Duration = SlotDuration.Fifteen,
                Type = AppointmentType.FollowUp,
                MaxCapacity = 1,
                CurrentBookings = 0,
                ProviderName = "Dr. Seed C",
                Location = "Annex - Room 12"
            });
            seedSlots.Add(new AppointmentSlot
            {
                StartTime = new DateTimeOffset(day3.AddHours(9), TimeSpan.Zero),
                EndTime = new DateTimeOffset(day3.AddHours(9).AddMinutes(15), TimeSpan.Zero),
                Duration = SlotDuration.Fifteen,
                Type = AppointmentType.FollowUp,
                MaxCapacity = 1,
                CurrentBookings = 0,
                ProviderName = "Dr. Seed C",
                Location = "Annex - Room 12"
            });

            context.AppointmentSlots.AddRange(seedSlots);
        }

        await context.SaveChangesAsync(ct);
    }
}
