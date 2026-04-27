using GloryCafe.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace GloryCafe.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private const string PublicPrefix = "/uploads/";
    private readonly string _webRootPath;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(string webRootPath, ILogger<LocalFileStorage> logger)
    {
        _webRootPath = webRootPath;
        _logger = logger;
    }

    public async Task<string> SaveAsync(
        Stream content,
        string extension,
        string folder,
        CancellationToken cancellationToken = default)
    {
        var safeFolder = SanitizeSegment(folder);
        var safeExtension = SanitizeExtension(extension);

        var folderAbsolute = Path.Combine(_webRootPath, "uploads", safeFolder);
        Directory.CreateDirectory(folderAbsolute);

        var fileName = $"{Guid.NewGuid():N}{safeExtension}";
        var absolutePath = Path.Combine(folderAbsolute, fileName);

        await using (var fs = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fs, cancellationToken);
        }

        return $"{PublicPrefix}{safeFolder}/{fileName}";
    }

    public Task DeleteAsync(string relativeUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl) || !relativeUrl.StartsWith(PublicPrefix, StringComparison.Ordinal))
            return Task.CompletedTask;

        var relativePath = relativeUrl[PublicPrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.Combine(_webRootPath, "uploads", relativePath);

        var uploadsRoot = Path.GetFullPath(Path.Combine(_webRootPath, "uploads"));
        var resolved = Path.GetFullPath(absolutePath);
        if (!resolved.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Refused to delete path outside uploads root: {Path}", resolved);
            return Task.CompletedTask;
        }

        try
        {
            if (File.Exists(resolved))
                File.Delete(resolved);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete file {Path}", resolved);
        }

        return Task.CompletedTask;
    }

    private static string SanitizeSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            throw new ArgumentException("Folder must not be empty.", nameof(segment));

        foreach (var ch in segment)
        {
            if (!(char.IsLetterOrDigit(ch) || ch is '-' or '_'))
                throw new ArgumentException("Folder may only contain alphanumerics, '-' or '_'.", nameof(segment));
        }
        return segment.ToLowerInvariant();
    }

    private static string SanitizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("Extension must not be empty.", nameof(extension));

        var ext = extension.StartsWith('.') ? extension : "." + extension;
        ext = ext.ToLowerInvariant();

        foreach (var ch in ext.AsSpan(1))
        {
            if (!char.IsLetterOrDigit(ch))
                throw new ArgumentException("Extension may only contain alphanumerics.", nameof(extension));
        }

        return ext;
    }
}
