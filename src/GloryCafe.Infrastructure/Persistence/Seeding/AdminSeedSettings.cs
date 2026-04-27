namespace GloryCafe.Infrastructure.Persistence.Seeding;

public class AdminSeedSettings
{
    public const string SectionName = "AdminSeed";

    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
}
