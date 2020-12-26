namespace BlazorRepl.Core.PackageInstallation
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using NuGet.Common;
    using NuGet.DependencyResolver;
    using NuGet.Frameworks;
    using NuGet.LibraryModel;
    using NuGet.Protocol.Core.Types;
    using NuGet.RuntimeModel;
    using NuGet.Versioning;

    public class PackageManager
    {
        private readonly RemoteDependencyWalker dependencyWalker;
        private readonly RemoteDependencyProvider dependencyProvider;
        private readonly HttpClient httpClient;

        public PackageManager(HttpClient httpClient)
        {
            // TODO: DI
            // TODO: use factory for http
            this.dependencyProvider = new RemoteDependencyProvider(new HttpClient(), new ConcurrentDictionary<string, LibraryDependencyInfo>());

            var cache = new NullSourceCacheContext();
            var logger = new NullLogger();
            var ctx = new RemoteWalkContext(cache, logger);
            ctx.RemoteLibraryProviders.Add(this.dependencyProvider);

            // it should be scoped
            this.dependencyWalker = new RemoteDependencyWalker(ctx);
            this.httpClient = httpClient;
        }

        public async Task InstallPackage(string packageName, string packageVersion)
        {
            var libraryRange = new LibraryRange(
                packageName,
                new VersionRange(new NuGetVersion(packageVersion)),
                LibraryDependencyTarget.Package);
            var framework = FrameworkConstants.CommonFrameworks.Net50;
            var graph = new RuntimeGraph(new[] { new RuntimeDescription(framework.DotNetFrameworkName) }); // TODO: do we need that?

            try
            {
                await this.dependencyWalker.WalkAsync(
                    libraryRange,
                    framework,
                    framework.DotNetFrameworkName, // check if it actually returns net5.0
                    graph,
                    recursive: true);

                var packageContents = new Dictionary<string, byte[]>();

                foreach (var package in this.dependencyProvider.PackagesToInstall)
                {
                    var packageBytes = await this.httpClient.GetByteArrayAsync(
                        $"https://api.nuget.org/v3-flatcontainer/{package.Library.Name}/{package.Library.Version}/{package.Library.Name}.{package.Library.Version}.nupkg");

                    using var zippedStream = new MemoryStream(packageBytes);
                    using var archive = new ZipArchive(zippedStream);

                    var dlls = ExtractDlls(archive.Entries, package.Framework);
                    foreach (var (key, value) in dlls)
                    {
                        packageContents.Add(key, value);
                    }

                    var scripts = ExtractStaticContents(archive.Entries, ".js");
                    foreach (var (key, value) in scripts)
                    {
                        packageContents.Add(key, value);
                    }

                    var styles = ExtractStaticContents(archive.Entries, ".css");
                    foreach (var (key, value) in styles)
                    {
                        packageContents.Add(key, value);
                    }
                }
            }
            finally
            {
                this.dependencyProvider.ClearPackagesToInstall();
            }
        }

        private static IDictionary<string, byte[]> ExtractDlls(IEnumerable<ZipArchiveEntry> entries, NuGetFramework framework)
        {
            // sometimes the packages comes with folder name netcoreapp5.0 instead of net5.0
            var targetFrameworkFolderNames = framework == FrameworkConstants.CommonFrameworks.Net50
                ? new[] { framework.GetShortFolderName(), "netcoreapp5.0" }
                : new[] { framework.GetShortFolderName() };

            var dllEntries = entries
                .Where(e =>
                    Path.GetExtension(e.FullName) == ".dll" &&
                    targetFrameworkFolderNames.Contains(Path.GetDirectoryName(e.FullName).Replace("lib\\", string.Empty)))
                .ToList();

            return GetEntriesContent(entries);
        }

        private static IDictionary<string, byte[]> ExtractStaticContents(IEnumerable<ZipArchiveEntry> entries, string extension)
        {
            var staticContentEntries = entries.Where(e => Path.GetExtension(e.Name) == extension);

            return GetEntriesContent(entries);
        }

        private static IDictionary<string, byte[]> GetEntriesContent(IEnumerable<ZipArchiveEntry> entries)
        {
            var result = new Dictionary<string, byte[]>();
            foreach (var entry in entries)
            {
                using var memoryStream = new MemoryStream();
                using var entryStream = entry.Open();
                entryStream.CopyTo(memoryStream);

                var entryBytes = memoryStream.ToArray();

                result.Add(entry.Name, entryBytes);
            }

            return result;
        }
    }
}
