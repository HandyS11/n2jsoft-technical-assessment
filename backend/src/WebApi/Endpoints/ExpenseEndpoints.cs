using System.ComponentModel.DataAnnotations;
using Application.Exceptions;
using Application.ExpenseReports;
using Application.Expenses;
using Application.Paging;
using Microsoft.AspNetCore.Http.HttpResults;

namespace WebApi.Endpoints;

internal static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var expenses = endpoints.NewVersionedApi("Expenses")
            .MapGroup("/v{version:apiVersion}/expense-reports/{reportId:guid}/expenses")
            .HasApiVersion(1.0);

        expenses.MapGet("/", ListExpensesAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
        expenses.MapGet("/{id:guid}", GetExpenseAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>A 404 for an unknown report, so that an empty page always means a report without expenses.</summary>
    private static async Task<Ok<PagedResult<ExpenseSummary>>> ListExpensesAsync(
        Guid reportId,
        IExpenseReportStore expenseReportStore,
        IExpenseStore expenseStore,
        CancellationToken cancellationToken,
        [Range(1, PageRequest.MaxPage)] int page = 1,
        [Range(1, PageRequest.MaxPageSize)] int pageSize = PageRequest.DefaultPageSize)
    {
        if (!await expenseReportStore.ExistsAsync(reportId, cancellationToken).ConfigureAwait(false))
        {
            throw new NotFoundException("ExpenseReport", reportId);
        }

        var expenses = await expenseStore.ListAsync(reportId, new PageRequest(page, pageSize), cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok(expenses);
    }

    private static async Task<Ok<ExpenseDetails>> GetExpenseAsync(
        Guid reportId,
        Guid id,
        IExpenseStore expenseStore,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var expense = await expenseStore.GetDetailsAsync(reportId, id, cancellationToken).ConfigureAwait(false)
                      ?? throw new NotFoundException("Expense", id);
        response.Headers.ETag = EntityTags.Of(expense.Version);
        return TypedResults.Ok(expense);
    }
}
