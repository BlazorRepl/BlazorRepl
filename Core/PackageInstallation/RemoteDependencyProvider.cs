namespace BlazorRepl.Core.PackageInstallation
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using NuGet.Common;
    using NuGet.Configuration;
    using NuGet.DependencyResolver;
    using NuGet.Frameworks;
    using NuGet.LibraryModel;
    using NuGet.Packaging;
    using NuGet.Packaging.Core;
    using NuGet.Protocol.Core.Types;
    using NuGet.Versioning;

    public class RemoteDependencyProvider : IRemoteDependencyProvider
    {
        private static readonly ConcurrentDictionary<string, LibraryDependencyInfo> LibraryDependencyCache = new();

        private readonly IHttpClientFactory httpClientFactory;
        private readonly ConcurrentDictionary<string, LibraryDependencyInfo> packagesToInstall = new();
        private readonly ConcurrentDictionary<string, LibraryDependencyInfo> packagesRequiringLicenseAcceptance = new();

        public RemoteDependencyProvider(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public bool IsHttp { get; } = true;

        public PackageSource Source { get; } = new("https://api.nuget.org/v3/index.json");

        internal ICollection<LibraryDependencyInfo> PackagesToInstall => this.packagesToInstall.Values;

        internal ICollection<LibraryDependencyInfo> PackagesRequiringLicenseAcceptance => this.packagesRequiringLicenseAcceptance.Values;

        public static void AddAssemblyDependenciesToCache(IEnumerable<AssemblyIdentity> assemblyNames)
        {
            foreach (var assemblyName in assemblyNames ?? Enumerable.Empty<AssemblyIdentity>())
            {
                var libraryIdentity = new LibraryIdentity(
                    assemblyName.Name,
                    new NuGetVersion(assemblyName.Version),
                    LibraryType.Assembly);

                var libraryDependencyInfo = new LibraryDependencyInfo(
                    libraryIdentity,
                    resolved: true,
                    FrameworkConstants.CommonFrameworks.Net50,
                    Array.Empty<LibraryDependency>());

                LibraryDependencyCache.TryAdd(libraryIdentity.Name, libraryDependencyInfo);
            }
        }

        public Task<LibraryIdentity> FindLibraryAsync(
            LibraryRange libraryRange,
            NuGetFramework targetFramework,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            // we are validating the version, name and target framework upon getting them in the ui
            // so we don't need second validation here
            return Task.FromResult(new LibraryIdentity(
                libraryRange.Name,
                libraryRange.VersionRange.MinVersion,
                LibraryType.Package));
        }

        public async Task<LibraryDependencyInfo> GetDependenciesAsync(
            LibraryIdentity libraryIdentity,
            NuGetFramework targetFramework,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (LibraryDependencyCache.TryGetValue(libraryIdentity.Name, out var dependencyInfo))
            {
                // handle the case when the constraint is not >=
                if (dependencyInfo.Library.Version >= libraryIdentity.Version)
                {
                    return dependencyInfo;
                }

                throw new InvalidOperationException();

                // differentiate the deps which comes from the project from those which comes from the current walking

                // we should separate the cache in 3 different collection
                // 1. libraries from project
                // 2. libraries from different nuget installations
                // 3. libraries from current walking

                // if we have downgrade from the versions in type 1 -> throw
                // if we have downgrade from the versions in type 2 -> check if the outside package can work with the current version that the current walking want to install.
                //    if yes -> download new version, change the version of collection type 2 in the cache and flag this library. After the process is finished, we should get the marked libraries and change the sources in the cache
                //    if not -> throw
            }

            var httpClient = this.httpClientFactory.CreateClient(nameof(RemoteDependencyProvider));
            var nuspecStream = await httpClient.GetStreamAsync(
                $"https://api.nuget.org/v3-flatcontainer/{libraryIdentity.Name}/{libraryIdentity.Version}/{libraryIdentity.Name}.nuspec",
                cancellationToken);

            var nuspecReader = new NuspecReader(nuspecStream);

            var dependencyGroup = NuGetFrameworkUtility.GetNearest(
                nuspecReader.GetDependencyGroups(false),
                targetFramework,
                item => item.TargetFramework);

            var dependencies = dependencyGroup?.Packages?.Select(PackagingUtility.GetLibraryDependencyFromNuspec).ToArray();

            var libraryDependencyInfo = new LibraryDependencyInfo(
                libraryIdentity,
                resolved: true,
                dependencyGroup?.TargetFramework ?? targetFramework,
                dependencies ?? Array.Empty<LibraryDependency>());

            LibraryDependencyCache.TryAdd(libraryIdentity.Name, libraryDependencyInfo);

            this.packagesToInstall.TryAdd(libraryIdentity.Name, libraryDependencyInfo);

            if (nuspecReader.GetRequireLicenseAcceptance())
            {
                this.packagesRequiringLicenseAcceptance.TryAdd(libraryIdentity.Name, libraryDependencyInfo);
            }

            return libraryDependencyInfo;
        }

        public Task<IPackageDownloader> GetPackageDownloaderAsync(
            PackageIdentity packageIdentity,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(
            string id,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            // TODO: Try using strongly-typed object from NuGet.Client lib
            var httpClient = this.httpClientFactory.CreateClient(nameof(RemoteDependencyProvider));
            var versionsResult = await httpClient.GetFromJsonAsync<IDictionary<string, object>>(
                $"https://api.nuget.org/v3-flatcontainer/{id}/index.json",
                cancellationToken);

            var versions = JsonSerializer
                .Deserialize<IEnumerable<string>>(versionsResult["versions"].ToString())
                .Select(x => new NuGetVersion(x))
                .ToList();

            return versions;
        }

        internal void ClearPackagesToInstall() => this.packagesToInstall.Clear();
    }
}
