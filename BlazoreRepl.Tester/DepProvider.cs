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

        public DepProvider(HttpClient client)
        {
            this.client = client;
        }

        public Task<LibraryIdentity> FindLibraryAsync(
            LibraryRange libraryRange,
            NuGetFramework targetFramework,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
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
                nr1.GetDependencyGroups(true),
                targetFramework,
                item => item.TargetFramework);

            var deps = dependencies.Packages.Select(PackagingUtility.GetLibraryDependencyFromNuspec).ToArray();

            var res = new LibraryDependencyInfo(
                libraryIdentity,
                true,
                targetFramework,
                deps);

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
            IPackageDownloader downloader = new RemotePackageArchiveDownloader("https://api.nuget.org/v3/index.json", remoteResource, packageIdentity, cacheContext, logger);
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
