using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotShallot.Web.Controllers;

[Authorize(AuthenticationSchemes = "Bearer")]
[Route("api/v1/[controller]")]
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
}
