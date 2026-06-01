namespace MedicalApp.API.Helpers
{
    /// <summary>
    /// Standard API response wrapper for all endpoints.
    /// This ensures Flutter always receives a consistent response shape.
    /// </summary>
    /// <typeparam name="T">Type of the data payload.</typeparam>
    public class ApiResponse<T>
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
        public int StatusCode { get; set; }

        /// <summary>
        /// Creates a success response.
        /// </summary>
        public static ApiResponse<T> Success(T data, string message = "تمت العملية بنجاح", int statusCode = 200)
        {
            return new ApiResponse<T>
            {
                IsSuccess = true,
                Message = message,
                Data = data,
                StatusCode = statusCode
            };
        }

        /// <summary>
        /// Creates a failure response.
        /// </summary>
        public static ApiResponse<T> Failure(string message, int statusCode = 400, List<string>? errors = null)
        {
            return new ApiResponse<T>
            {
                IsSuccess = false,
                Message = message,
                StatusCode = statusCode,
                Errors = errors
            };
        }
    }

    /// <summary>
    /// Non-generic version for responses without data.
    /// </summary>
    public class ApiResponse : ApiResponse<object>
    {
        public static ApiResponse Success(string message = "تمت العملية بنجاح", int statusCode = 200)
        {
            return new ApiResponse
            {
                IsSuccess = true,
                Message = message,
                StatusCode = statusCode
            };
        }

        public new static ApiResponse Failure(string message, int statusCode = 400, List<string>? errors = null)
        {
            return new ApiResponse
            {
                IsSuccess = false,
                Message = message,
                StatusCode = statusCode,
                Errors = errors
            };
        }
    }
}
