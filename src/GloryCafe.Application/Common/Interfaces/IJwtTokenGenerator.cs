using GloryCafe.Domain.Entities;

namespace GloryCafe.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    JwtToken Generate(AdminUser user);
}

public record JwtToken(string AccessToken, DateTime ExpiresAtUtc);
