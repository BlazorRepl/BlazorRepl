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

    class Program
    {
        private static IDictionary<string, LibraryDependencyInfo> initialCache =
            new Dictionary<string, LibraryDependencyInfo>();

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

            //var downloader = rp.GetPackageDownloaderAsync(
            //    new PackageIdentity("Blazored.Modal", new NuGetVersion(5, 1, 0)),
            //    cache,
            //    logger,
            //    default).GetAwaiter().GetResult();

            //Console.WriteLine(downloader.);

            PrintPackagesInfo(res);
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
    }
}
