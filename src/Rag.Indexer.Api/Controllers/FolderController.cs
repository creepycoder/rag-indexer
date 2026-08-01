using Microsoft.AspNetCore.Mvc;

namespace Rag.Indexer.Api.Controllers;

[ApiController]
[Route("api")]
public class FolderController : ControllerBase
{
    /// <summary>
    /// GET /api/directories?path= — Lists the subfolders of a folder on the API machine.
    /// With no path (or an empty path), returns the drive/volume roots.
    /// </summary>
    [HttpGet("directories")]
    public IActionResult Directories([FromQuery] string? path = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Ok(new { path = "", parent = (string?)null, directories = GetDriveRoots() });
        }

        if (!Directory.Exists(path))
        {
            return NotFound(new { error = $"Folder not found: {path}" });
        }

        var parent = Directory.GetParent(path)?.FullName;

        var directories = new List<string>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                directories.Add(dir);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Return whatever we could read; the UI surfaces an empty list.
        }

        directories.Sort(StringComparer.OrdinalIgnoreCase);

        return Ok(new { path, parent, directories });
    }

    /// <summary>
    /// GET /api/repositories — The repositories configured for the indexer worker
    /// (Indexing:Repositories), so the UI can offer them as quick choices.
    /// </summary>
    [HttpGet("repositories")]
    public IActionResult Repositories([FromServices] IConfiguration config)
    {
        var repositories = config.GetSection("Indexing:Repositories").Get<string[]>() ?? [];
        return Ok(new { repositories });
    }

    private static List<string> GetDriveRoots()
    {
        var roots = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    roots.Add(drive.RootDirectory.FullName);
                }
                catch (IOException)
                {
                    // Skip drives that cannot be read (empty reader, etc.).
                }
            }
        }
        else
        {
            roots.Add(Path.GetPathRoot(Path.GetFullPath("/")) ?? "/");
        }

        roots.Sort(StringComparer.OrdinalIgnoreCase);
        return roots;
    }
}
