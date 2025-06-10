# Phoenix.Alfresco

**A modular, scalable C# SDK for interacting with Alfresco via its public REST API.**  
Built with clean architecture, streaming support, structured logging, and composable primitives.

---

## ✨ Features

- 🔗 **Streaming + Paged Traversal** — Traverse folders and documents with `IAsyncEnumerable` or paged result sets  
- 🧩 **Modular JSON Binders** — Centralized mapping from raw API responses to typed models (`Node`, `NodeVersion`, etc.)  
- 📡 **Shared Request Pipeline** — Clean abstraction for all HTTP operations (`SendAlfrescoRequestAsync`)  
- 🧠 **Unified Result Handling** — Fluent error propagation via `Result<T>` + logging extensions  
- 📁 **Path & Node ID Support** — Use logical paths or direct node IDs interchangeably  
- 🔎 **Filtering Delegates** — Add custom filters (`Func<Node, bool>`) to traversal operations  
- ❌ **Cancellation Tokens & Robust Error Handling** — Built for reliability and responsiveness

---

## 🚀 Quick Start

```csharp
var client = new AlfrescoClient(...);

await foreach (var node in client.StreamDescendantsAsync(
    parentNodeId: "abc123",
    includeSubFolders: true,
    filter: n => n.IsFile && n.MimeType == "application/pdf",
    ct: cancellationToken))
{
    Console.WriteLine($"📄 {node.Name}");
}
