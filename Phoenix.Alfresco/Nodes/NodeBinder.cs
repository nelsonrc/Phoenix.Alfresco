using System;
using System.Text.Json;

namespace Phoenix.Alfresco.Commons;

public static class NodeBinder
{
    public static Node FromJson(JsonElement node)
    {
        var type = node.GetProperty("nodeType").GetString() ?? "";

        var content = node.TryGetProperty("content", out var contentProp) ? contentProp : default;
        var mimeType = content.ValueKind != JsonValueKind.Undefined &&
                       content.TryGetProperty("mimeType", out var mt)
                           ? mt.GetString()
                           : null;

        var parentId = node.TryGetProperty("parentId", out var pid) ? pid.GetString() : null;

        return new Node(
            Id: node.GetProperty("id").GetString()!,
            Name: node.GetProperty("name").GetString()!,
            NodeType: type,
            IsFolder: node.GetProperty("isFolder").GetBoolean(),
            IsFile: node.GetProperty("isFile").GetBoolean(),
            CreatedAt: node.GetProperty("createdAt").GetString()!,
            MimeType: mimeType,
            ParentId: parentId
        );
    }

    public static bool TryFromJson(JsonElement node, out Node result)
    {
        try
        {
            result = FromJson(node);
            return true;
        }
        catch
        {
            result = default!;
            return false;
        }
    }
}
