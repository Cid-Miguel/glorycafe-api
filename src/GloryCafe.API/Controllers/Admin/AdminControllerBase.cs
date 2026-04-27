using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace GloryCafe.API.Controllers.Admin;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public abstract class AdminControllerBase : ApiControllerBase
{
}
