namespace BlazorRepl.Client.Components
{
    using System.Collections.Generic;
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

    public class DepProvider : IRemoteDependencyProvider
    {
        public Task<LibraryIdentity> FindLibraryAsync(
            LibraryRange libraryRange,
            NuGetFramework targetFramework,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<LibraryDependencyInfo> GetDependenciesAsync(
            LibraryIdentity libraryIdentity,
            NuGetFramework targetFramework,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<IPackageDownloader> GetPackageDownloaderAsync(PackageIdentity packageIdentity, SourceCacheContext cacheContext, ILogger logger,
            CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string id, SourceCacheContext cacheContext, ILogger logger, CancellationToken token)
        {
            throw new System.NotImplementedException();
        }

        public bool IsHttp { get; } = true;

        public PackageSource Source { get; }
    }
}
