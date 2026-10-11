using System.ComponentModel.DataAnnotations;
using Application.Exceptions;
using Application.ExpenseReports;
using Application.Paging;
using Microsoft.AspNetCore.Http.HttpResults;

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
}
