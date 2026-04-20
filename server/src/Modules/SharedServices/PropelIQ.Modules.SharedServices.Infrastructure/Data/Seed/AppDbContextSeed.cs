using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Administration.Domain.Entities;

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

        await context.SaveChangesAsync(ct);
    }
}
