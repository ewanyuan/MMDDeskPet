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
