namespace BlazoreRepl.Tester
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    using BlazorRepl.Client.Components;
    using BlazorRepl.Core;

    using NuGet.Common;
    using NuGet.DependencyResolver;
    using NuGet.Frameworks;
    using NuGet.LibraryModel;
    using NuGet.Protocol.Core.Types;
    using NuGet.RuntimeModel;
    using NuGet.Versioning;
    using NuGet.Packaging.Core;
    using System.Threading.Tasks;
    using System.IO;
    using System.IO.Compression;

    class Program
    {
        private static IDictionary<string, LibraryDependencyInfo> initialCache =
            new Dictionary<string, LibraryDependencyInfo>();

        private static List<string> packagesDllsInBase64 = new List<string>();
        private static List<string> packagesStylesInBase64 = new List<string>();
        private static List<string> packagesScriptsInBase64 = new List<string>();

        static void Main(string[] args)
        {
            //CompilationService.InitAsync(new HttpClient { BaseAddress = new Uri("https://localhost:44347") }).GetAwaiter().GetResult();

            //initialCache = CompilationService.BaseAssemblyNames
            //    .Select(x =>
            //    {
            //        var libIdentity = new LibraryIdentity(
            //            x.Name,
            //            new NuGetVersion(x.Version),
            //            LibraryType.Assembly);

            //        return new LibraryDependencyInfo(
            //            libIdentity,
            //            resolved: true,
            //            FrameworkConstants.CommonFrameworks.Net50,
            //            Array.Empty<LibraryDependency>());
            //    })
            //    .ToDictionary(x => x.Library.Name, x => x);

            var rp = new DepProvider(new HttpClient(), new ConcurrentDictionary<string, LibraryDependencyInfo>());

            var cache = new NullSourceCacheContext();
            var logger = new NullLogger();
            var ctx = new RemoteWalkContext(cache, logger);
            ctx.RemoteLibraryProviders.Add(rp);
            var walker = new RemoteDependencyWalker(ctx);

            var libRange = new LibraryRange(
                "Syncfusion.Blazor",
                new VersionRange(new NuGetVersion("18.4.0.31")),
                LibraryDependencyTarget.Package);
            var framework = FrameworkConstants.CommonFrameworks.Net50;
            var graph = new RuntimeGraph(new[] { new RuntimeDescription("net5.0") });

            var res = walker.WalkAsync(
                libRange,
                framework,
                "net5.0",
                graph,
                recursive: true).GetAwaiter().GetResult();

            // should we create new downloader for each of the not cached walked items?
            // can we force downloader to automatically walk the package identity deps?
            //var downloader = rp.GetPackageDownloaderAsync(
            //    new PackageIdentity("Blazored.Modal", new NuGetVersion(5, 1, 0)),
            //    cache,
            //    logger,
            //    default).GetAwaiter().GetResult();

            // get target framework from PackagesForInstall


            //downloader.CoreReader.GetStreamAsync()

            //Console.WriteLine(downloader.);

            PrintPackagesInfo(res);
            InstallPackages(new HttpClient(), rp.PackagesForInstall).GetAwaiter().GetResult();

            rp.PackagesForInstall.Clear();

            //var libRange1 = new LibraryRange(
            //    "Syncfusion.Blazor",
            //    new VersionRange(new NuGetVersion("18.4.0.31")),
            //    LibraryDependencyTarget.Package);

            //var res1 = walker.WalkAsync(
            //    libRange1,
            //    framework,
            //    "net5.0",
            //    graph,
            //    recursive: true).GetAwaiter().GetResult();

            //PrintPackagesInfo(res1);
            //InstallPackages(new HttpClient(), rp.PackagesForInstall).GetAwaiter().GetResult();
        }

        private static void PrintPackagesInfo(GraphNode<RemoteResolveResult> graphNode, int nestLevel = 0)
        {
            var cachedString = initialCache.ContainsKey(graphNode.Item?.Key?.Name ?? "Unknown") ? "[cache hit]" : "[cache miss]";
            Console.WriteLine($"{new string(' ', nestLevel * 4)} {graphNode.Item?.Key?.Name ?? "Unknown"} {cachedString} [{graphNode.Item?.Key?.Version}]");
            foreach (var innerNode in graphNode.InnerNodes)
            {
                PrintPackagesInfo(innerNode, nestLevel + 1);
            }
        }

        private static async Task InstallPackages(HttpClient httpClient, IDictionary<string, LibraryDependencyInfo> packagesToInstall, int nestLevel = 0)
        {
            foreach (var (key, packageToInstall) in packagesToInstall)
            {
                var package = await httpClient.GetByteArrayAsync(
                    $"https://api.nuget.org/v3-flatcontainer/{packageToInstall.Library.Name}/{packageToInstall.Library.Version}/{packageToInstall.Library.Name}.{packageToInstall.Library.Version}.nupkg");

                // sometimes the packages comes with folder name netcoreapp5.0 instead of net5.0
                var targetFrameworkFolderNames = packageToInstall.Framework == FrameworkConstants.CommonFrameworks.Net50
                    ? new[] { packageToInstall.Framework.GetShortFolderName(), "netcoreapp5.0" }
                    : new[] { packageToInstall.Framework.GetShortFolderName() };

                using var zippedStream = new MemoryStream(package);
                using var archive = new ZipArchive(zippedStream);
                var dllEntries = archive.Entries
                    .Where(e =>
                        Path.GetExtension(e.FullName) == ".dll" &&
                        targetFrameworkFolderNames.Contains(Path.GetDirectoryName(e.FullName).Replace("lib\\", string.Empty)))
                    .ToList();

                //Console.WriteLine(string.Join(',', archive.Entries.Select(e => e.FullName)));

                // get only the dll that we need
                // we could have more than one dll (netstandard2.0, netstandard2.1... folders)
                if (dllEntries.Any())
                {
                    foreach (var dllEntry in dllEntries)
                    {
                        Console.WriteLine(packageToInstall.Library.Name);
                        using var dllMemoryStream = new MemoryStream();
                        using var dllStream = dllEntry.Open();
                        dllStream.CopyTo(dllMemoryStream);

                        var dllBytes = dllMemoryStream.ToArray();

                        //CompilationService.AddReference(dllBytes);

                        var dllBase64 = Convert.ToBase64String(dllBytes);
                        packagesDllsInBase64.Add(dllBase64);
                        //await this.JsRuntime.InvokeVoidAsync(
                        //   "App.NugetPackageInstallerPopup.addNugetFileToCache",
                        //   dllEntry.Name,
                        //   dllBase64);

                        //this.Visible = false;
                        //await this.VisibleChanged.InvokeAsync(this.Visible);
                    }

                }

                // ensure they are in in the correct folder?
                var cssEntries = archive.Entries.Where(e => e.FullName.EndsWith(".css"));
                foreach (var cssEntry in cssEntries)
                {
                    // do we need this check?
                    if (cssEntry != null)
                    {
                        using var memoryStream = new MemoryStream();
                        using var stream = cssEntry.Open();
                        stream.CopyTo(memoryStream);

                        var bytes = memoryStream.ToArray();
                        var base64 = Convert.ToBase64String(bytes);
                        packagesStylesInBase64.Add(base64);
                    }
                }

                // ensure they are in in the correct folder?
                var jsEntries = archive.Entries.Where(e => e.FullName.EndsWith(".js"));
                foreach (var jsEntry in jsEntries)
                {
                    // do we need this check?
                    if (jsEntry != null)
                    {
                        using var memoryStream = new MemoryStream();
                        using var stream = jsEntry.Open();
                        stream.CopyTo(memoryStream);

                        var bytes = memoryStream.ToArray();
                        var base64 = Convert.ToBase64String(bytes);
                        packagesScriptsInBase64.Add(base64);
                    }
                }
            }
        }
    }
}
