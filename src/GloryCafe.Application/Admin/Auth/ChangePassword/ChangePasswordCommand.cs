using MediatR;

namespace GloryCafe.Application.Admin.Auth.ChangePassword;

public record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword) : IRequest;
