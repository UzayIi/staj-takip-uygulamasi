using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Identity;

namespace Staj360.Infrastructure.Persistence;

/// <summary>
/// Development demo giriş hesapları (kanonik e-postalar). UserId korunarak e-posta güncellenir;
/// parola yalnızca yapılandırmadan (Seed:DemoPassword) alınır.
/// </summary>
public static class DemoLoginAccounts
{
    public const string SuperAdminEmail = "superadmin@gmail.com";
    public const string AdminEmail = "admin@gmail.com";
    public const string ManagerEmail = "yonetici@gmail.com";
    public const string MentorEmail = "danisman@gmail.com";
    public const string InternEmail = "stajyer@gmail.com";

    /// <summary>Geriye dönük alias.</summary>
    public const string MarkerEmail = SuperAdminEmail;

    /// <summary>Parola sıfırlama / tanıma için kanonik demo e-postaları.</summary>
    public static readonly string[] CanonicalEmails =
    [
        SuperAdminEmail,
        AdminEmail,
        ManagerEmail,
        MentorEmail,
        InternEmail
    ];

    public sealed record AccountDef(
        string TargetEmail,
        string Role,
        string FullName,
        string[] LegacyEmails);

    public static readonly AccountDef[] Definitions =
    [
        new(SuperAdminEmail, AppRoles.SuperAdmin, "Selin Karaca",
            ["admin.demo@stajamed.local"]),
        new(AdminEmail, AppRoles.Admin, "Burak Öztürk",
            ["admin.yonetim@stajamed.local"]),
        new(ManagerEmail, AppRoles.Manager, "Merve Acar",
            []),
        new(MentorEmail, AppRoles.Mentor, "Aylin Demirtaş",
            ["mentor.aylin@stajamed.local", "mentor@staj360.local"]),
        new(InternEmail, AppRoles.Intern, "Ayşe Yılmaz",
            ["stajyer.ayse@stajamed.local", "stajyer@staj360.local", "STJ-2026-2001"])
    ];

