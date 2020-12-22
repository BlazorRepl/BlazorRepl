namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;

    using NuGet.Common;
    using NuGet.Configuration;
    using NuGet.DependencyResolver;
    using NuGet.Frameworks;
    using NuGet.LibraryModel;
    using NuGet.Packaging;
    using NuGet.Packaging.Core;
    //using NuGet.ProjectModel;
    using NuGet.Protocol;
    using NuGet.Protocol.Core.Types;
    using NuGet.Versioning;

    public class DepProvider : IRemoteDependencyProvider
    {
        private readonly HttpClient client;
        private readonly Dictionary<string, LibraryDependencyInfo> libraryCache = new();

        public DepProvider(HttpClient client, Dictionary<string, LibraryDependencyInfo> libraryCache)
        {
            this.client = client;
            this.libraryCache = libraryCache;
        }

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
            // throw new NotImplementedException();
            // var ldi = LibraryDependencyInfo.CreateUnresolved(libraryIdentity, targetFramework);

            if (libraryCache.TryGetValue(libraryIdentity.Name, out var dependencyInfo))
            {
                // handle the case when the constraint is not >=
                if (dependencyInfo.Library.Version >= libraryIdentity.Version)
                {
                    return dependencyInfo;
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
            //var nr = new NuspecCoreReader(nuspecStream);
            var nr1 = new NuspecReader(nuspecStream);

            //var epr = new ExternalProjectReference(

            //var a = new PackageSpecReferenceDependencyProvider(Enumerable.Empty<ExternalProjectReference>(), logger);
            //var lib = a.GetLibrary(libraryIdentity, targetFramework);
            //var dependencyInfo = LibraryDependencyInfo.Create(
            //    lib.Identity,
            //    targetFramework,
            //    lib.Dependencies);

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

            libraryCache.Add(libraryIdentity.Name, res);

            return res;

            //return Task.FromResult(dependencyInfo)

            //var group = nr1.GetDependencyGroups(true).FirstOrDefault(g => g.TargetFramework == targetFramework);
            //if (group != null)
            //{
            //    group.Packages.Select(x => x.)

            //    //ldi.Resolved = true;
            //    //ldi. Dependencies = group.Packages.Select(x => new LibraryDependency(x.VersionRange, LibraryDependencyType.Default, LibraryIncludeFlags.Compile, LibraryIncludeFlags.Compile, null, true, false))
            //}
            //group.Packages.Select(x => LibraryDependencyInfo.Create())

            //var versions = XDocument.Parse<Nuspec>(versionsResult["versions"].ToString()).Select(x => new NuGetVersion(x)).ToList();
        }

        public Task<IPackageDownloader> GetPackageDownloaderAsync(
            PackageIdentity packageIdentity,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var ps = new PackageSource("https://api.nuget.org/v3/index.json");
            var ngrp1 = new PackageSearchResourceV3Provider();
            var ngrp2 = new DownloadResourceV3Provider();
            var ngrp3 = new HttpSourceResourceProvider();
            var ngrp4 = new RemoteV3FindPackageByIdResourceProvider();
            var ngrp5 = new DependencyInfoResourceV3Provider();

            var resourceProviders = new INuGetResourceProvider[] { ngrp1, ngrp2, ngrp3, ngrp4, ngrp5 };

            var sr = new SourceRepository(ps, resourceProviders);

            var httpSource = HttpSource.Create(sr);

            var remoteResource = new RemoteV3FindPackageByIdResource(sr, httpSource);
            IPackageDownloader downloader = new RemotePackageArchiveDownloader(
                "https://api.nuget.org/v3/index.json",
                remoteResource,
                packageIdentity,
                cacheContext,
                logger);

            return Task.FromResult(downloader);
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
