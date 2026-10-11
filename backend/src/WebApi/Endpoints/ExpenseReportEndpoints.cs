using System.ComponentModel.DataAnnotations;
using Application.Exceptions;
using Application.ExpenseReports;
using Application.ExpenseReports.Commands;
using Application.Paging;
using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Endpoints.Requests;

namespace WebApi.Endpoints;

internal static class ExpenseReportEndpoints
{
    public static IEndpointRouteBuilder MapExpenseReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var reports = endpoints.NewVersionedApi("ExpenseReports")
            .MapGroup("/v{version:apiVersion}/expense-reports")
            .HasApiVersion(1.0);

        reports.MapGet("/", ListExpenseReportsAsync)
            .ProducesValidationProblem();
        reports.MapGet("/{id:guid}", GetExpenseReportAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);
        // 404: the user is unknown or deleted. 422: the user is inactive, or already has a report for that month.
        reports.MapPost("/", CreateExpenseReportAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        reports.MapDelete("/{id:guid}", DeleteExpenseReportAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Ok<PagedResult<ExpenseReportSummary>>> ListExpenseReportsAsync(
        IExpenseReportStore expenseReportStore,
        CancellationToken cancellationToken,
        [Range(1, PageRequest.MaxPage)] int page = 1,
        [Range(1, PageRequest.MaxPageSize)] int pageSize = PageRequest.DefaultPageSize)
    {
        var reports = await expenseReportStore.ListAsync(new PageRequest(page, pageSize), cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok(reports);
    }

    private static async Task<Ok<ExpenseReportDetails>> GetExpenseReportAsync(
        Guid id,
        IExpenseReportStore expenseReportStore,
        CancellationToken cancellationToken)
    {
        var report = await expenseReportStore.GetDetailsAsync(id, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("ExpenseReport", id);
        return TypedResults.Ok(report);
    }

    private static async Task<Created<ExpenseReportDetails>> CreateExpenseReportAsync(
        CreateExpenseReportRequest request,
        CreateExpenseReportHandler handler,
        CancellationToken cancellationToken)
    {
        var report = await handler.HandleAsync(request.ToCommand(), cancellationToken).ConfigureAwait(false);
        return TypedResults.Created(new Uri($"/v1/expense-reports/{report.Id}", UriKind.Relative), report);
    }

    private static async Task<NoContent> DeleteExpenseReportAsync(
        Guid id,
        DeleteExpenseReportHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new DeleteExpenseReportCommand(id), cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
