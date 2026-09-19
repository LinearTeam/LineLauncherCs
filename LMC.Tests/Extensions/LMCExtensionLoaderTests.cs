using LMC.Extensions.Loading;
using LMC.Extensions.UI;

namespace LMC.Tests.Extensions;

public class LMCExtensionLoaderTests
{
    private readonly LMCExtensionLoader _loader = new(new LMCExtensionPackageExtractor());

    [Fact]
    public void Load_ThrowsWhenEntryAssemblyIsMissing()
    {
        using var scope = new TestFileSystemScope();
        var extensionsDirectory = scope.CreateDirectory("extensions");
        var manifest = ExtensionPackageBuilder.CreateManifest(
            "missing.assembly",
            "1.0.0",
            "missing.dll",
            typeof(ContextCaptureExtension).FullName!);
        var packagePath = ExtensionPackageBuilder.CreatePackage(
            Path.Combine(extensionsDirectory, "missing-assembly.lext"),
            manifest,
            entryType: typeof(ContextCaptureExtension),
            includeEntryAssembly: false);
        var resolvedExtension = ExtensionPackageBuilder.CreateResolvedExtension(
            ExtensionPackageBuilder.CreatePackageInfo(packagePath, manifest));

        var exception = Assert.Throws<FileNotFoundException>(() => _loader.Load(CreateRequest(extensionsDirectory, resolvedExtension)));

        Assert.Contains("Entry assembly", exception.Message);
    }

    [Fact]
    public void Load_ThrowsWhenEntryTypeIsMissing()
    {
        using var scope = new TestFileSystemScope();
        var extensionsDirectory = scope.CreateDirectory("extensions");
        var manifest = ExtensionPackageBuilder.CreateManifest(
            "missing.type",
            "1.0.0",
            Path.GetFileName(typeof(ContextCaptureExtension).Assembly.Location),
            "Missing.Type");
        var packagePath = ExtensionPackageBuilder.CreatePackage(
            Path.Combine(extensionsDirectory, "missing-type.lext"),
            manifest,
            entryType: typeof(ContextCaptureExtension));
        var resolvedExtension = ExtensionPackageBuilder.CreateResolvedExtension(
            ExtensionPackageBuilder.CreatePackageInfo(packagePath, manifest));

        var exception = Assert.Throws<TypeLoadException>(() => _loader.Load(CreateRequest(extensionsDirectory, resolvedExtension)));

        Assert.Contains("Entry type", exception.Message);
    }

    [Fact]
    public void Load_ThrowsWhenEntryTypeDoesNotImplementILMCExtension()
    {
        using var scope = new TestFileSystemScope();
        var extensionsDirectory = scope.CreateDirectory("extensions");
        var manifest = ExtensionPackageBuilder.CreateManifest(
            "invalid.entry",
            "1.0.0",
            Path.GetFileName(typeof(NonExtensionEntryPoint).Assembly.Location),
            typeof(NonExtensionEntryPoint).FullName!);
        var packagePath = ExtensionPackageBuilder.CreatePackage(
            Path.Combine(extensionsDirectory, "invalid-entry.lext"),
            manifest,
            entryType: typeof(NonExtensionEntryPoint));
        var resolvedExtension = ExtensionPackageBuilder.CreateResolvedExtension(
            ExtensionPackageBuilder.CreatePackageInfo(packagePath, manifest));

        var exception = Assert.Throws<InvalidOperationException>(() => _loader.Load(CreateRequest(extensionsDirectory, resolvedExtension)));

        Assert.Contains("does not implement ILMCExtension", exception.Message);
    }

    [Fact]
    public void Load_InitializesExtensionContext()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "LMC.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        var extensionsDirectory = Path.Combine(rootPath, "extensions");
        Directory.CreateDirectory(extensionsDirectory);
        var probePath = Path.Combine(rootPath, "probe.log");

        var dependencyManifest = ExtensionPackageBuilder.CreateManifest(
            "dependency",
            "1.0.0",
            "dependency.dll",
            "Dependency.Extension");
        var dependencyPackage = new LMC.Extensions.Packaging.ExtensionPackageInfo
        {
            PackagePath = "dependency.lext",
            PackageHash = "dependency-hash",
            Manifest = dependencyManifest
        };
        var hardDependency = ExtensionPackageBuilder.CreateResolvedExtension(dependencyPackage);

        var softDependencyManifest = ExtensionPackageBuilder.CreateManifest(
            "soft.dependency",
            "1.5.0",
            "soft.dll",
            "Soft.Dependency");
        var softDependencyPackage = new LMC.Extensions.Packaging.ExtensionPackageInfo
        {
            PackagePath = "soft.lext",
            PackageHash = "soft-hash",
            Manifest = softDependencyManifest
        };
        var softDependency = ExtensionPackageBuilder.CreateResolvedExtension(softDependencyPackage);

