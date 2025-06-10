// See https://aka.ms/new-console-template for more information
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Phoenix.Alfresco;

var ColoredText = (string text, ConsoleColor color) =>
{
    var originalColor = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ForegroundColor = originalColor;
};

var _logger = new LoggerFactory().CreateLogger<AlfrescoClient>();

var options = new MemoryCacheOptions
{
    //SizeLimit = 1024 // optional, you can omit for unlimited size
};

var memoryCache = new MemoryCache(options);
var cacheProvider = new MemoryCacheProvider(memoryCache, TimeSpan.FromMinutes(10));

AlfrescoClient client = new AlfrescoClient("http://localhost:8080", "admin", "admin", _logger, cacheProvider);

if (await client.AuthenticateAsync())
{
    Console.WriteLine("Authentication successful.");

    //
    ColoredText("\r\nCreating path...", ConsoleColor.Yellow);
    var result = await client.CreatePathAsync("Projects/2025/Specs");
    if (result.Success)
    {
        Console.WriteLine($"CreatePathAsync: Final folder ID: {result.Data}");
    }
    else
        Console.WriteLine($"CreatePathAsync: Failed to create path: {result.Message}");

    /*
    //
    Console.WriteLine();
    result = await client.ExistsPathAsync("Projects/2025/Specs");
    if (result.Success)
        Console.WriteLine($"Path found. Final folder ID: {result.Data}");
    else
        Console.WriteLine($"Missing path: {result.Message}");
    */

    //
    ColoredText("\r\nChecking if path exists...", ConsoleColor.Yellow);
    var ndResult = await client.ExistsPathAsync("Projects/2025/Specs");
    Console.WriteLine($"ExistsPathAsync:\r\nStatus: {ndResult.Success} - {ndResult.Message}");
    if (ndResult.Success)
        Console.WriteLine($"Node: {ndResult.Data!.Id} {ndResult.Data.Name} ({ndResult.Data.NodeType})");

    //
    ColoredText("\r\nUploading file...", ConsoleColor.Yellow);
    result = await client.UploadFileByPathAsync(
        folderPath: "Projects/2025/Specs",
        fileName: "plan.pdf",
        filePath: @"/Users/Mecathron/Downloads/AI-102-EH-20250328.pdf",
        mimeType: "application/pdf"
    );

    Console.WriteLine(result.Success
        ? $"✅ Uploaded. Node ID: {result.Data}"
        : $"❌ Upload failed: {result.Message}");

    //
    ColoredText("\r\nChecking if file exists...", ConsoleColor.Yellow);
    var fResult = await client.ExistsFileAsync("Projects/2025/Specs", "plan.pdf");
    if (fResult.Success)
    {
        var fileNode = fResult.Data!;
        Console.WriteLine($"✅ File found: {fileNode.Name} (ID: {fileNode.Id})");
    }
    else
    {
        Console.WriteLine($"❌ File check failed: {fResult.Message}");
    }

    //
    ColoredText("\r\nFetching file by path...", ConsoleColor.Yellow);
    var dlResult = await client.DownloadFileByPathAsync(
        folderPath: "Projects/2025/Specs",
        fileName: "plan.pdf",
        localPath: @"/Users/Mecathron/Downloads",
        localFileName: "plan_downloaded.pdf"
    );

    Console.WriteLine(dlResult.Success
        ? $"✅ File saved at: {dlResult.Data}"
        : $"❌ Download failed: {dlResult.Message}");

    //
    ColoredText("\r\nFetching node info by path...", ConsoleColor.Yellow);
    ndResult = await client.GetNodeByPathAsync(path: "Projects/2025/Specs/plan.pdf");
    if (ndResult.Success!)
    {
        var node = ndResult.Data!;
        Console.WriteLine($"✅ Node Entry fetched successfully:");
        Console.WriteLine($"Node ID: {node.Id}");
        Console.WriteLine($"Node Name: {node.Name}");
        Console.WriteLine($"Node Type: {node.NodeType}");
        Console.WriteLine($"Is Folder: {node.IsFolder}");
        Console.WriteLine($"Is File: {node.IsFile}");
        Console.WriteLine($"Created At: {node.CreatedAt}");
    }
    else
    {
        Console.WriteLine($"❌ Error fetching node entry: {ndResult.Message}");
    }

    //
    ColoredText("\r\nFetching file list by ID...", ConsoleColor.Yellow);
    var flResult = await client.ListChildrenAsync("27d28359-2164-4b11-9283-592164bb1104", nodeTypes: "cm:content");
    if (flResult.Success)
        foreach (var file in flResult.Data!)
            Console.WriteLine($"📄 {file.Name} [{file.NodeType}] ({file.MimeType})");
    else
        Console.WriteLine($"❌ Failed: {flResult.Message}");

    //
    ColoredText("\r\nFetching file list by ID...", ConsoleColor.Yellow);
    var nlResult = await client.ListChildrenByPathAsync(
        folderPath: "Projects/2025/Specs",
        nodeTypes: "cm:content", // Include folders and documents
        rootNodeId: "-root-");

    if (nlResult.Success)
    {
        foreach (var node in nlResult.Data!)
        {
            var type = node.IsFolder ? "📁 Folder" : "📄 File";
            Console.WriteLine($"{type}: {node.Name} [{node.NodeType}] ({node.MimeType})");
        }
    }
    else
    {
        Console.WriteLine($"❌ Failed to retrieve nodes: {result.Message}");
    }

    ColoredText("\r\nFetching nodes recursively by path...", ConsoleColor.Yellow);
    var rrResult = await client.ListDescendantsByPathAsync(
        folderPath: "Projects",
        nodeTypes: "cm:folder,cm:content",
        includeSubFolders: true,
        includeRootNode: true,
        rootNodeId: "-root-");

    if (rrResult.Success)
    {
        Console.WriteLine($"✅ Found {rrResult.Data!.Count} nodes recursively:");
        foreach (var node in rrResult.Data)
        {
            var type = node.IsFolder ? "📁" : "📄";
            Console.WriteLine($"{type} {node.Id} {node.Name} ({node.NodeType}) [Parent: {node.ParentId}]");
        }

        ColoredText("\r\nTree structure:", ConsoleColor.Green);
        var hierarchy = client.BuildNodeHierarchy(rrResult.Data);
        client.PrintTree(hierarchy);
    }
    else
    {
        Console.WriteLine($"❌ Failed to retrieve nodes: {result.Message}");
    }


    ColoredText("\r\nFetching folders recursively by path...", ConsoleColor.Yellow);
    rrResult = await client.ListDescendantsByPathAsync(
        folderPath: "Projects",
        nodeTypes: "cm:folder",
        includeSubFolders: true,
        includeRootNode: true,
        rootNodeId: "-root-");

    if (rrResult.Success)
    {
        Console.WriteLine($"✅ Found {rrResult.Data!.Count} nodes recursively:");
        foreach (var node in rrResult.Data)
        {
            var type = node.IsFolder ? "📁" : "📄";
            Console.WriteLine($"{type} {node.Id} {node.Name} ({node.NodeType}) [Parent: {node.ParentId}]");
        }

        ColoredText("\r\nTree structure:", ConsoleColor.Green);
        var hierarchy = client.BuildNodeHierarchy(rrResult.Data);
        client.PrintTree(hierarchy);
    }
    else
    {
        Console.WriteLine($"❌ Failed to retrieve nodes: {result.Message}");
    }


}
else
{
    Console.WriteLine("Authentication failed.");
}


