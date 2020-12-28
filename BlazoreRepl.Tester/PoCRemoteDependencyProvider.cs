namespace BlazoreRepl.Tester
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
    using NuGet.Common;
    using NuGet.Configuration;
    using NuGet.DependencyResolver;
    using NuGet.Frameworks;
    using NuGet.LibraryModel;
    using NuGet.Packaging;
    using NuGet.Packaging.Core;
    using NuGet.Protocol.Core.Types;
    using NuGet.Versioning;

    public class PoCRemoteDependencyProvider : IRemoteDependencyProvider
    {
        private readonly HttpClient client;
        private readonly ConcurrentDictionary<string, LibraryDependencyInfo> libraryCache = new();

        public PoCRemoteDependencyProvider(HttpClient client, ConcurrentDictionary<string, LibraryDependencyInfo> libraryCache)
        {
            this.client = client;
            this.libraryCache = libraryCache;
        }

        public ConcurrentDictionary<string, LibraryDependencyInfo> PackagesForInstall { get; set; } = new();

        public Task<LibraryIdentity> FindLibraryAsync(
            LibraryRange libraryRange,
            NuGetFramework targetFramework,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            // we are validating the version, name and target framework upoun getting them in the ui
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
            if (this.libraryCache.TryGetValue(libraryIdentity.Name, out var dependencyInfo))
            {
                // handle the case when the constraint is not >=
                if (dependencyInfo.Library.Version >= libraryIdentity.Version)
                {
                    return dependencyInfo;
                }
                else
                {
                    throw new InvalidOperationException();
                }

                // differentiate the deps which comes from the project from those which comes from the current waling 

                // we should separate the cache in 3 different collection
                // 1. libraries from project
                // 2. libraries from different nuget instalations
                // 3. libraries from current walking

                // if we have downgrade from the versions in type 1 -> throw 
                // if we have downgrade from the versions in type 2 -> check if the outside package can work with the current version that the current walking want to install. 
                // If yes -> download new version, change the version of collection type 2 in the cache and flag this library. After the process is finished, we should get the marked libraries and change the sources in the cache
                // if not -> throw
            }

            var nuspecStream = await this.client.GetStreamAsync(
                $"https://api.nuget.org/v3-flatcontainer/{libraryIdentity.Name}/{libraryIdentity.Version}/{libraryIdentity.Name}.nuspec");
            var nr1 = new NuspecReader(nuspecStream);

            var dependencies = NuGetFrameworkUtility.GetNearest(
                nr1.GetDependencyGroups(false),
                targetFramework,
                item => item.TargetFramework);

            var deps = dependencies?.Packages?.Select(PackagingUtility.GetLibraryDependencyFromNuspec).ToArray();

            var res = new LibraryDependencyInfo(
                libraryIdentity,
                resolved: true,
                dependencies?.TargetFramework ?? targetFramework,
                deps ?? Array.Empty<LibraryDependency>());

            if (!this.PackagesForInstall.TryAdd(libraryIdentity.Name, res))
            {
                // should we log this in prod?
                Console.WriteLine($"Package {libraryIdentity.Name} already has been added to packages to install");
            }

            if (!this.libraryCache.TryAdd(libraryIdentity.Name, res))
            {
                // should we log this in prod?
                Console.WriteLine($"Package {libraryIdentity.Name} already has been added to cache");
            }

            return res;
        }

        public Task<IPackageDownloader> GetPackageDownloaderAsync(
            PackageIdentity packageIdentity,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string id, SourceCacheContext cacheContext, ILogger logger, CancellationToken token)
        {
            var versionsResult = await this.client.GetFromJsonAsync<IDictionary<string, object>>(
                $"https://api.nuget.org/v3-flatcontainer/{id}/index.json");
            var versions = JsonSerializer.Deserialize<List<string>>(versionsResult["versions"].ToString()).Select(x => new NuGetVersion(x)).ToList();

            return versions;
        }

        public bool IsHttp { get; } = true;

        public PackageSource Source { get; }
    }
}
