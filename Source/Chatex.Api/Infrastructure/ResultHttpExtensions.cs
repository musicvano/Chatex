using Chatex.Business.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Chatex.Api.Infrastructure;

/// <summary>Turns business <see cref="Result"/>s into HTTP results; failures become RFC 9457 problem details.</summary>
internal static class ResultHttpExtensions
{
    /// <summary>The problem details extension that carries <see cref="Error.Code"/>.</summary>
    public const string CodeExtension = "code";

    public static int ToStatusCode(this ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static ProblemHttpResult ToProblem(this Error error)
    {
        ProblemDetails problem = error.Details is { Count: > 0 } details
            ? new HttpValidationProblemDetails(details.ToDictionary()) { Title = "One or more fields are invalid." }
            : new ProblemDetails();

        problem.Status = error.Kind.ToStatusCode();
        problem.Detail = error.Message;
        problem.Extensions[CodeExtension] = error.Code;
        return TypedResults.Problem(problem);
    }

    public static Results<Ok<T>, ProblemHttpResult> ToOk<T>(this Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();

    /// <param name="location">The new resource's URL, or null when it has no URL of its own.</param>
    public static Results<Created<T>, ProblemHttpResult> ToCreated<T>(this Result<T> result, Func<T, string?> location) =>
        result.IsSuccess ? TypedResults.Created(location(result.Value), result.Value) : result.Error.ToProblem();

    public static Results<NoContent, ProblemHttpResult> ToNoContent(this Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
}
