using System.Text.Json;

namespace Phoenix.Alfresco.Commons;

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
