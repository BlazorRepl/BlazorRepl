namespace BlazorRepl.Core.PackageInstallation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using NuGet.DependencyResolver;
    using NuGet.Frameworks;
    using NuGet.LibraryModel;
    using NuGet.RuntimeModel;
    using NuGet.Versioning;

    public class NuGetPackageManager
    {
        private readonly RemoteDependencyWalker remoteDependencyWalker;
        private readonly RemoteDependencyProvider remoteDependencyProvider;
        private readonly HttpClient httpClient;

        public NuGetPackageManager(
            RemoteDependencyWalker remoteDependencyWalker,
            RemoteDependencyProvider remoteDependencyProvider,
            HttpClient httpClient)
        {
            this.remoteDependencyWalker = remoteDependencyWalker;
            this.remoteDependencyProvider = remoteDependencyProvider;
            this.httpClient = httpClient;
        }

        public async Task<IDictionary<string, byte[]>> DownloadPackageContentsAsync(string packageName, string packageVersion)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentOutOfRangeException(nameof(packageName));
            }

            if (string.IsNullOrWhiteSpace(packageVersion))
            {
                throw new ArgumentOutOfRangeException(nameof(packageVersion));
            }

            var libraryRange = new LibraryRange(
                packageName,
                new VersionRange(new NuGetVersion(packageVersion)),
                LibraryDependencyTarget.Package);

            var framework = FrameworkConstants.CommonFrameworks.Net50;
            var graph = new RuntimeGraph(new[] { new RuntimeDescription(framework.DotNetFrameworkName) }); // TODO: do we need that?

            try
            {
                await this.remoteDependencyWalker.WalkAsync(
                    libraryRange,
                    framework,
                    framework.DotNetFrameworkName, // TODO: check if it actually returns net5.0
                    graph,
                    recursive: true);

                var packageContents = new Dictionary<string, byte[]>();

                foreach (var package in this.remoteDependencyProvider.PackagesToInstall)
                {
                    var lib = package.Library;
                    var packageBytes = await this.httpClient.GetByteArrayAsync(
                        $"https://api.nuget.org/v3-flatcontainer/{lib.Name}/{lib.Version}/{lib.Name}.{lib.Version}.nupkg");

                    await using var zippedStream = new MemoryStream(packageBytes);
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

                return packageContents;
            }
            finally
            {
                this.remoteDependencyProvider.ClearPackagesToInstall();
            }
        }

        private static IDictionary<string, byte[]> ExtractDlls(IEnumerable<ZipArchiveEntry> entries, NuGetFramework framework)
        {
            // sometimes the packages comes with folder name netcoreapp5.0 instead of net5.0
            var targetFrameworkFolderNames = framework == FrameworkConstants.CommonFrameworks.Net50
                ? new[] { framework.GetShortFolderName(), "netcoreapp5.0" }
                : new[] { framework.GetShortFolderName() };

            var dllEntries = entries.Where(e =>
                Path.GetExtension(e.FullName) == ".dll" &&
                targetFrameworkFolderNames.Contains(Path.GetDirectoryName(e.FullName).Replace("lib\\", string.Empty)));

            return GetEntriesContent(dllEntries);
        }

        private static IDictionary<string, byte[]> ExtractStaticContents(IEnumerable<ZipArchiveEntry> entries, string extension)
        {
            var staticContentEntries = entries.Where(e => Path.GetExtension(e.Name) == extension);

            return GetEntriesContent(staticContentEntries);
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
