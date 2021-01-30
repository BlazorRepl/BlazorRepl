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

            if (await LoadPackageDllsAsync())
            {
                // TODO: Ignore startup class casing
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                Console.WriteLine("assemblies " + string.Join(" | ", assemblies.Select(x => x.GetName())));

                var assembly = assemblies.FirstOrDefault(x => x.FullName == "BlazorRepl.UserComponents");
                if (assembly != null)
                {
                    Console.WriteLine("assembly found");
                    var startupType = assembly.GetExportedTypes().SingleOrDefault(t => t.Name == "Startup");
                    if (startupType != null)
                    {
                        Console.WriteLine("startup class exists");
                        var method = startupType.GetMethod("Configure", BindingFlags.Static | BindingFlags.Public);
                        if (method != null)
                        {
                            Console.WriteLine("configure method exists");
                            var parameters = method.GetParameters();
                            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(WebAssemblyHostBuilder))
                            {
                                Console.WriteLine("configure method params are OK");
                                method.Invoke(obj: null, new object[] { builder });
                            }
                            else
                            {
                                Console.WriteLine("no correct params");
                            }
                        }
                        else
                        {
                            Console.WriteLine("no method");
                        }
                    }
                    else
                    {
                        Console.WriteLine("no class");
                    }
                }
                else
                {
                    Console.WriteLine("no assembly");
                }
            }

            await builder.Build().RunAsync();
        }

        private static async Task<bool> LoadPackageDllsAsync()
        {
            var jsRuntime = ReplWebAssemblyJsRuntime.Instance;

            var sessionId = jsRuntime.Invoke<string>("App.getUrlFragment");

            // Validate that the sessionId is not altered
            // TODO: Validate it's a valid timestamp!!!
            if (string.IsNullOrWhiteSpace(sessionId) || !ulong.TryParse(sessionId, out _))
            {
                Console.WriteLine("ALL BAD");
                return false;
            }

            // TODO: Extract to a service
            jsRuntime.InvokeUnmarshalled<string, object>("App.CodeExecution.loadPackageFiles", sessionId);

            IEnumerable<byte[]> dlls;
            var i = 0;
            while (true)
            {
                dlls = jsRuntime.InvokeUnmarshalled<IEnumerable<byte[]>>("App.CodeExecution.getLoadedPackageDlls");
                if (dlls != null)
                {
                    break;
                }

                Console.WriteLine($"Iteration: {i++}");
                await Task.Delay(20);
            }

            var sw = new Stopwatch();

            foreach (var dll in dlls)
            {
                sw.Restart();
                AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(dll, writable: false));
                Console.WriteLine($"loading DLL - {sw.Elapsed}");
            }

            return true;
        }
    }
}
