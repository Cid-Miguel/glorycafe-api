using GloryCafe.Domain.Common;

namespace GloryCafe.Domain.Entities;

public class AdminUser : BaseEntity
{
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// True for newly seeded admins (so the first interactive login forces
    /// a password change) and after an explicit reset. Cleared by
    /// <c>ChangePasswordCommandHandler</c> on success.
    /// </summary>
    public bool MustChangePassword { get; set; } = true;
}
