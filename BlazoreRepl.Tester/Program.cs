namespace BlazoreRepl.Tester
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
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

            var rp = new DepProvider(new HttpClient(), new Dictionary<string, LibraryDependencyInfo>());

            var cache = new NullSourceCacheContext();
            var logger = new NullLogger();
            var ctx = new RemoteWalkContext(cache, logger);
            ctx.RemoteLibraryProviders.Add(rp);
            var walker = new RemoteDependencyWalker(ctx);

            var libRange = new LibraryRange(
                "Blazored.Modal",
                new VersionRange(new NuGetVersion(5, 1, 0)),
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
            InstallPackages(res, rp.PackagesForInstall).GetAwaiter().GetResult();
        }

        private static void PrintPackagesInfo(GraphNode<RemoteResolveResult> graphNode, int nestLevel = 0)
        {
            var cachedString = initialCache.ContainsKey(graphNode.Item.Key.Name) ? "[cache hit]" : "[cache miss]";
            Console.WriteLine($"{new string(' ', nestLevel * 4)} {graphNode.Item.Key.Name} {cachedString} [{graphNode.Item.Key.Version}]");
            foreach (var innerNode in graphNode.InnerNodes)
            {
                PrintPackagesInfo(innerNode, nestLevel + 1);
            }
        }

        private static async Task InstallPackages(GraphNode<RemoteResolveResult> graphNode, IDictionary<string, LibraryDependencyInfo> packageForInstall, int nestLevel = 0)
        {
            var shouldInstall = !initialCache.ContainsKey(graphNode.Item.Key.Name);
            if (shouldInstall)
            {
                if (packageForInstall.TryGetValue(graphNode.Item.Key.Name, out var dependencyInfo))
                {
                    foreach (var innerNode in graphNode.InnerNodes)
                    {
                        await InstallPackages(innerNode, packageForInstall, nestLevel + 1);
                    }

                    var Http = new HttpClient();

                    var package = await Http.GetByteArrayAsync(
                        $"https://api.nuget.org/v3-flatcontainer/{dependencyInfo.Library.Name}/{dependencyInfo.Library.Version}/{dependencyInfo.Library.Name}.{dependencyInfo.Library.Version}.nupkg");

                    using var zippedStream = new MemoryStream(package);
                    using var archive = new ZipArchive(zippedStream);
                    var dllEntries = archive.Entries.Where(e =>
                        Path.GetExtension(e.FullName) == ".dll" &&
                        Path.GetDirectoryName(e.FullName).Contains(dependencyInfo.Framework.GetShortFolderName()));

                    Console.WriteLine(string.Join(',', archive.Entries.Select(e => e.FullName)));

                    // get only the dll that we need
                    // we could have more than one dll (netstandard2.0, netstandard2.1... folders)
                    if (dllEntries.Any())
                    {
                        foreach (var dllEntry in dllEntries)
                        {
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
                else
                {
                    Console.WriteLine($"There is a package which we should install, but it's not in the PackageForInstall dict: {graphNode.Item.Key.Name}");
                }
            }
        }
    }
}
