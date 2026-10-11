using System.ComponentModel.DataAnnotations;
using Application.Exceptions;
using Application.ExpenseReports;
using Application.Expenses;
using Application.Expenses.Commands;
using Application.Paging;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using WebApi.Endpoints.Requests;

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
        // 422: the monthly quota is reached, the date is outside the report's month, or the report's user is deleted.
        expenses.MapPost("/", CreateExpenseAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        // 409: another request saved the expense while this one was changing it. 412: the expense changed since the
        // version given in If-Match.
        expenses.MapPut("/{id:guid}", UpdateExpenseAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

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

    private static async Task<Created<ExpenseDetails>> CreateExpenseAsync(
        Guid reportId,
        SaveExpenseRequest request,
        CreateExpenseHandler handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        // Validation has run: the date and the amount are present.
        var expense = await handler.HandleAsync(
                new CreateExpenseCommand(
                    reportId,
                    request.Date!.Value,
                    request.Description,
                    request.Amount!.Value,
                    request.BillingAddress.ToBillingAddress()),
                cancellationToken)
            .ConfigureAwait(false);
        response.Headers.ETag = EntityTags.Of(expense.Version);
        return TypedResults.Created(
            new Uri($"/v1/expense-reports/{reportId}/expenses/{expense.Id}", UriKind.Relative), expense);
    }

    /// <summary>
    /// A full replacement. With If-Match set to the expense's version (its ETag), the update is refused with 412 when
    /// the expense changed since it was read; without it, the update overwrites whatever is stored.
    /// </summary>
    private static async Task<NoContent> UpdateExpenseAsync(
        Guid reportId,
        Guid id,
        SaveExpenseRequest request,
        UpdateExpenseHandler handler,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(
                new UpdateExpenseCommand(
                    reportId,
                    id,
                    request.Date!.Value,
                    request.Description,
                    request.Amount!.Value,
                    request.BillingAddress.ToBillingAddress(),
                    EntityTags.ExpectedVersionOf(ifMatch)),
                cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
