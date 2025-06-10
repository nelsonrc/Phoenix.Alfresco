namespace Phoenix.Alfresco;

using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Phoenix.Alfresco.Commons;

public class AlfrescoClient
{
    public int CachedMinutes => 3;
    public int CachedSeconds => 60;

    #region Fields
    private readonly HttpClient _client;

    private readonly string _baseUrl;
    private readonly string _username;
    private readonly string _password;
    //private string _authToken = string.Empty;
    private TicketEntry _ticketEntry = new TicketEntry(new Ticket(string.Empty, string.Empty));
    #endregion

    private readonly ILogger<AlfrescoClient> _logger;

    private readonly ICacheProvider _appCache;

    public string TenantId => "-default-";

    public AlfrescoClient(string baseUrl, string username, string password, ILogger<AlfrescoClient>? logger, ICacheProvider? appCache)
    {
        _client = new HttpClient();
        _baseUrl = baseUrl;
        _username = username;
        _password = password;
        _logger = logger ?? null!;
        _appCache = appCache ?? null!;
    }

    #region Helper Methods

    private IDisposable BeginLogScope(string scopeName, object values)
    {
        return _logger.BeginScope("{@ScopeName}:{@ScopeValues}", scopeName, values)!;
    }

    private void LogWithContext<T>(
        LogLevel level,
        string eventName,
        T context,
        [CallerMemberName] string caller = "")
    {
        _logger.Log(level, "{EventName} {@Context} [Caller={Caller}]", eventName, context, caller);
    }

    private void LogWithContext(
        LogLevel level,
        string eventName,
        [CallerMemberName] string caller = "")
    {
        _logger.Log(level, "{EventName} [Caller={Caller}]", eventName, caller);
    }

    private void LogWithContext(
        LogLevel level,
        string eventName,
        string message,
        [CallerMemberName] string caller = "")
    {
        _logger.Log(level, "{EventName} {Message} [Caller={Caller}]", eventName, message, caller);
    }

    private void LogWithContext(
        LogLevel level,
        string eventName,
        Exception ex,
        [CallerMemberName] string caller = "")
    {
        _logger.Log(level, ex, "{EventName} [Caller={Caller}]", eventName, caller);
    }

    private void LogWithContext(
        LogLevel level,
        string eventName,
        string message,
        Exception ex,
        [CallerMemberName] string caller = "")
    {
        _logger.Log(level, ex, "{EventName} {Message} [Caller={Caller}]", eventName, message, caller);
    }

    private async Task<HttpResponseMessage> GetAsync(string relativeUrl, CancellationToken ct = default)
    {
        return await _client.GetAsync($"{_baseUrl}{relativeUrl}", ct);
    }

