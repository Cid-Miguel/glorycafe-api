using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GloryCafe.Application.Common.Interfaces;

namespace GloryCafe.API.Infrastructure;

/// <summary>
/// Reads the current admin's id from the JWT <c>sub</c> claim. Lives in
/// the API project because it depends on <see cref="IHttpContextAccessor"/>;
/// the Application layer talks to it through <see cref="ICurrentUserService"/>.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? AdminUserId
    {
        get
        {
            var subClaim = _httpContextAccessor.HttpContext?.User
                .FindFirstValue(JwtRegisteredClaimNames.Sub);
            return int.TryParse(subClaim, out var id) ? id : null;
        }
    }
}
