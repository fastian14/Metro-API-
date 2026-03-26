namespace MetroAPI.Models.Common;

/// <summary>
/// Unified envelope returned by every SparsWeb → Metro API endpoint.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data) =>
        new() { Success = true, Data = data };

    public static ApiResponse<T> Fail(string errorCode, string errorMessage) =>
        new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
