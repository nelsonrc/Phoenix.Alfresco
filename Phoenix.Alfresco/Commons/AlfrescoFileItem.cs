namespace Phoenix.Alfresco;

public class AlfrescoFileItem
{
    public string FolderId { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public string MIMEType { get; set; } = string.Empty;
    public bool IsLatestVersion { get; set; } = true;
    public string FileName { get; set; } = string.Empty;
    public string LastMDate { get; set; } = string.Empty;
    public string LastMBy { get; set; } = string.Empty;
}
