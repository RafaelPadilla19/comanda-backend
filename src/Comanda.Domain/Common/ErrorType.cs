namespace Comanda.Domain.Common;

/// <summary>Categoría de error, mapeada luego a un código HTTP en la API.</summary>
public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
}
