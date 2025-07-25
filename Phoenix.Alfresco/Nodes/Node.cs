namespace Phoenix.Alfresco;

public record Node(
    string Id,
    string Name,
    string NodeType,
    bool IsFolder,
    bool IsFile,
    string CreatedAt,
    string? MimeType = null,
    string? ParentId = null
);

/*

Subject: Phoenix.CMIS v1 – Alfresco Integration Milestone Achieved 🚀

Body:

Hey team,

Just wrapping up version 1 of the Phoenix.CMIS Alfresco integration—and I wanted to quickly highlight what we've accomplished in this release.

✅ Key Highlights:

Fully modular AlfrescoClient with support for authentication, browsing, upload, and folder management

Streaming and paged traversal support (StreamDescendantsAsync, ListChildrenPageAsync) with optional filtering delegates

Centralized request pipeline abstraction (SendAlfrescoRequestAsync) with structured logging and robust error handling

Unified result composition via Result<T> extension methods (WithLogging, AppendContext, FailFromResponseAsync)

JSON model binding separated via modular binders (NodeBinder, VersionBinder)

Cancellation and error propagation support across all recursive and I/O-heavy methods

Clean architecture focused on reusability, maintainability, and observability

We’ve drawn a clear line between core functionality and future extensibility. Some advanced features—like parallel traversal, hierarchy projections, and diagnostics overlays—are reserved for v2 to keep this release lean and strategic.

Thanks for everyone’s input and collaboration so far. v1 is fast, modular, and ready for integration. Let me know if you'd like me to walk you through the structure or share API usage patterns.

On to the next sprint 💪 —Nelson

*/
