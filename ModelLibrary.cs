using System.IO;

namespace MMDDeskPet;

internal sealed class LibraryFile
{
    public required string FullPath { get; init; }

    public required string DisplayName { get; init; }
}

internal sealed class ModelLibrary
{
    public static ModelLibrary Empty { get; } = new();

    public IReadOnlyList<LibraryFile> Models { get; init; } = [];

    public IReadOnlyList<LibraryFile> Motions { get; init; } = [];

    public IReadOnlyList<LibraryFile> Music { get; init; } = [];
}

internal static class ModelLibraryScanner
{
    // Depth 4 reaches models/a/b/c/d/file.pmx and stops before the next folder.
    public const int MaxDepth = 4;

    public const string ModelsFolderName = "models";

    public const string MotionsFolderName = "motions";

    public const string MusicFolderName = "music";

    public const string ReadmeFileName = "README.txt";

    public const string ModelsReadme =
        """
        把 PMX 放进这个文件夹。每个模型可以单独放在子文件夹里，并带上它的贴图。
        动作（VMD）放进 motions 文件夹，音乐（mp3、wav、ogg）放进 music 文件夹。

        Put PMX files in this folder. Each model may live in its own subfolder together with its textures.
        Put VMD motions in the motions folder, and mp3, wav, or ogg audio in the music folder.
        """;

    public const string MotionsReadme =
        """
        把 VMD 动作文件放进这个文件夹。子文件夹也可以。

        Put VMD motion files in this folder. Subfolders are fine.
        """;

    public const string MusicReadme =
        """
        把 mp3、wav 或 ogg 音乐放进这个文件夹。

        Put mp3, wav, or ogg music in this folder.
        """;

    private static readonly string[] ModelExtensions = [".pmx"];

    private static readonly string[] MotionExtensions = [".vmd"];

    private static readonly string[] MusicExtensions = [".mp3", ".wav", ".ogg"];

    public static string ExeDirectory()
    {
        return NormalizeDirectory(AppContext.BaseDirectory);
    }

    public static string ResolveRoot(string? configured, string exeDirectory)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return NormalizeDirectory(exeDirectory);
        }

        return NormalizeDirectory(configured);
    }

    public static string ModelsDirectory(string libraryRoot)
    {
        return Path.Combine(libraryRoot, ModelsFolderName);
    }

    public static string MotionsDirectory(string libraryRoot)
    {
        return Path.Combine(libraryRoot, MotionsFolderName);
    }

    public static string MusicDirectory(string libraryRoot)
    {
        return Path.Combine(libraryRoot, MusicFolderName);
    }

    public static IReadOnlyList<string> LibraryDirectories(string libraryRoot)
    {
        return
        [
            ModelsDirectory(libraryRoot),
            MotionsDirectory(libraryRoot),
            MusicDirectory(libraryRoot),
        ];
    }

    public static void EnsureFolders(string libraryRoot)
    {
        WriteReadmeIfMissing(ModelsDirectory(libraryRoot), ModelsReadme);
        WriteReadmeIfMissing(MotionsDirectory(libraryRoot), MotionsReadme);
        WriteReadmeIfMissing(MusicDirectory(libraryRoot), MusicReadme);
    }

    public static ModelLibrary Scan(string libraryRoot)
    {
        return new ModelLibrary
        {
            Models = ScanFolder(ModelsDirectory(libraryRoot), ModelExtensions),
            Motions = ScanFolder(MotionsDirectory(libraryRoot), MotionExtensions),
            Music = ScanFolder(MusicDirectory(libraryRoot), MusicExtensions),
        };
    }

    public static string EscapeMenuHeader(string header)
    {
        return header.Replace("&", "&&", StringComparison.Ordinal);
    }

    public static LibraryFile? SelectDefault(IReadOnlyList<LibraryFile> files, bool preferLoopName = false)
    {
        if (files.Count == 0)
        {
            return null;
        }

        var firstFolder = DirectoryOf(files[0].DisplayName);
        for (var index = 1; index < files.Count; index++)
        {
            var folder = DirectoryOf(files[index].DisplayName);
            if (string.Compare(folder, firstFolder, StringComparison.OrdinalIgnoreCase) < 0)
            {
                firstFolder = folder;
            }
        }

        var preferLoop = false;
        if (preferLoopName)
        {
            foreach (var file in files)
            {
                if (string.Equals(DirectoryOf(file.DisplayName), firstFolder, StringComparison.OrdinalIgnoreCase)
                    && NameContainsLoop(file))
                {
                    preferLoop = true;
                    break;
                }
            }
        }

        LibraryFile? selected = null;
        foreach (var file in files)
        {
            if (!string.Equals(DirectoryOf(file.DisplayName), firstFolder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (preferLoop && !NameContainsLoop(file))
            {
                continue;
            }

            if (selected is null || CompareDefaultName(file, selected) < 0)
            {
                selected = file;
            }
        }

        return selected;
    }

    private static bool NameContainsLoop(LibraryFile file)
    {
        return Path.GetFileName(file.DisplayName).Contains("loop", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteReadmeIfMissing(string directory, string readme)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, ReadmeFileName);
        if (!File.Exists(path))
        {
            File.WriteAllText(path, readme);
        }
    }

    private static IReadOnlyList<LibraryFile> ScanFolder(string folder, IReadOnlyList<string> extensions)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = MaxDepth,
            IgnoreInaccessible = true,
            AttributesToSkip = 0,
            ReturnSpecialDirectories = false,
        };

        var files = new List<LibraryFile>();
        foreach (var path in Directory.EnumerateFiles(folder, "*", options))
        {
            var extension = Path.GetExtension(path);
            var listed = false;
            foreach (var candidate in extensions)
            {
                if (extension.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    listed = true;
                    break;
                }
            }

            if (!listed)
            {
                continue;
            }

            var relative = Path.GetRelativePath(folder, path).Replace('\\', '/');
            files.Add(new LibraryFile
            {
                FullPath = Path.GetFullPath(path),
                DisplayName = relative,
            });
        }

        files.Sort(CompareByFolderThenName);
        return files;
    }

    private static int CompareByFolderThenName(LibraryFile left, LibraryFile right)
    {
        var directory = string.Compare(
            DirectoryOf(left.DisplayName),
            DirectoryOf(right.DisplayName),
            StringComparison.OrdinalIgnoreCase);
        if (directory != 0)
        {
            return directory;
        }

        return string.Compare(
            Path.GetFileName(left.DisplayName),
            Path.GetFileName(right.DisplayName),
            StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareDefaultName(LibraryFile left, LibraryFile right)
    {
        var leftName = Path.GetFileNameWithoutExtension(left.DisplayName);
        var rightName = Path.GetFileNameWithoutExtension(right.DisplayName);
        var length = leftName.Length.CompareTo(rightName.Length);
        if (length != 0)
        {
            return length;
        }

        return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
    }

    private static string DirectoryOf(string displayName)
    {
        var slash = displayName.LastIndexOf('/');
        return slash < 0 ? string.Empty : displayName[..slash];
    }

    private static string NormalizeDirectory(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
