namespace BlazorRepl.Client
{
    using System;
    using System.Collections.Generic;
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

            var jsRuntime = GetJsRuntime();

            try
            {
                var hasLoadedPackageDll = await TryLoadResourcesAsync(jsRuntime);

                ExecuteUserDefinedConfiguration(builder);

                if (hasLoadedPackageDll)
                {
                    // If we have loaded package DLLs in the app domain, we should reset the user components DLL in the storage for the
                    // next app load, so we are sure the user will use the default user components DLL when he/she loads the app next time
                    jsRuntime.InvokeUnmarshalled<string, object>(
                        "App.CodeExecution.updateUserComponentsDll",
                        CoreConstants.DefaultUserComponentsAssemblyBytes);
                }
            }
            catch (Exception ex)
            {
                // We shouldn't throw during app start so just give the user the info that an exception has been thrown,
                // update the user components DLL to make sure the app will run on reload and continue the app execution
                var actualException = ex is TargetInvocationException tie ? tie.InnerException : ex;
                Console.Error.WriteLine($"Error on app startup: {actualException}");

                jsRuntime.InvokeUnmarshalled<string, object>(
                    "App.CodeExecution.updateUserComponentsDll",
                    CoreConstants.DefaultUserComponentsAssemblyBytes);
            }

            await builder.Build().RunAsync();
        }

        private static async Task<bool> TryLoadResourcesAsync(IJSUnmarshalledRuntime jsRuntime)
        {
            var sessionId = jsRuntime.InvokeUnmarshalled<string>("App.getUrlFragmentValue");

            // We use timestamps for session ID and care only about DLLs in caches that contain timestamps
            if (!ulong.TryParse(sessionId, out _))
            {
                return false;
            }

            jsRuntime.InvokeUnmarshalled<string, object>("App.CodeExecution.loadResources", sessionId);

            IEnumerable<byte[]> dllsBytes;
            while (true)
            {
                dllsBytes = jsRuntime.InvokeUnmarshalled<IEnumerable<byte[]>>("App.CodeExecution.getLoadedPackageDlls");
                if (dllsBytes != null)
                {
                    break;
                }

                await Task.Delay(50);
            }

            var hasLoadedPackageDll = false;
            foreach (var dllBytes in dllsBytes)
            {
                AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(dllBytes));

                hasLoadedPackageDll = true;
            }

            return hasLoadedPackageDll;
        }

        private static void ExecuteUserDefinedConfiguration(WebAssemblyHostBuilder builder)
        {
            var userComponentsAssembly = typeof(__Main).Assembly;
            var startupType = userComponentsAssembly.GetType("Startup", throwOnError: false, ignoreCase: true)
                ?? userComponentsAssembly.GetType("BlazorRepl.UserComponents.Startup", throwOnError: false, ignoreCase: true);

            if (startupType == null)
            {
                return;
            }

            var configureMethod = startupType.GetMethod("Configure", BindingFlags.Static | BindingFlags.Public);
            if (configureMethod == null)
            {
                return;
            }

            var configureMethodParams = configureMethod.GetParameters();
            if (configureMethodParams.Length != 1 || configureMethodParams[0].ParameterType != typeof(WebAssemblyHostBuilder))
            {
                return;
            }

            configureMethod.Invoke(obj: null, new object[] { builder });
        }

        private static IJSUnmarshalledRuntime GetJsRuntime()
        {
            const string DefaultJsRuntimeTypeName = "DefaultWebAssemblyJSRuntime";
            const string InstanceFieldName = "Instance";

            var defaultJsRuntimeType = typeof(LazyAssemblyLoader).Assembly
                .GetTypes()
                .SingleOrDefault(t => t.Name == DefaultJsRuntimeTypeName);

            if (defaultJsRuntimeType == null)
            {
                throw new MissingMemberException($"Couldn't find type '{DefaultJsRuntimeTypeName}'.");
            }

            var instanceField = defaultJsRuntimeType.GetField(InstanceFieldName, BindingFlags.Static | BindingFlags.NonPublic);
            if (instanceField == null)
            {
                throw new MissingMemberException($"Couldn't find property '{InstanceFieldName}' in '{DefaultJsRuntimeTypeName}'.");
            }

            return (IJSUnmarshalledRuntime)instanceField.GetValue(obj: null);
        }
    }
}
