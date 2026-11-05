using System.Security.Claims;
using Chatex.Api.Infrastructure;
using Chatex.Business.Attachments;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Chatex.Api.Endpoints;

internal static class AttachmentEndpoints
{
    public static void MapAttachmentEndpoints(this IEndpointRouteBuilder routes)
    {
        var maxBytes = routes.ServiceProvider.GetRequiredService<IOptions<AttachmentOptions>>().Value.MaxBytes;
        var group = routes.MapGroup("/attachments").WithTags("Attachments");

        group.MapPost("", UploadAsync).WithName("UploadAttachment").AcceptsFileUpTo(maxBytes)
            .WithSummary("Stores a file; pass its id in attachmentIds when sending the message.");
        group.MapGet("/{id:guid}", DownloadAsync).WithName("DownloadAttachment")
            .WithSummary("For the uploader and anyone who can see the chat the file was sent to.");
    }

    private static async Task<Results<Created<AttachmentDto>, ProblemHttpResult>> UploadAsync(
        IFormFile file, ClaimsPrincipal user, IAttachmentService attachments, CancellationToken cancellationToken) =>
        (await attachments.UploadAsync(user.GetUserId(), file.ToFileUpload(), cancellationToken))
            .ToCreated(a => $"/api/attachments/{a.Id}");

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> DownloadAsync(
        Guid id, ClaimsPrincipal user, IAttachmentService attachments, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await attachments.GetDownloadAsync(user.GetUserId(), id, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        var content = await attachments.OpenReadAsync(result.Value, cancellationToken);
        return FileResults.ToFile(content, result.Value, response);
    }
}
