using System.Security.Claims;
using Joseco.DDD.Core.Results;
using Microsoft.AspNetCore.Mvc;

namespace Orders.WebApi.Infraestructure;

internal static class ApiResults
{
    /// <summary>The authenticated user id: the JWT "sub" claim (or its mapped NameIdentifier form).</summary>
    public static bool TryGetUserId(this ClaimsPrincipal user, out Guid userId)
    {
        string? value = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out userId);
    }

    /// <summary>Staff may act on every order (the "isStaff" claim issued by Identity).</summary>
    public static bool IsStaff(this ClaimsPrincipal user) =>
        bool.TryParse(user.FindFirst("isStaff")?.Value, out bool isStaff) && isStaff;

    public static IActionResult ToProblem(this ControllerBase controller, Error error)
    {
        int status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return controller.StatusCode(status, new ProblemDetails
        {
            Status = status,
            Title = error.Code,
            Detail = error.Description
        });
    }
}
