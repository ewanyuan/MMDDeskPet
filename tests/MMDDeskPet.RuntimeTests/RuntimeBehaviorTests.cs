using System.Text.Json;
using Xunit;

namespace MMDDeskPet.RuntimeTests;

public class RuntimeBehaviorTests
{
    [Fact]
    public async Task CompletionMatchesRequestIdAndResponseType()
    {
        var gate = new RuntimeRequestGate();
        var request = gate.Begin("load-character", "model-loaded", supersedePreviousLoad: true);
        using var document = JsonDocument.Parse("{\"type\":\"model-loaded\",\"requestId\":\"" + request.RequestId + "\"}");

        Assert.False(gate.TryComplete("motion-loaded", request.RequestId, document.RootElement));
        Assert.False(gate.TryComplete("model-loaded", "other-id", document.RootElement));
        Assert.True(gate.TryComplete("model-loaded", request.RequestId, document.RootElement));
        Assert.Equal(0, gate.PendingCount);
        var completed = await request.Completion.Task;
        Assert.Equal(request.RequestId, completed.GetProperty("requestId").GetString());
    }

    [Fact]
    public async Task NewerLoadCancelsThePreviousOne()
    {
        var gate = new RuntimeRequestGate();
        var first = gate.Begin("load-character", "model-loaded", supersedePreviousLoad: true);
        var second = gate.Begin("load-character", "model-loaded", supersedePreviousLoad: true);

        Assert.True(first.Superseded);
        Assert.Equal("superseded-by-load-character", first.CancelReason);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await first.Completion.Task);

