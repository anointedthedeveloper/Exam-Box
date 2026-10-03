namespace ExamBox.Services;

public record OpResult(bool Ok, string? Error = null)
{
    public static OpResult Success() => new(true);
    public static OpResult Fail(string error) => new(false, error);
}

public record OpResult<T>(bool Ok, T? Value = default, string? Error = null)
{
    public static OpResult<T> Success(T value) => new(true, value);
    public static OpResult<T> Fail(string error) => new(false, default, error);
}
