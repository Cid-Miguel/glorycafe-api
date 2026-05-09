using MediatR;

namespace GloryCafe.Application.Admin.Auth.Login;

public record LoginCommand(string Email, string Password) : IRequest<LoginResult>;

public record LoginResult(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string DisplayName,
    bool MustChangePassword);
