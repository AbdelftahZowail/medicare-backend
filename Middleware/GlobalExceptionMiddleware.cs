using System.Net;
using System.Text.Json;
using MedicalApp.API.Helpers;

namespace MedicalApp.API.Middleware
{
    /// <summary>
    /// Global exception handling middleware.
    /// Catches all unhandled exceptions and returns a clean JSON response to Flutter.
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var response = exception switch
            {
                UnauthorizedAccessException => new ApiResponse
                {
                    IsSuccess = false,
                    Message = "غير مصرح لك بالوصول",
                    StatusCode = (int)HttpStatusCode.Unauthorized
                },
                KeyNotFoundException => new ApiResponse
                {
                    IsSuccess = false,
                    Message = "العنصر المطلوب غير موجود",
                    StatusCode = (int)HttpStatusCode.NotFound
                },
                ArgumentException argEx => new ApiResponse
                {
                    IsSuccess = false,
                    Message = argEx.Message,
                    StatusCode = (int)HttpStatusCode.BadRequest
                },
                InvalidOperationException invEx => new ApiResponse
                {
                    IsSuccess = false,
                    Message = invEx.Message,
                    StatusCode = (int)HttpStatusCode.Conflict
                },
                _ => new ApiResponse
                {
                    IsSuccess = false,
                    Message = "حدث خطأ غير متوقع في الخادم",
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Errors = new List<string> { exception.Message }
                }
            };

            context.Response.StatusCode = response.StatusCode;

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await context.Response.WriteAsJsonAsync(response, jsonOptions);
        }
    }
}
