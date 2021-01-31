namespace BlazorRepl.Client
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Models;
    using BlazorRepl.Client.Services;
    using BlazorRepl.Core;
    using BlazorRepl.Core.PackageInstallation;
    using BlazorRepl.UserComponents;
    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
    using Microsoft.AspNetCore.Components.WebAssembly.Services;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.JSInterop;
    using NuGet.Common;
    using NuGet.DependencyResolver;
    using NuGet.Protocol.Core.Types;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");

            builder.Services.AddSingleton(serviceProvider => (IJSInProcessRuntime)serviceProvider.GetRequiredService<IJSRuntime>());
            builder.Services.AddSingleton(serviceProvider => (IJSUnmarshalledRuntime)serviceProvider.GetRequiredService<IJSRuntime>());
            builder.Services.AddSingleton(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddSingleton<SnippetsService>();
            builder.Services.AddSingleton<CompilationService>();
            builder.Services.AddSingleton<NuGetRemoteDependencyProvider>();
            builder.Services.AddTransient<NuGetPackageManagementService>();
            builder.Services.AddSingleton(serviceProvider =>
            {
                var remoteWalkContext = new RemoteWalkContext(NullSourceCacheContext.Instance, NullLogger.Instance);

                var remoteDependencyProvider = serviceProvider.GetRequiredService<NuGetRemoteDependencyProvider>();
                remoteWalkContext.RemoteLibraryProviders.Add(remoteDependencyProvider);

                return new RemoteDependencyWalker(remoteWalkContext);
            });

            builder.Services
                .AddOptions<SnippetsOptions>()
                .Configure<IConfiguration>((options, configuration) => configuration.GetSection("Snippets").Bind(options));

            builder.Logging.Services.AddSingleton<ILoggerProvider, HandleCriticalUserComponentExceptionsLoggerProvider>();

            try
            {
                await LoadPackageDllsAsync();

                Startup.Configure(builder);
            }
            catch (Exception ex) when (ex is not MissingMemberException)
            {
                Console.Error.WriteLine($"Error during user startup code execution: {ex}");
            }

            await builder.Build().RunAsync();
        }

        private static async Task LoadPackageDllsAsync()
        {
            const string DefaultJsRuntimeTypeName = "DefaultWebAssemblyJSRuntime";

            var defaultJsRuntimeType = typeof(LazyAssemblyLoader).Assembly
                .GetTypes()
                .SingleOrDefault(t => t.Name == DefaultJsRuntimeTypeName);

            if (defaultJsRuntimeType == null)
            {
                throw new MissingMemberException($"Couldn't find type '{DefaultJsRuntimeTypeName}'.");
            }

            var instanceField = defaultJsRuntimeType.GetField("Instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (instanceField == null)
            {
                throw new MissingMemberException($"Couldn't find property 'Instance' in '{DefaultJsRuntimeTypeName}'.");
            }

            var jsRuntime = (IJSUnmarshalledRuntime)instanceField.GetValue(obj: null);

            // We use timestamps for session ID and care only about DLLs in caches that contain timestamps
            var sessionId = jsRuntime.InvokeUnmarshalled<string>("App.getUrlFragmentValue");
            if (!ulong.TryParse(sessionId, out _))
            {
                return;
            }

            jsRuntime.InvokeUnmarshalled<string, object>("App.CodeExecution.loadPackageFiles", sessionId);

            IEnumerable<byte[]> dllsBytes;
            var i = 0;
            while (true)
            {
                dllsBytes = jsRuntime.InvokeUnmarshalled<IEnumerable<byte[]>>("App.CodeExecution.getLoadedPackageDlls");
                if (dllsBytes != null)
                {
                    break;
                }

                Console.WriteLine($"Iteration: {i++}");
                await Task.Delay(20);
            }

            var sw = new Stopwatch();

            foreach (var dllBytes in dllsBytes)
            {
                sw.Restart();
                AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(dllBytes, writable: false));
                Console.WriteLine($"loading DLL - {sw.Elapsed}");
            }
        }
    }
}
