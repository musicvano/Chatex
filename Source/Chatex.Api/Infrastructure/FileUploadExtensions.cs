using Chatex.Business.Common;
using Microsoft.AspNetCore.Mvc;

namespace Chatex.Api.Infrastructure;

internal static class FileUploadExtensions
{
    /// <summary>Extra room for the multipart framing around the file itself.</summary>
    private const long MultipartOverheadBytes = 64 * 1024;

    public static FileUpload ToFileUpload(this IFormFile file) =>
        new(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);

    /// <summary>
    /// Caps the request body a little above <paramref name="maxFileBytes"/>, so huge uploads are cut off early;
    /// the business layer still enforces the exact limit and answers with a validation error.
    /// Antiforgery is off because these endpoints authenticate with a bearer token, not a cookie.
    /// </summary>
    public static RouteHandlerBuilder AcceptsFileUpTo(this RouteHandlerBuilder builder, long maxFileBytes)
    {
        var limit = maxFileBytes + MultipartOverheadBytes;
        return builder
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(limit))
            .WithFormOptions(multipartBodyLengthLimit: limit);
    }
}
