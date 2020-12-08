namespace BlazoreRepl.Tester
{
    using System;
    using System.Net.Http;
    using System.Text.Json;

    using BlazorRepl.Client.Components;

    using NuGet.Common;
    using NuGet.DependencyResolver;
    using NuGet.Frameworks;
    using NuGet.LibraryModel;
    using NuGet.Protocol.Core.Types;
    using NuGet.RuntimeModel;

    class Program
    {
        static void Main(string[] args)
        {
            var rp = new DepProvider(new HttpClient());

            var ctx = new RemoteWalkContext(new NullSourceCacheContext(), new NullLogger());
            ctx.RemoteLibraryProviders.Add(rp);
            var walker = new RemoteDependencyWalker(ctx);

            var res = walker.WalkAsync(
                new LibraryRange("Blazored.Modal", LibraryDependencyTarget.All),
                new NuGetFramework("net5.0"),
                "net5.0",
                new RuntimeGraph(),
                recursive: true).GetAwaiter().GetResult();

            Console.WriteLine(JsonSerializer.Serialize(res));
        }
    }
}
