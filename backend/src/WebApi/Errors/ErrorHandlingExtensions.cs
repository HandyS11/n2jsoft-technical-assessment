namespace WebApi.Errors;

internal static class ErrorHandlingExtensions
{
    public static IServiceCollection AddErrorHandling(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddProblemDetails(options =>
        {
            if (environment.IsDevelopment())
            {
                // Unhandled exceptions become a bare 500: show what happened, but only to developers.
                options.CustomizeProblemDetails = static context =>
                    context.ProblemDetails.Detail ??= context.Exception?.ToString();
            }
        });
        services.AddExceptionHandler<ApiExceptionHandler>();

        return services;
    }
}
