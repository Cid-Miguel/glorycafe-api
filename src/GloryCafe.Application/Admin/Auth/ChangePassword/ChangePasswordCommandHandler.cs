using GloryCafe.Application.Common.Exceptions;
using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Application.Admin.Auth.ChangePassword;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ICurrentUserService _currentUser;

    public ChangePasswordCommandHandler(
        IApplicationDbContext db,
        IPasswordHasher hasher,
        ICurrentUserService currentUser)
    {
        _db = db;
        _hasher = hasher;
        _currentUser = currentUser;
    }

    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var adminId = _currentUser.AdminUserId
            ?? throw new InvalidCredentialsException();

        var admin = await _db.AdminUsers
            .FirstOrDefaultAsync(u => u.Id == adminId, cancellationToken)
            ?? throw new NotFoundException(nameof(AdminUser), adminId);

        // Re-verify the current password even though the user is already
        // authenticated: if a token leaks, the attacker still can't
        // hijack the account without the current password.
        if (!_hasher.Verify(request.CurrentPassword, admin.PasswordHash))
            throw new InvalidCredentialsException();

        admin.PasswordHash = _hasher.Hash(request.NewPassword);
        admin.MustChangePassword = false;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
