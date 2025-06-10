namespace Phoenix.Alfresco.Commons;

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
