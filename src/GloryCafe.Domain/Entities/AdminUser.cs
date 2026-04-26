using GloryCafe.Domain.Common;

namespace GloryCafe.Domain.Entities;

public class AdminUser : BaseEntity
{
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
}
