namespace ElectricitySchedule.Bot.Middleware;

internal class ErrorHandlingMiddleware(Logger<ErrorHandlingMiddleware> logger) : IMiddleware
{
    private readonly Logger<ErrorHandlingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            _logger.LogError("An error occured: {Message}", e.Message);
        }
    }
}