        var manifest = ExtensionPackageBuilder.CreateManifest(
            "context.capture",
            "1.0.0",
            Path.GetFileName(typeof(ContextCaptureExtension).Assembly.Location),
            typeof(ContextCaptureExtension).FullName!);
        var packagePath = ExtensionPackageBuilder.CreatePackage(
            Path.Combine(extensionsDirectory, "context-capture.lext"),
            manifest,
            entryType: typeof(ContextCaptureExtension));
        var resolvedExtension = ExtensionPackageBuilder.CreateResolvedExtension(
            ExtensionPackageBuilder.CreatePackageInfo(packagePath, manifest),
            [hardDependency],
            [softDependency]);

        Environment.SetEnvironmentVariable("LMC_TEST_PROBE_PATH", probePath);

        try
        {
            LoadedLMCExtension? loadedExtension = _loader.Load(CreateRequest(
                extensionsDirectory,
                resolvedExtension,
                hardDependencyCacheDirectories: [CreateDirectory(rootPath, "hard-dependency")],
                softDependencyCacheDirectories: [CreateDirectory(rootPath, "soft-dependency")]));

            try
            {
                Assert.NotNull(loadedExtension);
                Assert.Equal("context.capture", loadedExtension.ResolvedExtension.Package.Manifest.Id);

                var lines = File.ReadAllLines(probePath);
                Assert.Contains(lines, line => line.StartsWith("context:context.capture|1.0.0|3.0.0|", StringComparison.Ordinal));
                Assert.Contains("hard:dependency", lines);
                Assert.Contains("soft:soft.dependency", lines);
            }
            finally
            {
                ReleaseLoadContext(ref loadedExtension);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("LMC_TEST_PROBE_PATH", null);
            TryDeleteDirectory(rootPath);
        }
    }

    [Fact]
    public void Load_UsesCurrentAppDomainForSharedHostAssemblies()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "LMC.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        var extensionsDirectory = Path.Combine(rootPath, "extensions");
        Directory.CreateDirectory(extensionsDirectory);
        var manifest = ExtensionPackageBuilder.CreateManifest(
            "smoke.extension",
            "1.0.0",
            Path.GetFileName(typeof(SmokeTestExtension).Assembly.Location),
            typeof(SmokeTestExtension).FullName!);
        var packagePath = ExtensionPackageBuilder.CreatePackage(
            Path.Combine(extensionsDirectory, "smoke-extension.lext"),
            manifest,
            entryType: typeof(SmokeTestExtension));
        var resolvedExtension = ExtensionPackageBuilder.CreateResolvedExtension(
            ExtensionPackageBuilder.CreatePackageInfo(packagePath, manifest));

        LoadedLMCExtension? loadedExtension = _loader.Load(CreateRequest(extensionsDirectory, resolvedExtension));

        try
        {
            Assert.NotNull(loadedExtension);
            Assert.NotNull(loadedExtension.Instance);
            Assert.Equal("smoke.extension", loadedExtension.ResolvedExtension.Package.Manifest.Id);
        }
        finally
        {
            ReleaseLoadContext(ref loadedExtension);
            TryDeleteDirectory(rootPath);
        }
    }

    private static LMCExtensionLoadRequest CreateRequest(
        string extensionsDirectory,
        LMC.Extensions.Resolution.ResolvedExtension resolvedExtension,
        IReadOnlyList<string>? hardDependencyCacheDirectories = null,
        IReadOnlyList<string>? softDependencyCacheDirectories = null)
    {
        return new LMCExtensionLoadRequest
        {
            HostVersion = "3.0.0",
            ExtensionsDirectory = extensionsDirectory,
            ResolvedExtension = resolvedExtension,
            HardDependencyCacheDirectories = hardDependencyCacheDirectories ?? [],
            SoftDependencyCacheDirectories = softDependencyCacheDirectories ?? [],
            LoggerFactory = new TestLoggerFactory(),
            UIApi = new RecordingUIExtensionApi()
        };
    }

    private static void ReleaseLoadContext(ref LoadedLMCExtension? loadedExtension)
    {
        if (loadedExtension == null)
        {
            return;
        }

        var loadContext = loadedExtension.LoadContext;
        loadedExtension = null;
        loadContext.Unload();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static string CreateDirectory(string rootPath, string relativePath)
    {
        var path = Path.Combine(rootPath, relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
