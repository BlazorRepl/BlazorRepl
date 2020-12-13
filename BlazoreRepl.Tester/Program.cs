namespace BlazoreRepl.Tester
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using System.Collections.Generic;

    using BlazorRepl.Client.Components;

    using NuGet.Common;
    using NuGet.DependencyResolver;
    using NuGet.Frameworks;
    using NuGet.LibraryModel;
    using NuGet.Protocol.Core.Types;
    using NuGet.RuntimeModel;
    using NuGet.Versioning;

    class Program
    {
        static void Main(string[] args)
        {
            var libraryIdentity = new LibraryIdentity(
                "Microsoft.AspNetCore.Components",
                new NuGetVersion(5, 0, 0),
                LibraryType.Package);

            var depInfo = new LibraryDependencyInfo(
                libraryIdentity,
                true,
                FrameworkConstants.CommonFrameworks.Net50,
                Array.Empty<LibraryDependency>());

            var rp = new DepProvider(new HttpClient(), new Dictionary<string, LibraryDependencyInfo> { { "Microsoft.AspNetCore.Components", depInfo } });

            var ctx = new RemoteWalkContext(new NullSourceCacheContext(), new NullLogger());
            ctx.RemoteLibraryProviders.Add(rp);
            var walker = new RemoteDependencyWalker(ctx);

            var libRange = new LibraryRange(
                "Blazored.Modal",
                new VersionRange(new NuGetVersion(5, 1, 0)),
                LibraryDependencyTarget.Package);
            // var framework = new NuGetFramework(".NET", new Version(5, 0, 0));
            var framework = FrameworkConstants.CommonFrameworks.Net50;
            var graph = new RuntimeGraph(new[] { new RuntimeDescription("net5.0") });

            var res = walker.WalkAsync(
                libRange,
                framework,
                "net5.0",
                graph,
                recursive: true).GetAwaiter().GetResult();

            PrintPackagesInfo(res);
        }

        private static void PrintPackagesInfo(GraphNode<RemoteResolveResult> graphNode, int nestLevel = 0)
        {
            Console.WriteLine($"{new string(' ', nestLevel * 4)} {graphNode.Item.Key.Name} [{graphNode.Item.Key.Version}]");
            foreach (var innerNode in graphNode.InnerNodes)
            {
                PrintPackagesInfo(innerNode, nestLevel + 1);
            }
        }
    }
}
