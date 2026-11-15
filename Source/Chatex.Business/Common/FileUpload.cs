namespace Chatex.Business.Common;

/// <summary>A file sent by a client. <see cref="Length"/> is what the client claims; the content is still capped while reading.</summary>
public sealed record FileUpload(Stream Content, string FileName, string ContentType, long Length);
