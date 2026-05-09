namespace GloryCafe.Application.Common.Interfaces;

/// <summary>
/// Resolves the currently authenticated admin from the request context.
/// Implemented in the API layer where <c>HttpContext</c> is available;
/// the Application layer stays free of ASP.NET Core dependencies.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// The admin user's primary key, or <c>null</c> when the request is
    /// anonymous or the JWT does not carry a usable <c>sub</c> claim.
    /// </summary>
    int? AdminUserId { get; }
}
