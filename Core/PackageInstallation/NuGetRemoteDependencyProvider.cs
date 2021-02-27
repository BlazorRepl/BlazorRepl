namespace BlazorRepl.Core.PackageInstallation
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
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

    /// <remarks>
    /// Must be registered in DI container as transient because of the internal cache
    /// </remarks>
    public class NuGetRemoteDependencyProvider : IRemoteDependencyProvider
    {
        private readonly HttpClient httpClient;

        private ConcurrentDictionary<string, (LibraryDependencyInfo Dependency, string SourcePackage)> cache;

        public NuGetRemoteDependencyProvider(HttpClient httpClient)
        {
            Console.WriteLine("new NuGetRemoteDependencyProvider");

            this.httpClient = httpClient;

            this.InitializeCache();
        }

        public bool IsHttp { get; } = true;

        public PackageSource Source { get; } = new("https://api.nuget.org/v3/index.json");

        internal ICollection<LibraryDependencyInfo> PackagesToInstall { get; } = new List<LibraryDependencyInfo>();

        internal ICollection<PackageLicenseInfo> PackagesToAcceptLicense { get; } = new List<PackageLicenseInfo>();

        internal string SourcePackage { get; set; }

        public Task<LibraryIdentity> FindLibraryAsync(
            LibraryRange libraryRange,
            NuGetFramework targetFramework,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            // We are validating the name, version and target framework upon getting them on UI so we skip second validation here
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
            if (this.cache.TryGetValue(libraryIdentity.Name, out var dependencyInfo))
            {
                var dependencyLibrary = dependencyInfo.Dependency.Library;
                if (dependencyLibrary.Version >= libraryIdentity.Version)
                {
                    return dependencyInfo.Dependency;
                }

                if (string.IsNullOrWhiteSpace(dependencyInfo.SourcePackage))
                {
                    throw new InvalidOperationException(
                        $"Cannot install package '{dependencyLibrary.Name}' v{libraryIdentity.Version} because v{dependencyLibrary.Version} is directly installed to the app.");
                }

                if (this.cache.Values.Any(x => x.SourcePackage == libraryIdentity.Name))
                {
                    throw new InvalidOperationException(
                        $"Cannot install package '{dependencyLibrary.Name}' v{libraryIdentity.Version} because lower v{dependencyLibrary.Version} is already installed. You can manually update the package.");
                }

                // Remove the old version from cache and try again
                this.cache.TryRemove(libraryIdentity.Name, out _);

                return await this.GetDependenciesAsync(libraryIdentity, targetFramework, cacheContext, logger, cancellationToken);
            }

            const string NuGetNuspecEndpointFormat = "https://api.nuget.org/v3-flatcontainer/{0}/{1}/{0}.nuspec";

            var nuspecStream = await this.httpClient.GetStreamAsync(
                string.Format(NuGetNuspecEndpointFormat, libraryIdentity.Name, libraryIdentity.Version),
                cancellationToken);

            var nuspecReader = new NuspecReader(nuspecStream);

            var dependencyGroup = NuGetFrameworkUtility.GetNearest(
                nuspecReader.GetDependencyGroups(useStrictVersionCheck: false),
                targetFramework,
                item => item.TargetFramework);

            var dependencies = dependencyGroup?.Packages?.Select(PackagingUtility.GetLibraryDependencyFromNuspec).ToList();

            var libraryDependencyInfo = new LibraryDependencyInfo(
                libraryIdentity,
                resolved: true,
                dependencyGroup?.TargetFramework ?? NuGetFramework.AnyFramework,
                dependencies ?? Enumerable.Empty<LibraryDependency>());

            if (this.cache.TryAdd(libraryIdentity.Name, (libraryDependencyInfo, this.SourcePackage)))
            {
                this.PackagesToInstall.Add(libraryDependencyInfo);

                if (nuspecReader.GetRequireLicenseAcceptance())
                {
                    var authors = nuspecReader.GetAuthors();
                    var licenseMetadata = nuspecReader.GetLicenseMetadata();
                    var licenseUrl = nuspecReader.GetLicenseUrl() ?? licenseMetadata?.LicenseUrl?.ToString();

                    this.PackagesToAcceptLicense.Add(new PackageLicenseInfo
                    {
                        Package = libraryIdentity.Name,
                        License = licenseMetadata?.License,
                        LicenseUrl = licenseUrl,
                        Authors = authors,
                    });
                }
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

        public Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(
            string id,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        internal void ClearPackagesToInstall(bool clearFromCache = false)
        {
            if (clearFromCache)
            {
                foreach (var package in this.PackagesToInstall)
                {
                    this.cache.TryRemove(package.Library.Name, out _);
                }
            }

            this.PackagesToInstall.Clear();
            this.PackagesToAcceptLicense.Clear();
        }

        private void InitializeCache()
        {
            this.cache = new();

            foreach (var (packageName, packageVersion) in CompilationService.BaseAssemblyPackageVersionMappings)
            {
                var libraryIdentity = new LibraryIdentity(packageName, new NuGetVersion(packageVersion), LibraryType.Package);

                var libraryDependencyInfo = new LibraryDependencyInfo(
                    libraryIdentity,
                    resolved: true,
                    FrameworkConstants.CommonFrameworks.Net50,
                    Array.Empty<LibraryDependency>());

                // App packages are marked with null source package
                this.cache.TryAdd(libraryIdentity.Name, (libraryDependencyInfo, null));
            }
        }
    }
}
