namespace BlazorRepl.Client
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Models;
    using BlazorRepl.Client.Services;
    using BlazorRepl.Core;
    using BlazorRepl.Core.PackageInstallation;
    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NuGet.Common;
    using NuGet.DependencyResolver;
    using NuGet.Protocol.Core.Types;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");

            builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddScoped<SnippetsService>();
            builder.Services.AddSingleton<CompilationService>();
            builder.Services.AddSingleton<RemoteDependencyProvider>();
            builder.Services.AddScoped<NuGetPackageManager>();
            builder.Services.AddHttpClient();
            builder.Services.AddScoped(serviceProvider =>
            {
                var remoteWalkContext = new RemoteWalkContext(NullSourceCacheContext.Instance, NullLogger.Instance);

                var remoteDependencyProvider = serviceProvider.GetRequiredService<RemoteDependencyProvider>();
                remoteWalkContext.RemoteLibraryProviders.Add(remoteDependencyProvider);

                return new RemoteDependencyWalker(remoteWalkContext);
            });

            builder.Services
                .AddOptions<SnippetsOptions>()
                .Configure<IConfiguration>((options, configuration) => configuration.GetSection("Snippets").Bind(options));

            // TODO: builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
            builder.Logging.Services.AddSingleton<ILoggerProvider, HandleCriticalUserComponentExceptionsLoggerProvider>();

            await builder.Build().RunAsync();
        }
    }
}