    public const string CanonicalInternStudentNumber = "STJ-2026-2001";
    private static readonly Regex LegacyDemoStudentNumber = new(@"^DEMO-(\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public sealed class EnsureResult
    {
        public bool Success { get; init; } = true;
        public string? ConflictMessage { get; init; }
        public Dictionary<string, Guid> UserIdsByEmail { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kanonik demo hesapları oluşturur veya legacy e-postadan taşıyarak günceller.
    /// Çakışmada silme yapmaz; ConflictMessage döner.
    /// </summary>
    public static async Task<EnsureResult> EnsureAsync(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        string password,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var result = new EnsureResult();

        foreach (var def in Definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ensured = await EnsureSingleAsync(userManager, def, password, logger, cancellationToken);
            if (!ensured.Success)
            {
                return new EnsureResult
                {
                    Success = false,
                    ConflictMessage = ensured.ConflictMessage
                };
            }

            result.UserIdsByEmail[def.TargetEmail] = ensured.User!.Id;
        }

        await EnsureMinimalGraphAsync(db, result.UserIdsByEmail, logger, cancellationToken);
        await NormalizeLegacyStudentNumbersAsync(db, logger, cancellationToken);
        await SanitizeCanonicalFullNamesAsync(userManager, logger, cancellationToken);
        return result;
    }

    /// <summary>
    /// DEMO-#### öğrenci numaralarını STJ-2026-#### biçimine dönüştürür (idempotent).
    /// Hedef numara doluysa dönüşüm atlanır.
    /// </summary>
    public static async Task NormalizeLegacyStudentNumbersAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var candidates = await db.InternProfiles.IgnoreQueryFilters()
            .Where(p => p.StudentNumber.StartsWith("DEMO-"))
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
            return;

        var taken = new HashSet<string>(
            await db.InternProfiles.IgnoreQueryFilters().Select(p => p.StudentNumber).ToListAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        var changed = 0;
        foreach (var profile in candidates)
        {
            var match = LegacyDemoStudentNumber.Match(profile.StudentNumber);
            if (!match.Success)
                continue;

            var target = $"STJ-2026-{match.Groups[1].Value}";
            if (taken.Contains(target) &&
                !string.Equals(profile.StudentNumber, target, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Öğrenci no dönüşümü atlandı (hedef dolu): {From} -> {To}",
                    profile.StudentNumber, target);
                continue;
            }

            taken.Remove(profile.StudentNumber);
            profile.StudentNumber = target;
            taken.Add(target);
            changed++;
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Legacy DEMO öğrenci numaraları dönüştürüldü: {Count}", changed);
        }
    }

    private static async Task SanitizeCanonicalFullNamesAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var def in Definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userManager.FindByEmailAsync(def.TargetEmail);
            if (user is null)
                continue;

            if (!LooksLikeDemoOrTestName(user.FullName))
                continue;

            user.FullName = def.FullName;
            await userManager.UpdateAsync(user);
            logger.LogInformation("Kanonik hesap görünen adı güncellendi: {Email}", def.TargetEmail);
        }
    }

    private static bool LooksLikeDemoOrTestName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return name.Contains("Demo", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Test User", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Test Intern", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Örnek Kullanıcı", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Sample User", StringComparison.OrdinalIgnoreCase)
               || Regex.IsMatch(name, @"^(Stajyer|Mentor|Danışman|Yönetici|Kullanıcı)\s*\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private sealed class SingleResult
    {
        public bool Success { get; init; } = true;
        public string? ConflictMessage { get; init; }
        public ApplicationUser? User { get; init; }
    }

    private static async Task<SingleResult> EnsureSingleAsync(
        UserManager<ApplicationUser> userManager,
        AccountDef def,
        string password,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var byTarget = await userManager.FindByEmailAsync(def.TargetEmail);
        ApplicationUser? user = byTarget;

        if (user is null)
        {
            foreach (var legacy in def.LegacyEmails)
            {
                // Legacy listesinde e-posta olmayan öğeler (öğrenci no) atlanır.
                if (!legacy.Contains('@', StringComparison.Ordinal))
                    continue;

                var candidate = await userManager.FindByEmailAsync(legacy);
                if (candidate is null)
                    continue;

                // Hedef e-posta başka bir kullanıcıdaysa çakışma.
                var conflict = await userManager.FindByEmailAsync(def.TargetEmail);
                if (conflict is not null && conflict.Id != candidate.Id)
                {
                    var msg =
                        $"Demo e-posta çakışması: '{def.TargetEmail}' zaten başka kullanıcıya ait. " +
                        $"Legacy '{legacy}' (Id={candidate.Id}) taşınamadı. Silme yapılmadı.";
                    logger.LogError("{Message}", msg);
                    return new SingleResult { Success = false, ConflictMessage = msg };
                }

                var emailResult = await userManager.SetEmailAsync(candidate, def.TargetEmail);
                if (!emailResult.Succeeded)
                {
                    var msg = $"SetEmailAsync başarısız ({def.TargetEmail}): {FormatErrors(emailResult)}";
                    logger.LogError("{Message}", msg);
                    return new SingleResult { Success = false, ConflictMessage = msg };
                }

                var nameResult = await userManager.SetUserNameAsync(candidate, def.TargetEmail);
                if (!nameResult.Succeeded)
                {
                    var msg = $"SetUserNameAsync başarısız ({def.TargetEmail}): {FormatErrors(nameResult)}";
                    logger.LogError("{Message}", msg);
                    return new SingleResult { Success = false, ConflictMessage = msg };
                }

                user = await userManager.FindByIdAsync(candidate.Id.ToString());
                logger.LogInformation("Demo hesap e-postası güncellendi (UserId korundu): {Email}", def.TargetEmail);
                break;
            }
        }

        if (user is null)
        {
            // Intern için legacy öğrenci numarasıyla profil üzerinden bul.
            if (def.Role == AppRoles.Intern)
            {
                // handled in graph; create user here if missing
            }

            user = new ApplicationUser
            {
                UserName = def.TargetEmail,
                Email = def.TargetEmail,
                FullName = def.FullName,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            var create = await userManager.CreateAsync(user, password);
            if (!create.Succeeded)
            {
                var msg = $"Demo kullanıcı oluşturulamadı ({def.TargetEmail}): {FormatErrors(create)}";
                logger.LogError("{Message}", msg);
                return new SingleResult { Success = false, ConflictMessage = msg };
            }

            logger.LogInformation("Demo hesap oluşturuldu: {Email}", def.TargetEmail);
        }
        else
        {
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await userManager.UpdateAsync(user);
            }

            if (!user.IsActive)
            {
                user.IsActive = true;
                await userManager.UpdateAsync(user);
            }

            if (!string.Equals(user.Email, def.TargetEmail, StringComparison.OrdinalIgnoreCase))
            {
                var emailResult = await userManager.SetEmailAsync(user, def.TargetEmail);
                if (!emailResult.Succeeded)
                {
                    var msg = $"SetEmailAsync başarısız ({def.TargetEmail}): {FormatErrors(emailResult)}";
                    logger.LogError("{Message}", msg);
                    return new SingleResult { Success = false, ConflictMessage = msg };
                }
            }

            if (!string.Equals(user.UserName, def.TargetEmail, StringComparison.OrdinalIgnoreCase))
            {
                var nameResult = await userManager.SetUserNameAsync(user, def.TargetEmail);
                if (!nameResult.Succeeded)
                {
                    var msg = $"SetUserNameAsync başarısız ({def.TargetEmail}): {FormatErrors(nameResult)}";
                    logger.LogError("{Message}", msg);
                    return new SingleResult { Success = false, ConflictMessage = msg };
                }
            }

            if (!string.Equals(user.FullName, def.FullName, StringComparison.Ordinal))
            {
                user.FullName = def.FullName;
                await userManager.UpdateAsync(user);
            }

            // Mevcut kullanıcının parolasını demo parolasına senkronize et.
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await userManager.ResetPasswordAsync(user, token, password);
            if (!reset.Succeeded)
            {
                var msg = $"Demo parola güncellenemedi ({def.TargetEmail}): {FormatErrors(reset)}";
                logger.LogError("{Message}", msg);
                return new SingleResult { Success = false, ConflictMessage = msg };
            }
        }

        if (!await userManager.IsInRoleAsync(user, def.Role))
            await userManager.AddToRoleAsync(user, def.Role);

        return new SingleResult { Success = true, User = user };
    }

    /// <summary>Yönetici/danışman birim ataması ve stajyer profil+dönem (minimal, idempotent).</summary>
    private static async Task EnsureMinimalGraphAsync(
        AppDbContext db,
        IReadOnlyDictionary<string, Guid> ids,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var branch = await db.OrganizationUnits.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Code == OrganizationSeedCatalog.DefaultBranchCode && !u.IsDeleted, cancellationToken);
        if (branch is null)
        {
            logger.LogWarning("Demo birim grafiği atlandı: varsayılan şube bulunamadı.");
            return;
        }

        var schedule = await db.WorkSchedules.FirstOrDefaultAsync(s => !s.IsDeleted, cancellationToken);
        if (schedule is null)
        {
            logger.LogWarning("Demo birim grafiği atlandı: çalışma programı yok.");
            return;
        }

        if (ids.TryGetValue(ManagerEmail, out var managerId))
        {
            var has = await db.ManagerUnitAssignments.AnyAsync(
                a => a.ManagerUserId == managerId && a.OrganizationUnitId == branch.Id && a.IsActive && !a.IsDeleted,
                cancellationToken);
            if (!has)
            {
                db.ManagerUnitAssignments.Add(new ManagerUnitAssignment
                {
                    ManagerUserId = managerId,
                    OrganizationUnitId = branch.Id,
                    IsActive = true,
                    AssignedAtUtc = DateTime.UtcNow
                });
            }
        }

        if (ids.TryGetValue(MentorEmail, out var mentorId))
        {
            var has = await db.AdvisorUnitAssignments.AnyAsync(
                a => a.AdvisorUserId == mentorId && a.OrganizationUnitId == branch.Id && a.IsActive && !a.IsDeleted,
                cancellationToken);
            if (!has)
            {
                db.AdvisorUnitAssignments.Add(new AdvisorUnitAssignment
                {
                    AdvisorUserId = mentorId,
                    OrganizationUnitId = branch.Id,
                    IsActive = true,
                    AssignedAtUtc = DateTime.UtcNow
                });
            }
        }

        if (ids.TryGetValue(InternEmail, out var internUserId) && ids.TryGetValue(MentorEmail, out mentorId))
        {
            var profile = await db.InternProfiles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.UserId == internUserId, cancellationToken)
                ?? await db.InternProfiles.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.StudentNumber == CanonicalInternStudentNumber
                                              || p.StudentNumber == "DEMO-2001", cancellationToken);

            if (profile is null)
            {
                profile = new InternProfile
                {
                    UserId = internUserId,
                    StudentNumber = CanonicalInternStudentNumber,
                    University = "Dicle Üniversitesi",
                    SchoolDepartment = "Bilgisayar Mühendisliği",
                    ClassLevel = "3",
                    PhoneNumber = "0555 010 2001",
                    Address = "Diyarbakır/Yenişehir — Sentetik test adresi",
                    CurrentOrganizationUnitId = branch.Id,
                    IsActive = true
                };
                db.InternProfiles.Add(profile);
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                if (profile.IsDeleted) profile.IsDeleted = false;
                if (profile.UserId != internUserId) profile.UserId = internUserId;
                if (profile.CurrentOrganizationUnitId == Guid.Empty)
                    profile.CurrentOrganizationUnitId = branch.Id;
                profile.IsActive = true;
                profile.University = "Dicle Üniversitesi";
                profile.SchoolDepartment = "Bilgisayar Mühendisliği";
                profile.ClassLevel = "3";
                profile.PhoneNumber = "0555 010 2001";
                profile.Address = "Diyarbakır/Yenişehir — Sentetik test adresi";
                if (string.IsNullOrWhiteSpace(profile.StudentNumber)
                    || string.Equals(profile.StudentNumber, "DEMO-2001", StringComparison.OrdinalIgnoreCase))
                    profile.StudentNumber = CanonicalInternStudentNumber;
            }

            var assignment = await db.InternUnitAssignments
                .FirstOrDefaultAsync(a => a.InternProfileId == profile.Id && a.IsActive && !a.IsDeleted, cancellationToken);
            if (assignment is null)
            {
                db.InternUnitAssignments.Add(new InternUnitAssignment
                {
                    InternProfileId = profile.Id,
                    OrganizationUnitId = branch.Id,
                    AdvisorUserId = mentorId,
                    StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)),
                    IsActive = true
                });
            }

            var period = await db.InternshipPeriods
                .FirstOrDefaultAsync(p => p.InternProfileId == profile.Id && !p.IsDeleted, cancellationToken);
            if (period is null)
            {
                db.InternshipPeriods.Add(new InternshipPeriod
                {
                    InternProfileId = profile.Id,
                    MentorUserId = mentorId,
                    WorkScheduleId = schedule.Id,
                    StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)),
                    EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)),
                    RequiredWorkDays = 40,
                    Status = InternshipStatus.Active
                });
            }
            else if (period.MentorUserId != mentorId || period.Status != InternshipStatus.Active)
            {
                period.MentorUserId = mentorId;
                if (period.Status != InternshipStatus.Active)
                    period.Status = InternshipStatus.Active;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task<int> ResetPasswordsAsync(
        UserManager<ApplicationUser> userManager,
        string password,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var success = 0;
        foreach (var email in CanonicalEmails)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                logger.LogDebug("Demo parola atlandı (hesap yok): {Email}", email);
                continue;
            }

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, password);
            if (result.Succeeded)
            {
                success++;
            }
            else
            {
                logger.LogWarning(
                    "Demo parola sıfırlanamadı: {Email}. Kodlar: {Codes}",
                    email,
                    string.Join(", ", result.Errors.Select(e => e.Code)));
            }
        }

        return success;
    }

    public static bool IsCanonicalDemoEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && CanonicalEmails.Contains(email, StringComparer.OrdinalIgnoreCase);

    private static string FormatErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => $"{e.Code}:{e.Description}"));
}
