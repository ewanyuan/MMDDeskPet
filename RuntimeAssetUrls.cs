using System.Globalization;
using System.IO;

namespace MMDDeskPet;

/// <summary>
/// Builds the https://pet.local URLs the page is allowed to fetch.
/// The app files and external models share that host. WebView2's folder
/// mapping serves the host itself and does not raise WebResourceRequested,
/// so the host process streams every response instead of mapping a folder.
/// </summary>
internal static class RuntimeAssetUrls
{
    internal const string AppHost = "pet.local";

    internal static string ForAppFile(string relativePath)
    {
        return Build(relativePath);
    }

    internal static string ForExternalFile(string token, string relativePath)
    {
        var suffix = relativePath.Replace('\\', '/').TrimStart('/');
        return Build("ext/" + token + "/" + suffix);
    }

    internal static bool TryReadExternal(string absolutePath, out string token, out string relativePath)
    {
        token = string.Empty;
        relativePath = string.Empty;
        var decoded = Uri.UnescapeDataString(absolutePath.Replace('\\', '/'));
        if (decoded.StartsWith('/'))
        {
            decoded = decoded[1..];
        }

        const string prefix = "ext/";
        if (!decoded.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = decoded[prefix.Length..];
        var slash = rest.IndexOf('/');
        if (slash <= 0 || slash >= rest.Length - 1)
        {
            return false;
        }

        token = rest[..slash];
        if (token.Contains('.') || token.Contains('/') || token.Contains('\\'))
        {
            return false;
        }

        var segments = rest[(slash + 1)..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment == ".."))
        {
            return false;
        }

        relativePath = string.Join('/', segments.Where(segment => segment != "."));
        return relativePath.Length > 0;
    }

    internal static bool TryReadAppRelative(string absolutePath, out string relativePath)
    {
        relativePath = string.Empty;
        var decoded = Uri.UnescapeDataString(absolutePath.Replace('\\', '/'));
        if (decoded.StartsWith('/'))
        {
            decoded = decoded[1..];
        }

        if (decoded.StartsWith("ext/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = decoded.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment == ".."))
        {
            return false;
        }

        relativePath = string.Join('/', segments.Where(segment => segment != "."));
        return relativePath.Length > 0;
    }

    internal static string ContentTypeFor(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" or ".htm" => "text/html",
            ".js" => "text/javascript",
            ".css" => "text/css",
            ".json" or ".map" => "application/json",
            ".wasm" => "application/wasm",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".tga" => "image/x-tga",
            ".dds" or ".spa" or ".sph" or ".pmx" or ".pmd" or ".vmd" or ".vpd" => "application/octet-stream",
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".ogg" => "audio/ogg",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".flac" => "audio/flac",
            ".mp4" => "video/mp4",
            _ => "application/octet-stream",
        };
    }

    private static string Build(string relativePath)
    {
        var encoded = string.Join(
            "/",
            relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
        return "https://" + AppHost + "/" + encoded;
    }
}

internal sealed class ExternalFileMap
{
    private readonly Dictionary<string, string> _roots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _tokens = new(StringComparer.OrdinalIgnoreCase);

    internal string TokenForDirectory(string directory, out bool created)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (_tokens.TryGetValue(full, out var existing))
        {
            created = false;
            return existing;
        }

        var token = _roots.Count.ToString(CultureInfo.InvariantCulture);
        _roots[token] = full;
        _tokens[full] = token;
        created = true;
        return token;
    }

    internal string UrlForFile(string absoluteFile, out string token, out bool created)
    {
        var full = Path.GetFullPath(absoluteFile);
        var directory = Path.GetDirectoryName(full)
            ?? throw new InvalidOperationException("The selected file does not have a directory.");
        token = TokenForDirectory(directory, out created);
        return RuntimeAssetUrls.ForExternalFile(token, Path.GetFileName(full));
    }

    internal bool TryResolve(string absolutePath, out string filePath)
    {
        filePath = string.Empty;
        if (!RuntimeAssetUrls.TryReadExternal(absolutePath, out var token, out var relative))
        {
            return false;
        }

        if (!_roots.TryGetValue(token, out var root))
        {
            return false;
        }

        var combined = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsInsideDirectory(root, combined))
        {
            return false;
        }

        filePath = combined;
        return true;
    }

    internal static bool IsInsideDirectory(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath);
        if (Path.IsPathRooted(relative))
        {
            return false;
        }

        var firstSegment = relative
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return firstSegment is not null && !firstSegment.Equals("..", StringComparison.Ordinal);
    }
}
