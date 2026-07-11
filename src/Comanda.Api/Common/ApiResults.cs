using Comanda.Domain.Common;

namespace Comanda.Api.Common;

/// <summary>Envoltura estándar de error (en español) devuelta por la API.</summary>
public sealed record ErrorResponse(string Codigo, string Mensaje);

public static class ApiResults
{
    public static int ToStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    };

    /// <summary>Escribe el error como JSON con el código HTTP correspondiente.</summary>
    public static Task SendErrorAsync(this HttpContext ctx, Error error, CancellationToken ct = default)
    {
        ctx.Response.StatusCode = ToStatusCode(error.Type);
        return ctx.Response.WriteAsJsonAsync(new ErrorResponse(error.Code, error.Message), ct);
    }
}
