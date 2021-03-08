namespace BlazorRepl.Core.PackageInstallation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;
    using NuGet.Common;
    using NuGet.DependencyResolver;
    using NuGet.Frameworks;
    using NuGet.LibraryModel;
    using NuGet.Packaging;
    using NuGet.Protocol.Core.Types;
    using NuGet.Versioning;

    public class NuGetPackageManagementService
    {
        private static readonly string LibFolderPrefix = $"lib{Path.DirectorySeparatorChar}";
        private static readonly string StaticWebAssetsFolderPrefix = $"staticwebassets{Path.DirectorySeparatorChar}";

        private readonly NuGetRemoteDependencyProvider remoteDependencyProvider;
        private readonly HttpClient httpClient;
        private readonly RemoteDependencyWalker remoteDependencyWalker;
        private readonly RemoteWalkContext remoteWalkContext;
        private readonly List<Package> installedPackages = new();

        private Package currentlyInstallingPackage;

        public NuGetPackageManagementService(NuGetRemoteDependencyProvider remoteDependencyProvider, HttpClient httpClient)
        {
            this.remoteDependencyProvider = remoteDependencyProvider;
            this.httpClient = httpClient;

            this.remoteWalkContext = new RemoteWalkContext(NullSourceCacheContext.Instance, NullLogger.Instance);
            this.remoteWalkContext.RemoteLibraryProviders.Add(this.remoteDependencyProvider);

            this.remoteDependencyWalker = new RemoteDependencyWalker(this.remoteWalkContext);
        }

        public IReadOnlyCollection<Package> InstalledPackages => this.installedPackages;

        public async Task<PreparePackageInstallationResult> PreparePackageForDownloadAsync(string packageName, string packageVersion)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentOutOfRangeException(nameof(packageName));
            }

            if (string.IsNullOrWhiteSpace(packageVersion))
            {
                throw new ArgumentOutOfRangeException(nameof(packageVersion));
            }

            if (this.currentlyInstallingPackage != null)
            {
                throw new InvalidOperationException("Another package is currently being installed.");
            }

            var libraryRange = new LibraryRange(
                packageName,
                new VersionRange(new NuGetVersion(packageVersion)),
                LibraryDependencyTarget.Package);

            this.remoteDependencyProvider.SourcePackage = packageName;

            try
            {
                await this.remoteDependencyWalker.WalkAsync(
                    libraryRange,
                    framework: FrameworkConstants.CommonFrameworks.Net50,
                    runtimeIdentifier: null,
                    runtimeGraph: null,
                    recursive: true);
            }
            finally
            {
                this.remoteDependencyProvider.SourcePackage = null;
            }

            this.currentlyInstallingPackage = new Package { Name = packageName, Version = packageVersion };

            return new PreparePackageInstallationResult
            {
                PackagesToAcceptLicense = this.remoteDependencyProvider.PackagesToAcceptLicense,
            };
        }

        public void CancelPackageInstallation()
        {
            this.currentlyInstallingPackage = null;

            this.remoteDependencyProvider.ClearPackagesToInstall(clearFromCache: true);

            this.remoteWalkContext.FindLibraryEntryCache.Clear();
        }

        public async Task<PackagesContentsResult> DownloadPackagesContentsAsync()
        {
            if (this.currentlyInstallingPackage == null)
            {
                throw new InvalidOperationException("No package is currently being installed.");
            }

            try
            {
                var result = new PackagesContentsResult();

                foreach (var package in this.remoteDependencyProvider.PackagesToInstall)
                {
                    // Get byte[] instead of Stream because for some reason the stream later (when storing) is not the same
                    const string NuGetPackageDownloadEndpointFormat = "https://api.nuget.org/v3-flatcontainer/{0}/{1}/{0}.{1}.nupkg";
                    var packageBytes = await this.httpClient.GetByteArrayAsync(
                        string.Format(NuGetPackageDownloadEndpointFormat, package.Library.Name, package.Library.Version));

                    using var memoryStream = new MemoryStream(packageBytes);
                    using var archive = new ZipArchive(memoryStream);

                    var dlls = ExtractDlls(archive.Entries, package.Framework);
                    foreach (var file in dlls)
                    {
                        result.DllFiles.Add(file);
                    }

                    var scripts = ExtractStaticContents(archive.Entries, ".js");
                    foreach (var file in scripts)
                    {
                        result.JavaScriptFiles.Add(file);
                    }

                    var styles = ExtractStaticContents(archive.Entries, ".css");
                    foreach (var file in styles)
                    {
                        result.CssFiles.Add(file);
                    }
                }

                this.installedPackages.Add(this.currentlyInstallingPackage);

                return result;
            }
            finally
            {
                this.currentlyInstallingPackage = null;
                this.remoteDependencyProvider.ClearPackagesToInstall();
            }
        }

        public async Task<IEnumerable<string>> SearchPackagesAsync(string query, int take = 7)
        {
            // TODO: Support prerelease packages
            // TODO: Maybe support other package types
            const string NuGetSearchPackagesEndpointFormat =
                "https://api-v2v3search-0.nuget.org/autocomplete?q={0}&take={1}&packageType=dependency&semVerLevel=2.0.0&prerelease=false";

            var result = await this.httpClient.GetFromJsonAsync<NuGetPackagesSearchResponse>(
                string.Format(NuGetSearchPackagesEndpointFormat, query, take));

            return result?.Data ?? Enumerable.Empty<string>();
        }

        public async Task<IEnumerable<string>> GetPackageVersionsAsync(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentOutOfRangeException(nameof(packageName));
            }

            // TODO: Support prerelease packages
            const string NuGetPackageVersionsEndpointFormat =
                "https://api-v2v3search-0.nuget.org/autocomplete?id={0}&semVerLevel=2.0.0&prerelease=false";

            var result = await this.httpClient.GetFromJsonAsync<NuGetPackageVersionsResponse>(
                string.Format(NuGetPackageVersionsEndpointFormat, packageName));

            return result?.Data?.Reverse().ToList() ?? Enumerable.Empty<string>();
        }

        // TODO: Abstract .NET 5.0 hard-coded stuff everywhere
        private static IDictionary<string, byte[]> ExtractDlls(IEnumerable<ZipArchiveEntry> entries, NuGetFramework framework)
        {
            var allDllEntries = entries.Where(e =>
                Path.GetExtension(e.FullName) == ".dll" &&
                e.FullName.StartsWith(LibFolderPrefix, StringComparison.OrdinalIgnoreCase));

            var wantedFramework = framework;
            if (framework == NuGetFramework.AnyFramework)
            {
                var frameworkCandidates = new HashSet<NuGetFramework>();
                foreach (var dllEntry in allDllEntries)
                {
                    var path = dllEntry.FullName[LibFolderPrefix.Length..];
                    var candidateFramework = FrameworkNameUtility.ParseNuGetFrameworkFolderName(path, strictParsing: true, out _);

                    if (candidateFramework != null)
                    {
                        frameworkCandidates.Add(candidateFramework);
                    }
                }

                var nearestCompatibleFramework = NuGetFrameworkUtility.GetNearest(
                    frameworkCandidates,
                    FrameworkConstants.CommonFrameworks.Net50,
                    f => f);

                if (nearestCompatibleFramework == null)
                {
                    return new Dictionary<string, byte[]>(0);
                }

                wantedFramework = nearestCompatibleFramework;
            }

            var dllEntries = allDllEntries.Where(e =>
            {
                var path = e.FullName[LibFolderPrefix.Length..];
                var parsedFramework = FrameworkNameUtility.ParseNuGetFrameworkFolderName(path, strictParsing: true, out _);

                return parsedFramework == wantedFramework;
            });

            return GetEntriesContent(dllEntries);
        }

        private static IDictionary<string, byte[]> ExtractStaticContents(IEnumerable<ZipArchiveEntry> entries, string extension)
        {
            var staticContentEntries = entries.Where(e =>
                Path.GetExtension(e.Name) == extension &&
                e.FullName.StartsWith(StaticWebAssetsFolderPrefix, StringComparison.OrdinalIgnoreCase));

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
