namespace Comanda.Domain.Common;

/// <summary>Resultado de una operación: éxito o fallo con un <see cref="Error"/>.</summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Un resultado exitoso no puede tener un error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Un resultado fallido requiere un error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

/// <summary>Resultado con un valor asociado en caso de éxito.</summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error)
        => _value = value;

    /// <summary>Valor del resultado. Solo es válido cuando <see cref="Result.IsSuccess"/> es verdadero.</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede acceder al valor de un resultado fallido.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
}
