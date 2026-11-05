using Chatex.Business.Attachments;
using Chatex.Business.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;

namespace Chatex.Api.Infrastructure;

/// <summary>Streams stored files back. Files are private to their readers, and browsers must not guess their type.</summary>
internal static class FileResults
{
    public static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> ToImageAsync(
        this Task<Result<Stream>> pending, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await pending;
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        var (content, contentType) = await ImageSniffer.SniffAsync(result.Value, cancellationToken);
        SetPrivateHeaders(response);
        response.Headers.CacheControl = "private, no-cache";
        return TypedResults.Stream(content, contentType);
    }

    /// <summary>Images open in the browser; everything else downloads under its original name.</summary>
    public static FileStreamHttpResult ToFile(Stream content, AttachmentDownload download, HttpResponse response)
    {
        SetPrivateHeaders(response);
        if (!IsImage(download.ContentType))
        {
            return TypedResults.Stream(content, download.ContentType, download.FileName, enableRangeProcessing: true);
        }

        response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileNameStar = download.FileName }.ToString();
        return TypedResults.Stream(content, download.ContentType, enableRangeProcessing: true);
    }

    private static bool IsImage(string contentType) => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static void SetPrivateHeaders(HttpResponse response)
    {
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.CacheControl = "private, max-age=3600";
    }
}