        using var document = JsonDocument.Parse("{\"ok\":true}");
        Assert.False(gate.TryComplete("model-loaded", first.RequestId, document.RootElement));
        Assert.True(gate.TryComplete("model-loaded", second.RequestId, document.RootElement));
        Assert.True(second.Completion.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task RuntimeErrorCompletesOnlyTheMatchingRequest()
    {
        var gate = new RuntimeRequestGate();
        var load = gate.Begin("load-character", "model-loaded", supersedePreviousLoad: true);
        var other = gate.Begin("set-morph", "morph-updated", supersedePreviousLoad: false);
        var error = new InvalidOperationException(
            "Unable to load from https://ext0.local/Mikoto.physics-stable.pmx: LoadFileError: Error status: 0");

        var failed = gate.FailByRuntimeError(load.RequestId, error);

        Assert.Single(failed);
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await load.Completion.Task);
        Assert.Contains("status: 0", thrown.Message, StringComparison.Ordinal);
        Assert.Equal(1, gate.PendingCount);
        Assert.False(other.Completion.Task.IsCompleted);

        gate.FailByRuntimeError(null, new InvalidOperationException("window error"));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await other.Completion.Task);
        Assert.Equal(0, gate.PendingCount);
    }

    [Fact]
    public async Task CancelAllReleasesEveryPendingRequest()
    {
        var gate = new RuntimeRequestGate();
        var request = gate.Begin("load-motion", "motion-loaded", supersedePreviousLoad: true);
        var pending = gate.CancelAll("navigation-reset");

        Assert.Single(pending);
        Assert.Equal("navigation-reset", request.CancelReason);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await request.Completion.Task);
        Assert.Equal(0, gate.PendingCount);
    }

    [Fact]
    public void StartupDoesNotAutoLoadABundledModel()
    {
        Assert.False(StartupModelPolicy.ShouldAutoLoadBundledModel);
        Assert.False(StartupModelPolicy.ShouldRestoreLastModel(null, _ => true));
        Assert.False(StartupModelPolicy.ShouldRestoreLastModel("  ", _ => true));
        Assert.False(StartupModelPolicy.ShouldRestoreLastModel(@"C:\models\missing.pmx", _ => false));
        Assert.True(StartupModelPolicy.ShouldRestoreLastModel(
            @"C:\models\chosen.pmx",
            path => path.EndsWith("chosen.pmx", StringComparison.Ordinal)));
        Assert.False(StartupModelPolicy.ShouldLoadFirstLibraryModel(@"C:\models\chosen.pmx", _ => true, 3));
        Assert.True(StartupModelPolicy.ShouldLoadFirstLibraryModel(@"C:\models\missing.pmx", _ => false, 1));
        Assert.True(StartupModelPolicy.ShouldLoadFirstLibraryModel(null, _ => false, 2));
        Assert.False(StartupModelPolicy.ShouldLoadFirstLibraryModel(null, _ => false, 0));
        Assert.True(StartupModelPolicy.ShouldLoadFirstLibraryModel("  ", _ => true, 4));
    }

    [Fact]
    public void ExternalUrlsStayOnThePageOriginAndRoundTripSpecialNames()
    {
        var spaced = RuntimeAssetUrls.ForExternalFile("0", "desk pet.pmx");
        var nested = RuntimeAssetUrls.ForExternalFile("0", @"tex\肌 色.png");
        var ampersand = RuntimeAssetUrls.ForExternalFile("0", "clothes/Hime_&_Mikoto.png");

        Assert.Equal("https://pet.local/ext/0/desk%20pet.pmx", spaced);
        Assert.Equal("https://pet.local/ext/0/tex/%E8%82%8C%20%E8%89%B2.png", nested);
        Assert.Equal("https://pet.local/ext/0/clothes/Hime_%26_Mikoto.png", ampersand);
        Assert.DoesNotContain("ext0.local", spaced, StringComparison.Ordinal);
        Assert.DoesNotContain("ext0.local", nested, StringComparison.Ordinal);

        AssertRoundTrip(spaced, "0", "desk pet.pmx");
        AssertRoundTrip(nested, "0", "tex/肌 色.png");
        AssertRoundTrip(ampersand, "0", "clothes/Hime_&_Mikoto.png");
        Assert.False(RuntimeAssetUrls.TryReadExternal("/ext/0/../secret.pmx", out _, out _));
        Assert.Equal("https://pet.local/web/index.html", RuntimeAssetUrls.ForAppFile("web/index.html"));
    }

    [Fact]
    public void ExternalMapResolvesTexturesUnderFoldersWithAmpersands()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmddeskpet-url-" + Guid.NewGuid().ToString("N"), "Hime_&_Mikoto");
        Directory.CreateDirectory(Path.Combine(root, "tex"));
        Directory.CreateDirectory(Path.Combine(root, "clothes"));
        var model = Path.Combine(root, "desk pet.pmx");
        var skin = Path.Combine(root, "tex", "肌 色.png");
        var outfit = Path.Combine(root, "clothes", "Hime_&_Mikoto.png");
        File.WriteAllBytes(model, [1]);
        File.WriteAllBytes(skin, [2]);
        File.WriteAllBytes(outfit, [3]);

        try
        {
            var map = new ExternalFileMap();
            var modelUrl = map.UrlForFile(model, out var token, out var created);
            Assert.Equal("0", token);
            Assert.True(created);
            Assert.StartsWith("https://pet.local/ext/0/", modelUrl, StringComparison.Ordinal);

            Assert.True(map.TryResolve(new Uri(modelUrl).AbsolutePath, out var resolvedModel));
            Assert.True(map.TryResolve(new Uri(RuntimeAssetUrls.ForExternalFile(token, "tex/肌 色.png")).AbsolutePath, out var resolvedSkin));
            Assert.True(map.TryResolve(new Uri(RuntimeAssetUrls.ForExternalFile(token, "clothes/Hime_&_Mikoto.png")).AbsolutePath, out var resolvedOutfit));
            Assert.Equal(Path.GetFullPath(model), resolvedModel);
            Assert.Equal(Path.GetFullPath(skin), resolvedSkin);
            Assert.Equal(Path.GetFullPath(outfit), resolvedOutfit);
            Assert.True(File.Exists(resolvedSkin));
            Assert.True(File.Exists(resolvedOutfit));

            Assert.False(map.TryResolve("/ext/0/missing.pmx", out var missing) && File.Exists(missing));
            Assert.False(map.TryResolve("/ext/1/desk%20pet.pmx", out _));
            Assert.False(map.TryResolve("/ext/0/../../etc/passwd", out _));

            var again = map.UrlForFile(model, out var sameToken, out var createdAgain);
            Assert.Equal(token, sameToken);
            Assert.False(createdAgain);
            Assert.Equal(modelUrl, again);
        }
        finally
        {
            Directory.Delete(Directory.GetParent(root)!.FullName, recursive: true);
        }
    }

    [Fact]
    public void LaunchOptionsParseSmokeArguments()
    {
        Assert.True(LaunchOptions.TryParse(
            ["--model", @"C:\models\Hime_&_Mikoto\desk pet.pmx", @"--motion=C:\motions\dance.vmd", "--smoke-test", "--exit-after", "45"],
            out var options,
            out var error));
        Assert.Null(error);
        Assert.Equal(@"C:\models\Hime_&_Mikoto\desk pet.pmx", options.ModelPath);
        Assert.Equal(@"C:\motions\dance.vmd", options.MotionPath);
        Assert.True(options.SmokeTest);
        Assert.Equal(45, options.ExitAfterSeconds);
        Assert.False(options.SelfTest);

        Assert.False(LaunchOptions.TryParse(["--smoke-test", "--model"], out _, out var missing));
        Assert.Contains("--model", missing, StringComparison.Ordinal);
        Assert.False(LaunchOptions.TryParse(["--exit-after", "0"], out _, out var badSeconds));
        Assert.Contains("--exit-after", badSeconds, StringComparison.Ordinal);

        Assert.True(LaunchOptions.TryParse(
            ["--library", @"D:\pet lib\Hime_&_Mikoto", "--smoke-test"],
            out var libraryOptions,
            out var libraryError));
        Assert.Null(libraryError);
        Assert.Equal(@"D:\pet lib\Hime_&_Mikoto", libraryOptions.LibraryFolder);
        Assert.True(libraryOptions.SmokeTest);
        Assert.Null(libraryOptions.ModelPath);

        Assert.True(LaunchOptions.TryParse([@"--library=E:\motions"], out var libraryEquals, out _));
        Assert.Equal(@"E:\motions", libraryEquals.LibraryFolder);
        Assert.False(LaunchOptions.TryParse(["--smoke-test", "--library"], out _, out var missingLibrary));
        Assert.Contains("--library", missingLibrary, StringComparison.Ordinal);
    }

    [Fact]
    public void LibraryScannerListsEveryModelSortedByFolderThenName()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmddeskpet-lib-" + Guid.NewGuid().ToString("N"));
        var models = Path.Combine(root, "models");
        var deep = Path.Combine(models, "a", "b", "c", "d");
        var tooDeep = Path.Combine(deep, "e");
        var hime = Path.Combine(models, "Hime_&_Mikoto");
        var spaced = Path.Combine(models, "b folder");
        Directory.CreateDirectory(Path.Combine(models, "A"));
        Directory.CreateDirectory(deep);
        Directory.CreateDirectory(tooDeep);
        Directory.CreateDirectory(hime);
        Directory.CreateDirectory(spaced);
        Directory.CreateDirectory(Path.Combine(root, "motions", "sub"));
        Directory.CreateDirectory(Path.Combine(root, "music"));

        File.WriteAllText(Path.Combine(models, "README.txt"), "do not list");
        File.WriteAllBytes(Path.Combine(models, "zeta.pmx"), [1]);
        File.WriteAllBytes(Path.Combine(models, "A", "Upper.PMX"), [1]);
        File.WriteAllBytes(Path.Combine(hime, "Hime.physics-stable.pmx"), [2]);
        File.WriteAllBytes(Path.Combine(hime, "初音 ミク.pmx"), [3]);
        File.WriteAllBytes(Path.Combine(deep, "deep.pmx"), [4]);
        File.WriteAllBytes(Path.Combine(tooDeep, "too-deep.pmx"), [5]);
        File.WriteAllBytes(Path.Combine(spaced, "second.pmx"), [6]);
        File.WriteAllBytes(Path.Combine(root, "motions", "wave.vmd"), [7]);
        File.WriteAllBytes(Path.Combine(root, "motions", "sub", "ダンス.vmd"), [8]);
        File.WriteAllBytes(Path.Combine(root, "music", "song.mp3"), [9]);
        File.WriteAllBytes(Path.Combine(root, "music", "tone.wav"), [10]);
        File.WriteAllBytes(Path.Combine(root, "music", "clip.ogg"), [11]);
        File.WriteAllBytes(Path.Combine(root, "music", "skip.m4a"), [12]);
        File.WriteAllBytes(Path.Combine(root, "notes.pmx"), [13]);

        try
        {
            var library = ModelLibraryScanner.Scan(root);
            Assert.Equal(
            [
                "zeta.pmx",
                "A/Upper.PMX",
                "a/b/c/d/deep.pmx",
                "b folder/second.pmx",
                "Hime_&_Mikoto/Hime.physics-stable.pmx",
                "Hime_&_Mikoto/初音 ミク.pmx",
            ],
            library.Models.Select(file => file.DisplayName).ToArray());
            Assert.Contains(library.Models, file => file.DisplayName.Contains("Hime_&_Mikoto", StringComparison.Ordinal));
            Assert.Contains(library.Models, file => file.DisplayName.Contains("初音 ミク", StringComparison.Ordinal));
            Assert.Contains(library.Models, file => file.DisplayName.Contains("physics-stable", StringComparison.Ordinal));
            Assert.DoesNotContain(library.Models, file => file.DisplayName.Contains("too-deep", StringComparison.Ordinal));
            Assert.DoesNotContain(library.Models, file => file.DisplayName.Contains("README", StringComparison.Ordinal));
            Assert.DoesNotContain(library.Models, file => file.DisplayName.Contains("notes", StringComparison.Ordinal));
            Assert.All(library.Models, file => Assert.True(File.Exists(file.FullPath)));
            Assert.Equal(
                ["wave.vmd", "sub/ダンス.vmd"],
                library.Motions.Select(file => file.DisplayName).ToArray());
            Assert.Equal(
                ["clip.ogg", "song.mp3", "tone.wav"],
                library.Music.Select(file => file.DisplayName).ToArray());
            Assert.Equal(
                "Hime_&&_Mikoto/初音 ミク.pmx",
                ModelLibraryScanner.EscapeMenuHeader("Hime_&_Mikoto/初音 ミク.pmx"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DefaultPickIsTheShortestNameInTheFirstFolder()
    {
        var models = new LibraryFile[]
        {
            Named("Zzz/a.pmx"),
            Named("Hime_260426/Hime.physics-max.pmx"),
            Named("Hime_260426/Hime.pmx"),
            Named("Hime_260426/Hime.physics-stable.pmx"),
            Named("Hime_260426/Hime.physics-balanced.pmx"),
        };
        Assert.Equal("Hime_260426/Hime.pmx", ModelLibraryScanner.SelectDefault(models)!.DisplayName);

        var sameLength = new LibraryFile[]
        {
            Named("pack/cat.pmx"),
            Named("pack/cab.pmx"),
            Named("pack/bat.pmx"),
        };
        Assert.Equal("pack/bat.pmx", ModelLibraryScanner.SelectDefault(sameLength)!.DisplayName);

        var motions = new LibraryFile[]
        {
            Named("zzz/a.vmd"),
            Named("waist-dance/waist-dance-camera.vmd"),
            Named("waist-dance/waist-dance.vmd"),
        };
        Assert.Equal("waist-dance/waist-dance.vmd", ModelLibraryScanner.SelectDefault(motions)!.DisplayName);
        Assert.Null(ModelLibraryScanner.SelectDefault([]));

        var root = Path.Combine(Path.GetTempPath(), "mmddeskpet-lib-default-" + Guid.NewGuid().ToString("N"));
        var hime = Path.Combine(root, "models", "Hime_260426");
        var later = Path.Combine(root, "models", "Zzz");
        var dance = Path.Combine(root, "motions", "waist-dance");
        var otherMotion = Path.Combine(root, "motions", "zzz");
        Directory.CreateDirectory(hime);
        Directory.CreateDirectory(later);
        Directory.CreateDirectory(dance);
        Directory.CreateDirectory(otherMotion);
        foreach (var name in new[] { "Hime.physics-balanced.pmx", "Hime.physics-max.pmx", "Hime.physics-stable.pmx", "Hime.pmx" })
        {
            File.WriteAllBytes(Path.Combine(hime, name), [1]);
        }

        File.WriteAllBytes(Path.Combine(later, "a.pmx"), [1]);
        File.WriteAllBytes(Path.Combine(dance, "waist-dance-camera.vmd"), [1]);
        File.WriteAllBytes(Path.Combine(dance, "waist-dance.vmd"), [1]);
        File.WriteAllBytes(Path.Combine(otherMotion, "a.vmd"), [1]);

        try
        {
            var library = ModelLibraryScanner.Scan(root);
            Assert.Equal(
            [
                "Hime_260426/Hime.physics-balanced.pmx",
                "Hime_260426/Hime.physics-max.pmx",
                "Hime_260426/Hime.physics-stable.pmx",
                "Hime_260426/Hime.pmx",
                "Zzz/a.pmx",
            ],
            library.Models.Select(file => file.DisplayName).ToArray());
            Assert.Equal(
            [
                "waist-dance/waist-dance-camera.vmd",
                "waist-dance/waist-dance.vmd",
                "zzz/a.vmd",
            ],
            library.Motions.Select(file => file.DisplayName).ToArray());
            Assert.Equal("Hime_260426/Hime.pmx", ModelLibraryScanner.SelectDefault(library.Models)!.DisplayName);
            Assert.Equal("waist-dance/waist-dance.vmd", ModelLibraryScanner.SelectDefault(library.Motions)!.DisplayName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static LibraryFile Named(string displayName)
    {
        return new LibraryFile
        {
            FullPath = displayName,
            DisplayName = displayName,
        };
    }

    [Fact]
    public void LibraryFoldersAreCreatedOnceAndBlankRootMeansTheExeFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmddeskpet-lib-empty-" + Guid.NewGuid().ToString("N"));
        var exe = Path.Combine(Path.GetTempPath(), "mmddeskpet-exe-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Empty(ModelLibraryScanner.Scan(root).Models);
            Assert.Empty(ModelLibraryScanner.Scan(root).Motions);
            Assert.Empty(ModelLibraryScanner.Scan(root).Music);

            ModelLibraryScanner.EnsureFolders(root);
            Assert.Equal(
                ModelLibraryScanner.ModelsReadme,
                File.ReadAllText(Path.Combine(root, "models", "README.txt")));
            Assert.Contains("VMD", File.ReadAllText(Path.Combine(root, "motions", "README.txt")), StringComparison.Ordinal);
            Assert.Contains("动作", File.ReadAllText(Path.Combine(root, "motions", "README.txt")), StringComparison.Ordinal);
            Assert.Contains("mp3", File.ReadAllText(Path.Combine(root, "music", "README.txt")), StringComparison.Ordinal);
            Assert.Contains("音乐", File.ReadAllText(Path.Combine(root, "music", "README.txt")), StringComparison.Ordinal);
            Assert.Empty(ModelLibraryScanner.Scan(root).Models);

            File.WriteAllText(Path.Combine(root, "models", "README.txt"), "custom");
            ModelLibraryScanner.EnsureFolders(root);
            Assert.Equal("custom", File.ReadAllText(Path.Combine(root, "models", "README.txt")));

            Assert.Equal(Path.GetFullPath(exe), ModelLibraryScanner.ResolveRoot(null, exe));
            Assert.Equal(Path.GetFullPath(exe), ModelLibraryScanner.ResolveRoot("   ", exe));
            var custom = Path.Combine(root, "elsewhere");
            Assert.Equal(Path.GetFullPath(custom), ModelLibraryScanner.ResolveRoot(custom, exe));
            Assert.False(string.IsNullOrWhiteSpace(ModelLibraryScanner.ExeDirectory()));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void AppAndExternalPathsStayDistinct()
    {
        Assert.True(RuntimeAssetUrls.TryReadAppRelative("/web/index.html", out var index));
        Assert.Equal("web/index.html", index);
        Assert.True(RuntimeAssetUrls.TryReadAppRelative("/web/mmd-spr.wasm", out var wasm));
        Assert.Equal("web/mmd-spr.wasm", wasm);
        Assert.False(RuntimeAssetUrls.TryReadAppRelative("/ext/0/Hime.pmx", out _));
        Assert.False(RuntimeAssetUrls.TryReadAppRelative("/web/../../secret.txt", out _));
        Assert.Equal("application/wasm", RuntimeAssetUrls.ContentTypeFor("mmd-spr.wasm"));
        Assert.Equal("text/javascript", RuntimeAssetUrls.ContentTypeFor("mmd.bundle.js"));
        Assert.Equal("text/html", RuntimeAssetUrls.ContentTypeFor("index.html"));
        Assert.Equal("application/octet-stream", RuntimeAssetUrls.ContentTypeFor("Hime.pmx"));
        Assert.Equal("application/octet-stream", RuntimeAssetUrls.ContentTypeFor("wave.vmd"));
        Assert.Equal("application/octet-stream", RuntimeAssetUrls.ContentTypeFor("cloth.spa"));
        Assert.Equal("application/octet-stream", RuntimeAssetUrls.ContentTypeFor("cloth.sph"));
        Assert.Equal("application/octet-stream", RuntimeAssetUrls.ContentTypeFor("skin.dds"));
        Assert.Equal("image/x-tga", RuntimeAssetUrls.ContentTypeFor("skin.tga"));
        Assert.Equal("image/png", RuntimeAssetUrls.ContentTypeFor("skin.png"));
        Assert.Equal("image/jpeg", RuntimeAssetUrls.ContentTypeFor("skin.jpg"));
        Assert.Equal("image/bmp", RuntimeAssetUrls.ContentTypeFor("skin.bmp"));
        Assert.Equal("audio/mpeg", RuntimeAssetUrls.ContentTypeFor("song.mp3"));
        Assert.Equal("audio/wav", RuntimeAssetUrls.ContentTypeFor("song.wav"));
        Assert.Equal("audio/ogg", RuntimeAssetUrls.ContentTypeFor("song.ogg"));
        Assert.Equal("application/json", RuntimeAssetUrls.ContentTypeFor("settings.json"));
    }

    [Fact]
    public void LargeModelStaysAFileStream()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmddeskpet-large-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var model = Path.Combine(root, "Hime.pmx");
        const long size = 20L * 1024 * 1024;
        using (var created = new FileStream(model, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            created.SetLength(size);
        }

        try
        {
            var map = new ExternalFileMap();
            var url = map.UrlForFile(model, out var token, out _);
            Assert.Equal("0", token);
            Assert.Equal("https://pet.local/ext/0/Hime.pmx", url);
            Assert.True(map.TryResolve(new Uri(url).AbsolutePath, out var resolved));
            using var stream = new FileStream(
                resolved,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                64 * 1024,
                FileOptions.SequentialScan);
            Assert.Equal(size, stream.Length);
            Assert.Equal(0, stream.Position);
            Assert.Equal("application/octet-stream", RuntimeAssetUrls.ContentTypeFor(resolved));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertRoundTrip(string url, string token, string relativePath)
    {
        var uri = new Uri(url);
        Assert.Equal(RuntimeAssetUrls.AppHost, uri.IdnHost);
        Assert.True(RuntimeAssetUrls.TryReadExternal(uri.AbsolutePath, out var readToken, out var readRelative));
        Assert.Equal(token, readToken);
        Assert.Equal(relativePath, readRelative);
    }
}
