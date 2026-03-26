namespace MetroAPI.Services;

/// <summary>
/// Thrown by <see cref="MetroService"/> when the Metropolitan carrier API
/// returns a non-2xx response or an unrecoverable error occurs.
/// </summary>
public class MetroApiException : Exception
{
    public string ErrorCode { get; }

    public MetroApiException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
