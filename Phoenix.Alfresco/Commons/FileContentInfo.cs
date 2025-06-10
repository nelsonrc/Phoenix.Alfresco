using System;

namespace Phoenix.Alfresco.Commons;

public record FileContentInfo(
    Stream Content,
    string MimeType,
    string? FileName = null,
    long? ContentLength = null
);