    private async Task<HttpResponseMessage> PostAsync(string relativeUrl, object payload, CancellationToken ct = default)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync($"{_baseUrl}{relativeUrl}", content, ct);
    }

    private async Task<HttpResponseMessage> DeleteAsync(string relativeUrl, CancellationToken ct = default)
    {
        return await _client.DeleteAsync($"{_baseUrl}{relativeUrl}", ct);
    }
    private async Task<HttpResponseMessage> PutAsync(string relativeUrl, object payload, CancellationToken ct = default)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PutAsync($"{_baseUrl}{relativeUrl}", content, ct);
    }

    private async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Alfresco API error: {response.StatusCode} - {err}");
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    private async Task<T> ParseJsonAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Alfresco API error: {response.StatusCode} - {err}");
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(content)!;
    }

    #endregion

    #region AuthenticateAsync
    public async Task<bool> AuthenticateAsync()
    {
        //using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/alfresco/api/{TenantId}/public/authentication/versions/1/tickets");
        request.Content = new StringContent(
            $"{{\"userId\":\"{_username}\",\"password\":\"{_password}\"}}",
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await _client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return false;

        // Parse the ticket (token) from the response
        var content = await response.Content.ReadAsStringAsync();

        _ticketEntry = JsonConvert.DeserializeObject<TicketEntry>(content)!;
        Console.WriteLine($"Ticket ID: {_ticketEntry.Entry.Id}, User ID: {_ticketEntry.Entry.UserId}");

        //_authToken = System.Text.Json.JsonDocument.Parse(content)
        //    .RootElement.GetProperty("entry").GetProperty("id").GetString()!;

        // Add the token to DefaultRequestHeaders.Authorization
        //_httpClient.DefaultRequestHeaders.Authorization =
        //    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _ticketEntry.Entry.Id);

        var ticket = _ticketEntry.Entry.Id;
        var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ticket}"));
        _client.DefaultRequestHeaders.Authorization =
           new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicAuth);

        //var authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{_username}:{_password}"));
        //_httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);

        return true;
    }
    #endregion

    #region GetNodeAsync
    public async Task<Result<Node>> GetNodeAsync(
        string nodeId = "-root-",
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("GetNode", new { nodeId });

        LogWithContext(LogLevel.Information, "GetNodeStart", new { });

        var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{nodeId}?include=properties";
        var response = await GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            LogWithContext(LogLevel.Error, "GetNodeFailed", new { status = response.StatusCode, error });
            return Result<Node>.Fail($"Failed to retrieve node {nodeId}: {response.StatusCode} – {error}");
        }

        using var doc = await ParseJsonAsync(response);
        var entry = doc.RootElement.GetProperty("entry");

        var content = entry.TryGetProperty("content", out var contentProp) ? contentProp : default;
        var mimeType = content.ValueKind != JsonValueKind.Undefined &&
                    content.TryGetProperty("mimeType", out var mt)
                    ? mt.GetString()
                    : null;

        var node = new Node(
            Id: entry.GetProperty("id").GetString()!,
            Name: entry.GetProperty("name").GetString()!,
            NodeType: entry.GetProperty("nodeType").GetString()!,
            IsFolder: entry.GetProperty("isFolder").GetBoolean(),
            IsFile: entry.GetProperty("isFile").GetBoolean(),
            CreatedAt: entry.GetProperty("createdAt").GetString()!,
            MimeType: mimeType
        );

        LogWithContext(LogLevel.Information, "GetNodeSuccess", new { node.Name, node.NodeType });
        return Result<Node>.Ok(node);
    }
    #endregion

    #region GetNodeByPathAsync
    public async Task<Result<Node>> GetNodeByPathAsync(
    string path,
    string rootNodeId = "-root-",
    string nodesTypes = "",
    CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            return await GetNodeAsync(rootNodeId, ct);

        var acceptedTypes = nodesTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var segments = path.Trim('/').Split('/');
        var currentId = rootNodeId;

        foreach (var segment in segments)
        {
            var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{currentId}/children?include=properties&maxItems=100";
            var response = await GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                return Result<Node>.Fail($"Traversal failed at '{segment}': {response.StatusCode} – {error}");
            }

            using var doc = await ParseJsonAsync(response);
            var entries = doc.RootElement.GetProperty("list").GetProperty("entries");

            string? nextId = null;
            foreach (var entry in entries.EnumerateArray())
            {
                var node = entry.GetProperty("entry");
                var name = node.GetProperty("name").GetString();
                var type = node.GetProperty("nodeType").GetString() ?? "";

                var isNameMatch = string.Equals(name, segment, StringComparison.OrdinalIgnoreCase);
                var isTypeMatch = acceptedTypes.Length == 0 || acceptedTypes.Contains(type, StringComparer.OrdinalIgnoreCase);

                if (isNameMatch && isTypeMatch)
                {
                    nextId = node.GetProperty("id").GetString();
                    break;
                }
            }

            if (nextId == null)
                return Result<Node>.Fail($"Node '{segment}' not found under parent '{currentId}' with matching type.");

            currentId = nextId;
        }

        return await GetNodeAsync(currentId, ct);
    }
    #endregion

    #region ExistsPathAsync
    public async Task<Result<Node>> ExistsPathAsync(
        string path,
        string rootNodeId = "-root-",
        CancellationToken ct = default)
    {
        var cacheKey = $"ExistsPath:{rootNodeId}:{path}".ToLowerInvariant();
        using var scope = BeginLogScope("PathResolution", new { path, rootNodeId });

        if (_appCache.TryGet(cacheKey, out Result<Node>? cached))
        {
            LogWithContext(LogLevel.Debug, "PathResolutionCacheHit", new { cacheKey });
            return cached!;
        }

        LogWithContext(LogLevel.Information, "PathTraversalStart", new { });

        var result = await GetNodeByPathAsync(path, rootNodeId, "cm:folder", ct);

        if (result.Success && result.Data is not null)
        {
            LogWithContext(LogLevel.Information, "PathResolved", new
            {
                nodeId = result.Data.Id,
                nodeName = result.Data.Name,
                nodeType = result.Data.NodeType
            });

            _appCache.Set(cacheKey, result, TimeSpan.FromMinutes(CachedMinutes));
        }
        else
        {
            LogWithContext(LogLevel.Warning, "PathTraversalFailed", new { reason = result.Message });
            _appCache.Set(cacheKey, result, TimeSpan.FromSeconds(CachedSeconds));
        }

        return result;
    }
    #endregion

    #region CreateFolderAsync
    public async Task<Result<Node>> CreateFolderAsync(
        string parentId,
        string folderName,
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("CreateFolder", new { parentId, folderName });

        LogWithContext(LogLevel.Information, "CreateFolderStart", new { });

        var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{parentId}/children";

        var payload = new
        {
            name = folderName,
            nodeType = "cm:folder"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync($"{_baseUrl}{url}", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            LogWithContext(LogLevel.Error, "CreateFolderFailed", new
            {
                status = response.StatusCode,
                error
            });
            return Result<Node>.Fail($"Failed to create folder '{folderName}': {response.StatusCode} – {error}");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var entry = doc.RootElement.GetProperty("entry");

        var node = new Node(
            Id: entry.GetProperty("id").GetString()!,
            Name: entry.GetProperty("name").GetString()!,
            NodeType: entry.GetProperty("nodeType").GetString()!,
            IsFolder: entry.GetProperty("isFolder").GetBoolean(),
            IsFile: entry.GetProperty("isFile").GetBoolean(),
            CreatedAt: entry.GetProperty("createdAt").GetString()!
        );

        LogWithContext(LogLevel.Information, "CreateFolderSuccess", new { node.Id, node.Name });
        return node.AsOkResult("Folder created successfully.");
    }
    #endregion

    #region CreatePathAsync
    //
    public async Task<Result<Node>> CreatePathAsync(
        string path,
        string rootNodeId = "-root-",
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("CreatePath", new { path, rootNodeId });

        if (string.IsNullOrWhiteSpace(path))
            return await GetNodeAsync(rootNodeId, ct);

        var segments = path.Trim('/').Split('/');
        var currentId = rootNodeId;
        Result<Node> lastNodeResult = Result<Node>.Fail("Path not initialized.");

        foreach (var segment in segments)
        {
            LogWithContext(LogLevel.Debug, "CreatePathTraverse", new { segment, currentId });

            var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{currentId}/children?include=properties&maxItems=100";
            var response = await GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                return Result<Node>.Fail($"Traversal failed at '{segment}': {response.StatusCode} – {error}");
            }

            using var doc = await ParseJsonAsync(response);
            var entries = doc.RootElement.GetProperty("list").GetProperty("entries");

            string? nextId = null;
            foreach (var entry in entries.EnumerateArray())
            {
                var node = entry.GetProperty("entry");
                var name = node.GetProperty("name").GetString();
                var type = node.GetProperty("nodeType").GetString();

                if (string.Equals(name, segment, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(type, "cm:folder", StringComparison.OrdinalIgnoreCase))
                {
                    nextId = node.GetProperty("id").GetString();
                    lastNodeResult = new Node(
                        Id: nextId!,
                        Name: name!,
                        NodeType: type!,
                        IsFolder: node.GetProperty("isFolder").GetBoolean(),
                        IsFile: node.GetProperty("isFile").GetBoolean(),
                        CreatedAt: node.GetProperty("createdAt").GetString()!
                    ).AsResult("Segment exists.");

                    break;
                }
            }

            if (nextId != null)
            {
                currentId = nextId;
                continue;
            }

            LogWithContext(LogLevel.Information, "CreatePathSegmentMissing", new { segment, parentId = currentId });

            var createdResult = await CreateFolderAsync(currentId, segment, ct);
            if (!createdResult.Success) return createdResult;

            LogWithContext(LogLevel.Information, "CreatePathSegmentCreated", new
            {
                segment,
                createdResult.Data!.Id
            });

            lastNodeResult = createdResult;
            currentId = createdResult.Data.Id;
        }

        return lastNodeResult;
    }
    #endregion

    #region ExistsFileAsync
    //
    public async Task<Result<Node>> ExistsFileAsync(
        string folderPath,
        string fileName,
        string rootNodeId = "-root-",
        CancellationToken ct = default)
    {
        var path = $"{folderPath.TrimEnd('/')}/{fileName}";
        var cacheKey = $"ExistsFile:{rootNodeId}:{path}".ToLowerInvariant();

        using var scope = BeginLogScope("FileResolution", new { folderPath, fileName, rootNodeId });

        if (_appCache.TryGet(cacheKey, out Result<Node>? cached))
        {
            LogWithContext(LogLevel.Debug, "FileResolutionCacheHit", new { cacheKey });
            return cached!;
        }

        LogWithContext(LogLevel.Information, "FileTraversalStart", new { });

        var result = await GetNodeByPathAsync(path, rootNodeId, "", ct); // No filtering

        if (result.Success && result.Data!.IsFile)
        {
            LogWithContext(LogLevel.Information, "FileResolved", new
            {
                nodeId = result.Data.Id,
                nodeName = result.Data.Name,
                nodeType = result.Data.NodeType
            });

            _appCache.Set(cacheKey, result, TimeSpan.FromMinutes(CachedMinutes));
            return result;
        }

        var reason = !result.Success
            ? result.Message
            : $"Node at '{path}' is not a file.";

        LogWithContext(LogLevel.Warning, "FileTraversalFailed", new { reason });

        var failure = Result<Node>.Fail(reason!);
        _appCache.Set(cacheKey, failure, TimeSpan.FromSeconds(CachedSeconds));
        return failure;
    }

    /*
    public async Task<Result<bool>> ExistsFileAsync(string path, string fileName, string rootNodeId = "-root-", CancellationToken ct = default)
    {
        var pathResult = await ExistsPathAsync(path, rootNodeId, ct);
        if (!pathResult.Success)
            return Result<bool>.Fail($"Path not found: {pathResult.Message}");

        var folderId = pathResult.Data!;
        var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{folderId}/children?include=properties&maxItems=100";
        var response = await GetAsync(url, ct);
        using var doc = await ParseJsonAsync(response);
        var entries = doc.RootElement.GetProperty("list").GetProperty("entries");

        foreach (var entry in entries.EnumerateArray())
        {
            var node = entry.GetProperty("entry");
            if (!node.GetProperty("isFile").GetBoolean()) continue;

            var name = node.GetProperty("name").GetString();
            if (string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))
                return Result<bool>.Ok(true, "File exists.");
        }

        return Result<bool>.Ok(false, "File does not exist in the specified path.");
    }*/
    #endregion

    #region UploadFileAsync
    //

    public async Task<Result<Node>> UploadFileAsync(
        string parentId,
        string fileName,
        string filePath,
        string mimeType,
        bool overwrite = true,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return Result<Node>.Fail($"File does not exist: {filePath}");

        using var content = new MultipartFormDataContent();

        var fileBytes = await File.ReadAllBytesAsync(filePath, ct);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);

        content.Add(fileContent, "filedata", fileName);
        content.Add(new StringContent(fileName), "name");
        content.Add(new StringContent(overwrite.ToString().ToLower()), "overwrite");

        var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{parentId}/children";
        var response = await _client.PostAsync($"{_baseUrl}{url}", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return Result<Node>.Fail($"Upload failed: {response.StatusCode} – {error}");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var entry = doc.RootElement.GetProperty("entry");

        var node = new Node(
            Id: entry.GetProperty("id").GetString()!,
            Name: entry.GetProperty("name").GetString()!,
            NodeType: entry.GetProperty("nodeType").GetString()!,
            IsFolder: entry.GetProperty("isFolder").GetBoolean(),
            IsFile: entry.GetProperty("isFile").GetBoolean(),
            CreatedAt: entry.GetProperty("createdAt").GetString()!
        );

        return Result<Node>.Ok(node, "File uploaded successfully.");
    }
    #endregion

    #region UploadFileByPathAsync
    //
    public async Task<Result<Node>> UploadFileByPathAsync(
        string folderPath,
        string fileName,
        string filePath,
        string mimeType,
        bool overwrite = true,
        bool alwaysCreatePath = true,
        string rootNodeId = "-root-",
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("UploadFileByPath", new
        {
            folderPath,
            fileName,
            rootNodeId,
            alwaysCreatePath
        });

        LogWithContext(LogLevel.Information, "UploadByPathStart", new { filePath });

        Result<Node> folderResult;

        if (alwaysCreatePath)
        {
            folderResult = await CreatePathAsync(folderPath, rootNodeId, ct);
        }
        else
        {
            folderResult = await ExistsPathAsync(folderPath, rootNodeId, ct);
        }

        if (!folderResult.Success)
        {
            LogWithContext(LogLevel.Warning, "UploadFolderUnavailable", new { reason = folderResult.Message });
            return Result<Node>.Fail($"Folder '{folderPath}' unavailable: {folderResult.Message}");
        }

        var uploadResult = await UploadFileAsync(folderResult.Data!.Id, fileName, filePath, mimeType, overwrite, ct);

        if (uploadResult.Success)
        {
            LogWithContext(LogLevel.Information, "UploadByPathSuccess", new
            {
                uploadResult.Data!.Id,
                uploadResult.Data.Name
            });
        }
        else
        {
            LogWithContext(LogLevel.Error, "UploadByPathFailed", new { error = uploadResult.Message });
        }

        return uploadResult;
    }
    #endregion

    #region DownloadFileAsync
    public async Task<Result<string>> DownloadFileAsync(
        string nodeId,
        string localPath,
        string localFileName,
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("DownloadFile", new { nodeId, localPath, localFileName });

        LogWithContext(LogLevel.Information, "DownloadStart", new { });

        var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{nodeId}/content";
        var requestUrl = $"{_baseUrl}{url}";

        HttpResponseMessage response;
        try
        {
            response = await _client.GetAsync(requestUrl, ct);
        }
        catch (Exception ex)
        {
            LogWithContext(LogLevel.Error, "DownloadHttpError", new { ex.Message });
            return Result<string>.Fail($"HTTP error while downloading: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            LogWithContext(LogLevel.Error, "DownloadFailed", new { status = response.StatusCode, error });
            return Result<string>.Fail($"Failed to download node {nodeId}: {response.StatusCode} – {error}");
        }

        var fullPath = Path.Combine(localPath, localFileName);

        try
        {
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            await File.WriteAllBytesAsync(fullPath, bytes, ct);

            LogWithContext(LogLevel.Information, "DownloadSuccess", new { fullPath });
            return Result<string>.Ok(fullPath, "File downloaded successfully.");
        }
        catch (Exception ex)
        {
            LogWithContext(LogLevel.Error, "DownloadWriteError", new { ex.Message });
            return Result<string>.Fail($"Failed to save file: {ex.Message}");
        }
    }
    #endregion

    #region DownloadFileByPathAsync
    //
    public async Task<Result<string>> DownloadFileByPathAsync(
        string folderPath,
        string fileName,
        string localPath,
        string localFileName = "",
        string rootNodeId = "-root-",
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("DownloadFileByPath", new
        {
            folderPath,
            fileName,
            localPath,
            localFileName,
            rootNodeId
        });

        LogWithContext(LogLevel.Information, "DownloadFileByPathStart", new { });

        var fileResult = await ExistsFileAsync(folderPath, fileName, rootNodeId, ct);
        if (!fileResult.Success)
        {
            LogWithContext(LogLevel.Warning, "FileNotFoundByPath", new { reason = fileResult.Message });
            return Result<string>.Fail($"File not found at '{folderPath}/{fileName}': {fileResult.Message}");
        }

        var finalFileName = string.IsNullOrWhiteSpace(localFileName) ? fileName : localFileName;

        var downloadResult = await DownloadFileAsync(fileResult.Data!.Id, localPath, finalFileName, ct);

        if (downloadResult.Success)
        {
            LogWithContext(LogLevel.Information, "DownloadFileByPathSuccess", new { downloadResult.Data });
        }
        else
        {
            LogWithContext(LogLevel.Error, "DownloadFileByPathFailed", new { downloadResult.Message });
        }

        return downloadResult;
    }
    #endregion

    #region DownloadFileStreamAsync
    public async Task<Result<FileContentInfo>> DownloadFileStreamAsync(
        string nodeId,
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("DownloadFileStream", new { nodeId });

        LogWithContext(LogLevel.Information, "DownloadStreamStart", new { });

        var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{nodeId}/content";
        var requestUrl = $"{_baseUrl}{url}";

        HttpResponseMessage response;
        try
        {
            response = await _client.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex)
        {
            LogWithContext(LogLevel.Error, "DownloadStreamHttpError", new { ex.Message });
            return Result<FileContentInfo>.Fail($"HTTP error during stream download: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            LogWithContext(LogLevel.Error, "DownloadStreamFailed", new { status = response.StatusCode, error });
            return Result<FileContentInfo>.Fail($"Failed to download stream: {response.StatusCode} – {error}");
        }

        var content = await response.Content.ReadAsStreamAsync(ct);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        var contentLength = response.Content.Headers.ContentLength;

        var info = new FileContentInfo(
            Content: content,
            MimeType: contentType,
            FileName: fileName,
            ContentLength: contentLength
        );

        LogWithContext(LogLevel.Information, "DownloadStreamSuccess", new { nodeId, contentType });
        return Result<FileContentInfo>.Ok(info, "Stream with metadata ready.");
    }
    #endregion

    #region DownloadFileStreamByPathAsync
    public async Task<Result<FileContentInfo>> DownloadFileStreamByPathAsync(
        string folderPath,
        string fileName,
        string rootNodeId = "-root-",
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("DownloadFileStreamByPath", new
        {
            folderPath,
            fileName,
            rootNodeId
        });

        LogWithContext(LogLevel.Information, "DownloadFileStreamByPathStart", new { });

        var fileResult = await ExistsFileAsync(folderPath, fileName, rootNodeId, ct);
        if (!fileResult.Success)
        {
            LogWithContext(LogLevel.Warning, "FileNotFoundByPath", new { reason = fileResult.Message });
            return Result<FileContentInfo>.Fail($"File not found at '{folderPath}/{fileName}': {fileResult.Message}");
        }

        var nodeId = fileResult.Data!.Id;
        var streamResult = await DownloadFileStreamAsync(nodeId, ct);

        if (streamResult.Success)
        {
            LogWithContext(LogLevel.Information, "DownloadFileStreamByPathSuccess", new { fileName, nodeId });
        }
        else
        {
            LogWithContext(LogLevel.Error, "DownloadFileStreamByPathFailed", new { streamResult.Message });
        }

        return streamResult;
    }
    #endregion

    #region DeleteFileAsync
    public async Task<Result<bool>> DeleteFileAsync(
        string nodeId,
        bool permanent = false,
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("DeleteFile", new { nodeId, permanent });

        LogWithContext(LogLevel.Information, "DeleteFileStart", new { });

        var query = permanent ? "?permanent=true" : string.Empty;
        var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{nodeId}{query}";

        HttpResponseMessage response;

        try
        {
            response = await _client.DeleteAsync($"{_baseUrl}{url}", ct);
        }
        catch (Exception ex)
        {
            LogWithContext(LogLevel.Error, "DeleteFileHttpError", new { ex.Message });
            return Result<bool>.Fail($"HTTP error during deletion: {ex.Message}");
        }

        if (response.IsSuccessStatusCode)
        {
            LogWithContext(LogLevel.Information, "DeleteFileSuccess", new { nodeId });
            return Result<bool>.Ok(true, "File deleted successfully.");
        }

        var error = await response.Content.ReadAsStringAsync(ct);
        LogWithContext(LogLevel.Error, "DeleteFileFailed", new
        {
            status = response.StatusCode,
            error
        });

        return Result<bool>.Fail($"Failed to delete node {nodeId}: {response.StatusCode} – {error}");
    }
    #endregion

    #region DeleteFileByPathAsync
    public async Task<Result<bool>> DeleteFileByPathAsync(
        string folderPath,
        string fileName,
        bool permanent = false,
        string rootNodeId = "-root-",
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("DeleteFileByPath", new
        {
            folderPath,
            fileName,
            permanent,
            rootNodeId
        });

        LogWithContext(LogLevel.Information, "DeleteFileByPathStart", new { });

        var fileResult = await ExistsFileAsync(folderPath, fileName, rootNodeId, ct);
        if (!fileResult.Success)
        {
            LogWithContext(LogLevel.Warning, "DeleteFileNotFound", new { reason = fileResult.Message });
            return Result<bool>.Fail($"File not found at '{folderPath}/{fileName}': {fileResult.Message}");
        }

        var deleteResult = await DeleteFileAsync(fileResult.Data!.Id, permanent, ct);

        if (deleteResult.Success)
        {
            LogWithContext(LogLevel.Information, "DeleteFileByPathSuccess", new { nodeId = fileResult.Data.Id });
        }
        else
        {
            LogWithContext(LogLevel.Error, "DeleteFileByPathFailed", new { deleteResult.Message });
        }

        return deleteResult;
    }
    #endregion

    #region ListDescendantsAsync
    public async Task<Result<List<Node>>> ListDescendantsAsync(
        string parentNodeId,
        string nodeTypes = "",
        bool includeSubFolders = false,
        bool includeRootNode = false,
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("ListDescendants", new
        {
            parentNodeId,
            nodeTypes,
            includeSubFolders,
            includeRootNode
        });

        LogWithContext(LogLevel.Information, "ListDescendantsAsyncStart", new { });

        var allNodes = new List<Node>();
        var typeSet = nodeTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // ✅ Optional: Include root node as first element
        if (includeRootNode && includeSubFolders)
        {
            var rootResult = await GetNodeAsync(parentNodeId, ct);
            if (!rootResult.Success)
            {


                LogWithContext(LogLevel.Error, "RootNodeFetchFailed", new { parentNodeId, rootResult.Message });
                return Result<List<Node>>.Fail($"Failed to fetch root node: {rootResult.Message}");
            }

            if (typeSet.Count == 0 || typeSet.Contains(rootResult.Data!.NodeType))
                allNodes.Add(rootResult.Data!);
        }

        var stack = new Stack<string>();
        stack.Push(parentNodeId);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var currentId = stack.Pop();

            var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{currentId}/children?include=properties&maxItems=100";
            var response = await GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                LogWithContext(LogLevel.Error, "ChildNodeFetchFailed", new { parent = currentId, status = response.StatusCode, error });
                return Result<List<Node>>.Fail($"Failed to list children for {currentId}: {response.StatusCode} – {error}");
            }

            using var doc = await ParseJsonAsync(response);
            var entries = doc.RootElement.GetProperty("list").GetProperty("entries");

            foreach (var entry in entries.EnumerateArray())
            {
                var node = entry.GetProperty("entry");
                var type = node.GetProperty("nodeType").GetString() ?? "";

                var content = node.TryGetProperty("content", out var contentProp) ? contentProp : default;
                var mimeType = content.ValueKind != JsonValueKind.Undefined &&
                            content.TryGetProperty("mimeType", out var mt)
                                ? mt.GetString()
                                : null;

                var parentId = node.TryGetProperty("parentId", out var pid) ? pid.GetString() : null;

                var model = new Node(
                    Id: node.GetProperty("id").GetString()!,
                    Name: node.GetProperty("name").GetString()!,
                    NodeType: type,
                    IsFolder: node.GetProperty("isFolder").GetBoolean(),
                    IsFile: node.GetProperty("isFile").GetBoolean(),
                    CreatedAt: node.GetProperty("createdAt").GetString()!,
                    MimeType: mimeType,
                    ParentId: parentId
                );

                if (includeSubFolders && model.IsFolder)
                    stack.Push(model.Id);

                if (typeSet.Count == 0 || typeSet.Contains(type))
                    allNodes.Add(model);
            }
        }

        LogWithContext(LogLevel.Information, "ListDescendantsAsyncSuccess", new { nodeCount = allNodes.Count });
        return Result<List<Node>>.Ok(allNodes);
    }
    #endregion

    #region ListChildrenAsync
    public Task<Result<List<Node>>> ListChildrenAsync(
        string parentNodeId,
        string nodeTypes = "",
        CancellationToken ct = default)
    {
        return ListDescendantsAsync(
            parentNodeId: parentNodeId,
            nodeTypes: nodeTypes,
            includeSubFolders: false,
            ct: ct);
    }

    /*
    public async Task<Result<List<Node>>> GetNodeListAsync(
        string parentNodeId,
        string nodeTypes = "",
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("GetNodeList", new { parentNodeId, nodeTypes });

        LogWithContext(LogLevel.Information, "GetNodeListStart", new { });

        var types = nodeTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{parentNodeId}/children?include=properties&maxItems=100";
        var response = await GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            LogWithContext(LogLevel.Error, "GetNodeListFailed", new { status = response.StatusCode, error });
            return Result<List<Node>>.Fail($"Failed to retrieve child nodes: {response.StatusCode} – {error}");
        }

        using var doc = await ParseJsonAsync(response);
        var entries = doc.RootElement.GetProperty("list").GetProperty("entries");

        var nodes = new List<Node>();

        foreach (var entry in entries.EnumerateArray())
        {
            var node = entry.GetProperty("entry");
            var type = node.GetProperty("nodeType").GetString() ?? "";

            if (types.Count > 0 && !types.Contains(type))
                continue;

            var content = node.TryGetProperty("content", out var contentProp) ? contentProp : default;
            var mimeType = content.ValueKind != JsonValueKind.Undefined &&
                        content.TryGetProperty("mimeType", out var mt)
                            ? mt.GetString()
                            : null;

            var parentId = node.TryGetProperty("parentId", out var pid) ? pid.GetString() : null;

            nodes.Add(new Node(
                Id: node.GetProperty("id").GetString()!,
                Name: node.GetProperty("name").GetString()!,
                NodeType: type,
                IsFolder: node.GetProperty("isFolder").GetBoolean(),
                IsFile: node.GetProperty("isFile").GetBoolean(),
                CreatedAt: node.GetProperty("createdAt").GetString()!,
                MimeType: mimeType,
                ParentId: parentId
            ));
        }

        LogWithContext(LogLevel.Information, "GetNodeListSuccess", new { count = nodes.Count });
        return Result<List<Node>>.Ok(nodes, "Node list with parent IDs retrieved.");
    }
    */
    #endregion

    #region ListDescendantsByPathAsync
    public async Task<Result<List<Node>>> ListDescendantsByPathAsync(
        string folderPath,
        string nodeTypes = "",
        bool includeSubFolders = false,
        bool includeRootNode = false,
        string rootNodeId = "-root-",
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("ListDescendantsByPath", new
        {
            folderPath,
            nodeTypes,
            includeSubFolders,
            includeRootNode,
            rootNodeId
        });

        LogWithContext(LogLevel.Information, "ListDescendantsByPathStart", new { });

        var pathResult = await ExistsPathAsync(folderPath, rootNodeId, ct);
        if (!pathResult.Success)
        {
            LogWithContext(LogLevel.Warning, "PathResolutionFailed", new { folderPath, pathResult.Message });
            return Result<List<Node>>.Fail($"Path not found: {folderPath} – {pathResult.Message}");
        }

        return await ListDescendantsAsync(
            parentNodeId: pathResult.Data!.Id,
            nodeTypes: nodeTypes,
            includeSubFolders: includeSubFolders,
            includeRootNode: includeRootNode,
            ct: ct);
    }

    #endregion

    #region ListChildrenByPathAsync
    public Task<Result<List<Node>>> ListChildrenByPathAsync(
        string folderPath,
        string nodeTypes = "",
        string rootNodeId = "-root-",
        CancellationToken ct = default)
    {
        return ListDescendantsByPathAsync(
            folderPath: folderPath,
            nodeTypes: nodeTypes,
            includeSubFolders: false,
            rootNodeId: rootNodeId,
            ct: ct);
    }

    /*
    public async Task<Result<List<Node>>> GetNodeListByPathAsync(
        string folderPath,
        string nodeTypesCsv = "",
        string rootNodeId = "-root-",
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("GetNodeListByPath", new
        {
            folderPath,
            nodeTypesCsv,
            rootNodeId
        });

        LogWithContext(LogLevel.Information, "GetNodeListByPathStart", new { });

        var cacheKey = $"node-list::{folderPath}::{nodeTypesCsv}";

        return await _appCache.GetOrAddAsync(cacheKey, async () =>
        {
            var folderResult = await ExistsPathAsync(folderPath, rootNodeId, ct);
            if (!folderResult.Success)
            {
                LogWithContext(LogLevel.Warning, "FolderNotFound", new { folderPath, folderResult.Message });
                return Result<List<Node>>.Fail($"Path not found: {folderPath} – {folderResult.Message}");
            }

            var listResult = await GetNodeListAsync(folderResult.Data!.Id, nodeTypesCsv, ct);
            return listResult;
        }, ct);
    }
    */
    #endregion

    #region AddTreeNodeAsync
    public async Task<Result<List<AlfrescoTreeNode>>> AddTreeNodeAsync(
        string folderId,
        List<AlfrescoTreeNode>? list = null,
        CancellationToken ct = default)
    {
        list ??= new List<AlfrescoTreeNode>();

        var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{folderId}/children?include=properties&maxItems=100";
        var response = await GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return Result<List<AlfrescoTreeNode>>.Fail($"Failed to retrieve children for folder '{folderId}': {response.StatusCode} – {error}");
        }

        try
        {
            using var doc = await ParseJsonAsync(response);
            var entries = doc.RootElement.GetProperty("list").GetProperty("entries");

            foreach (var entry in entries.EnumerateArray())
            {
                var node = entry.GetProperty("entry");
                if (!node.GetProperty("isFolder").GetBoolean()) continue;

                var treeNode = new AlfrescoTreeNode
                {
                    ParentId = node.GetProperty("parentId").GetString() ?? "",
                    Id = node.GetProperty("id").GetString() ?? "",
                    Name = node.GetProperty("name").GetString() ?? ""
                };

                list.Add(treeNode);

                var subTreeResult = await AddTreeNodeAsync(treeNode.Id, list, ct);
                if (!subTreeResult.Success)
                    return Result<List<AlfrescoTreeNode>>.Fail(subTreeResult!.Message!); // bubble up failure
            }

            return Result<List<AlfrescoTreeNode>>.Ok(list, "Tree successfully built.");
        }
        catch (Exception ex)
        {
            return Result<List<AlfrescoTreeNode>>.Fail($"Error processing folder '{folderId}': {ex.Message}");
        }
    }
    #endregion

    #region AddTreeNodeByPathAsync
    //
    public async Task<Result<List<AlfrescoTreeNode>>> AddTreeNodeByPathAsync(
        string path,
        string rootNodeId = "-root-",
        CancellationToken ct = default)
    {
        var pathResult = await ExistsPathAsync(path, rootNodeId, ct);
        if (!pathResult.Success)
            return Result<List<AlfrescoTreeNode>>.Fail($"Path resolution failed: {pathResult.Message}");

        var folderId = pathResult.Data!;
        return await AddTreeNodeAsync(folderId.Id, null, ct);
    }
    #endregion

    #region CopyNodeAsync
    public async Task<Result<Node>> CopyNodeAsync(
        string sourceNodeId,
        string targetParentId,
        string? targetName = null,
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("CopyNode", new
        {
            sourceNodeId,
            targetParentId,
            targetName
        });

        LogWithContext(LogLevel.Information, "CopyNodeStart", new { });

        var payload = new Dictionary<string, object>
        {
            ["targetParentId"] = targetParentId
        };

        if (!string.IsNullOrWhiteSpace(targetName))
            payload["name"] = targetName;

        var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{sourceNodeId}/copy";

        var response = await PostAsync(url, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            LogWithContext(LogLevel.Error, "CopyNodeFailed", new { status = response.StatusCode, error });
            return Result<Node>.Fail($"Failed to copy node {sourceNodeId}: {response.StatusCode} – {error}");
        }

        using var doc = await ParseJsonAsync(response);
        var entry = doc.RootElement.GetProperty("entry");

        var content = entry.TryGetProperty("content", out var contentProp) ? contentProp : default;
        var mimeType = content.ValueKind != JsonValueKind.Undefined &&
                       content.TryGetProperty("mimeType", out var mt)
            ? mt.GetString()
            : null;

        var parentId = entry.TryGetProperty("parentId", out var pid) ? pid.GetString() : null;

        var node = new Node(
            Id: entry.GetProperty("id").GetString()!,
            Name: entry.GetProperty("name").GetString()!,
            NodeType: entry.GetProperty("nodeType").GetString()!,
            IsFolder: entry.GetProperty("isFolder").GetBoolean(),
            IsFile: entry.GetProperty("isFile").GetBoolean(),
            CreatedAt: entry.GetProperty("createdAt").GetString()!,
            MimeType: mimeType,
            ParentId: parentId
        );

        LogWithContext(LogLevel.Information, "CopyNodeSuccess", new { node.Id, node.Name });
        return Result<Node>.Ok(node, "Node copied successfully.");
    }
    #endregion

    #region CopyNodeByPathAsync
    public async Task<Result<Node>> CopyNodeByPathAsync(
        string sourceNodePath,
        string targetFolderPath,
        string? targetName = null,
        string rootNodeId = "-root-",
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("CopyNodeByPath", new
        {
            sourceNodePath,
            targetFolderPath,
            targetName,
            rootNodeId
        });

        LogWithContext(LogLevel.Information, "CopyNodeByPathStart", new { });

        // Use GetNodeByPathAsync instead of GetFileIdByPathAsync
        var sourceResult = await GetNodeByPathAsync(sourceNodePath, rootNodeId, "", ct);
        if (!sourceResult.Success)
        {
            LogWithContext(LogLevel.Warning, "CopySourceNotFound", new { reason = sourceResult.Message });
            return Result<Node>.Fail($"Source node not found: {sourceResult.Message}");
        }

        var targetResult = await ExistsPathAsync(targetFolderPath, rootNodeId, ct);
        if (!targetResult.Success)
        {
            LogWithContext(LogLevel.Warning, "CopyTargetNotFound", new { reason = targetResult.Message });
            return Result<Node>.Fail($"Target folder not found: {targetResult.Message}");
        }

        return await CopyNodeAsync(
            sourceNodeId: sourceResult.Data!.Id,
            targetParentId: targetResult.Data!.Id,
            targetName: targetName,
            ct: ct);
    }
    #endregion

    #region MoveNodeAsync
    public async Task<Result<Node>> MoveNodeAsync(
        string sourceNodeId,
        string targetParentId,
        string? newName = null,
        CancellationToken ct = default)
    {
        using var scope = BeginLogScope("MoveNode", new
        {
            sourceNodeId,
            targetParentId,
            newName
        });

        LogWithContext(LogLevel.Information, "MoveNodeStart", new { });

        var payload = new Dictionary<string, object>
        {
            ["targetParentId"] = targetParentId
        };

        if (!string.IsNullOrWhiteSpace(newName))
            payload["name"] = newName;

        var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{sourceNodeId}/move";
        var response = await PostAsync(url, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            LogWithContext(LogLevel.Error, "MoveNodeFailed", new { status = response.StatusCode, error });
            return Result<Node>.Fail($"Failed to move node {sourceNodeId}: {response.StatusCode} – {error}");
        }

        using var doc = await ParseJsonAsync(response);
        var entry = doc.RootElement.GetProperty("entry");

        var content = entry.TryGetProperty("content", out var contentProp) ? contentProp : default;
        var mimeType = content.ValueKind != JsonValueKind.Undefined &&
                       content.TryGetProperty("mimeType", out var mt)
            ? mt.GetString()
            : null;

        var parentId = entry.TryGetProperty("parentId", out var pid) ? pid.GetString() : null;

        var node = new Node(
            Id: entry.GetProperty("id").GetString()!,
            Name: entry.GetProperty("name").GetString()!,
            NodeType: entry.GetProperty("nodeType").GetString()!,
            IsFolder: entry.GetProperty("isFolder").GetBoolean(),
            IsFile: entry.GetProperty("isFile").GetBoolean(),
            CreatedAt: entry.GetProperty("createdAt").GetString()!,
            MimeType: mimeType,
            ParentId: parentId
        );

        LogWithContext(LogLevel.Information, "MoveNodeSuccess", new { node.Id, node.Name });
        return Result<Node>.Ok(node, "Node moved successfully.");
    }
    #endregion

    #region MoveNodeByPathAsync
    public async Task<Result<Node>> MoveNodeByPathAsync(
    string sourceNodePath,
    string targetFolderPath,
    string? newName = null,
    string rootNodeId = "-root-",
    CancellationToken ct = default)
    {
        using var scope = BeginLogScope("MoveNodeByPath", new
        {
            sourceNodePath,
            targetFolderPath,
            newName,
            rootNodeId
        });

        LogWithContext(LogLevel.Information, "MoveNodeByPathStart", new { });

        // Resolve source node (using GetNodeByPathAsync)
        var sourceResult = await GetNodeByPathAsync(sourceNodePath, rootNodeId, "", ct);
        if (!sourceResult.Success)
        {
            LogWithContext(LogLevel.Warning, "MoveSourceNotFound", new { reason = sourceResult.Message });
            return Result<Node>.Fail($"Source node not found: {sourceResult.Message}");
        }

        // Resolve target folder
        var targetResult = await ExistsPathAsync(targetFolderPath, rootNodeId, ct);
        if (!targetResult.Success)
        {
            LogWithContext(LogLevel.Warning, "MoveTargetNotFound", new { reason = targetResult.Message });
            return Result<Node>.Fail($"Target folder not found: {targetResult.Message}");
        }

        return await MoveNodeAsync(
            sourceNodeId: sourceResult.Data!.Id,
            targetParentId: targetResult.Data!.Id,
            newName: newName,
            ct: ct);
    }
    #endregion

    #region BuildNodeHierarchy
    public List<HierarchyItem> BuildNodeHierarchy(List<Node> flatNodes)
    {
        var itemMap = flatNodes.ToDictionary(
            n => n.Id,
            n => new HierarchyItem(
                Id: n.Id,
                Name: n.Name,
                MimeType: n.MimeType,
                ParentId: n.ParentId,
                IsFolder: n.IsFolder
            ));

        foreach (var node in flatNodes)
        {
            if (!string.IsNullOrWhiteSpace(node.ParentId) &&
                itemMap.TryGetValue(node.ParentId, out var parent))
            {
                var child = itemMap[node.Id];
                parent.Children.Add(child);
            }
        }

        return itemMap.Values
            .Where(item => string.IsNullOrWhiteSpace(item.ParentId) || !itemMap.ContainsKey(item.ParentId))
            .ToList();
    }
    #endregion

    #region PrintTree
    public void PrintTree(IEnumerable<HierarchyItem> items, int indent = 0)
    {
        foreach (var item in items.OrderBy(i => i.IsFolder ? 0 : 1).ThenBy(i => i.Name))
        {
            var prefix = item.IsFolder ? "📁" : "📄";
            Console.WriteLine($"{new string(' ', indent * 2)}{prefix} {item.Name} [{item.Id}]");

            if (item.Children.Any())
                PrintTree(item.Children, indent + 1);
        }
    }
    #endregion

    //
    private Node ToNodeModel(JsonElement node)
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

    private async Task<Result<T>> SendAlfrescoRequestAsync<T>(
        string url,
        Func<JsonElement, T> parse,
        HttpMethod? method = null,
        HttpContent? content = null,
        string logScope = "",
        CancellationToken ct = default)
    {
        method ??= HttpMethod.Get;
        using var scope = BeginLogScope(logScope, new { url });

            try
            {
                var request = new HttpRequestMessage(method, url);
                if (content is not null)
                    request.Content = content;

                var response = await _client.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                    return await ResultExtensions.FailFromResponseAsync<T>(response, logScope, ct);
                /*
                if (!response.IsSuccessStatusCode)
                    {
                        var err = await response.Content.ReadAsStringAsync(ct);
                        LogWithContext(LogLevel.Error, "RequestFailed", new { url, status = response.StatusCode, err });
                        return Result<T>.Fail($"Alfresco API error: {response.StatusCode} – {err}");
                    }
                */

                using var doc = await ParseJsonAsync(response);
                var result = parse(doc.RootElement);
                return Result<T>.Ok(result);
            }
            catch (Exception ex)
            {
                LogWithContext(LogLevel.Error, "RequestException", new { url, ex.Message });
                return Result<T>.Fail($"[{logScope}] Unexpected exception: {ex.Message}");
            //return Result<T>.Fail($"Exception calling Alfresco: {ex.Message}");
            }
    }

    public Task<Result<List<Node>>> ListChildrenPageAsync(
        string parentNodeId,
        int skipCount = 0,
        int maxItems = 100,
        CancellationToken ct = default)
    {
        var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{parentNodeId}/children" +
                $"?include=properties&skipCount={skipCount}&maxItems={maxItems}";

        return SendAlfrescoRequestAsync(
            url: url,
            parse: root => root.GetProperty("list").GetProperty("entries")
                            .EnumerateArray()
                            .Select(e => ToNodeModel(e.GetProperty("entry")))
                            .ToList(),
            logScope: "ListChildrenPage",
            ct: ct);
    }


    public async IAsyncEnumerable<Node> StreamChildrenAsync(
        string parentNodeId,
        int pageSize = 100,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        int skip = 0;

        while (true)
        {
            var pageResult = await ListChildrenPageAsync(parentNodeId, skip, pageSize, ct);
            if (!pageResult.Success || pageResult.Data!.Count == 0)
                yield break;

            foreach (var node in pageResult.Data)
                yield return node;

            if (pageResult.Data.Count < pageSize)
                yield break;

            skip += pageSize;
        }
    }

    public async Task<Result<List<Node>>> ListChildrenPageByPathAsync(
        string folderPath,
        int skipCount = 0,
        int maxItems = 100,
        string rootNodeId = "-root-",
        CancellationToken ct = default)
    {
        var pathResult = await ExistsPathAsync(folderPath, rootNodeId, ct);
        if (!pathResult.Success)
            return Result<List<Node>>.Fail($"Path not found: {folderPath} – {pathResult.Message}");

        return await ListChildrenPageAsync(pathResult.Data!.Id, skipCount, maxItems, ct);
    }


    public async IAsyncEnumerable<Node> StreamChildrenByPathAsync(
        string folderPath,
        int pageSize = 100,
        string rootNodeId = "-root-",
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var pathResult = await ExistsPathAsync(folderPath, rootNodeId, ct);
        if (!pathResult.Success)
            yield break;

        await foreach (var node in StreamChildrenAsync(pathResult.Data!.Id, pageSize, ct))
            yield return node;
    }

    public async IAsyncEnumerable<Node> StreamDescendantsAsync(
        string parentNodeId,
        string nodeTypes = "",
        bool includeSubFolders = false,
        bool includeRootNode = false,
        Func<Node, bool>? filter = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var typeSet = nodeTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (includeRootNode && includeSubFolders)
        {
            var rootResult = await GetNodeAsync(parentNodeId, ct);
            if (rootResult.Success &&
                (typeSet.Count == 0 || typeSet.Contains(rootResult.Data!.NodeType)) &&
                (filter == null || filter(rootResult.Data!)))
            {
                yield return rootResult.Data!;
            }
        }

        var stack = new Stack<string>();
        stack.Push(parentNodeId);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var currentId = stack.Pop();

            var url = $"/alfresco/api/{TenantId}/public/alfresco/versions/1/nodes/{currentId}/children" +
                      "?include=properties&maxItems=100";

            var response = await GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                yield break;

            using var doc = await ParseJsonAsync(response);
            var entries = doc.RootElement.GetProperty("list").GetProperty("entries");

            foreach (var entry in entries.EnumerateArray())
            {
                var model = ToNodeModel(entry.GetProperty("entry"));

                if (includeSubFolders && model.IsFolder)
                    stack.Push(model.Id);

                if ((typeSet.Count == 0 || typeSet.Contains(model.NodeType)) &&
                    (filter == null || filter(model)))
                {
                    yield return model;
                }
            }
        }
    }

}


