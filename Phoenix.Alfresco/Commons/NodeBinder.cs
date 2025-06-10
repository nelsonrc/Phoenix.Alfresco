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


public class NodeVersion
{
    public string Id { get; }
    public string VersionLabel { get; }
    public string ModifiedAt { get; }
    public string? Modifier { get; }

    public NodeVersion(string id, string versionLabel, string modifiedAt, string? modifier = null)
    {
        Id = id;
        VersionLabel = versionLabel;
        ModifiedAt = modifiedAt;
        Modifier = modifier;
    }
}

public static class VersionBinder
{
    public static NodeVersion FromJson(JsonElement version)
    {
        var user = version.TryGetProperty("modifiedByUser", out var userProp)
            ? userProp.GetProperty("displayName").GetString()
            : null;

        return new NodeVersion(
            id: version.GetProperty("id").GetString()!,
            versionLabel: version.GetProperty("versionLabel").GetString() ?? "v?",
            modifiedAt: version.GetProperty("modifiedAt").GetString()!,
            modifier: user
        );
    }
}
