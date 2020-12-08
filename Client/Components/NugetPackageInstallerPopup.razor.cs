namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Services;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;
    using System.IO.Compression;
    using System.IO;

    using BlazorRepl.Core;

    using Microsoft.Extensions.Logging;

    using NuGet.Common;
    using NuGet.DependencyResolver;
    using NuGet.Frameworks;
    using NuGet.LibraryModel;
    using NuGet.Protocol.Core.Types;
    using NuGet.RuntimeModel;
    using NuGet.Versioning;

    public partial class NugetPackageInstallerPopup : IDisposable
    {
        private DotNetObjectReference<NugetPackageInstallerPopup> dotNetInstance;

        [Inject]
        public IJSRuntime JsRuntime { get; set; }

        [Inject]
        public HttpClient Http { get; set; }

        [Inject]
        public CompilationService CompilationService { get; set; }

        [Inject]
        public SnippetsService SnippetsService { get; set; }

        [CascadingParameter]
        public PageNotifications PageNotificationsComponent { get; set; }

        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public EventCallback<bool> VisibleChanged { get; set; }

        [Parameter]
        public string SessionId { get; set; }

        [Parameter]
        public EventCallback<string> SessionIdChanged { get; set; }

        public string NugetPackageName { get; set; }

        public string SelectedNugetPackageName { get; set; }

        public string SelectedNugetPackageVersion { get; set; }

        public List<string> NugetPackages { get; set; } = new List<string>();

        public List<string> NugetPackageVersions { get; set; } = new List<string>();

        public string VisibleClass => this.Visible ? "show" : string.Empty;

        public string DisplayStyle => this.Visible ? string.Empty : "display: none;";

        [JSInvokable]
        public Task CloseAsync() => this.CloseInternalAsync();

        public void Dispose()
        {
            this.dotNetInstance?.Dispose();
            this.PageNotificationsComponent?.Dispose();

            _ = this.JsRuntime.InvokeVoidAsync("App.NugetPackageInstallerPopup.dispose");
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                this.dotNetInstance = DotNetObjectReference.Create(this);

                this.SessionId = await this.JsRuntime.InvokeAsync<string>(
                    "App.NugetPackageInstallerPopup.init",
                    this.dotNetInstance);
                await this.SessionIdChanged.InvokeAsync(this.SessionId);
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        private async Task GetNugetPackages()
        {
            var result = await this.Http.GetFromJsonAsync<IDictionary<string, object>>(
                $"https://api-v2v3search-0.nuget.org/autocomplete?q={NugetPackageName}");

            this.NugetPackages = JsonSerializer.Deserialize<List<string>>(result["data"].ToString()).Take(5).ToList();
            this.SelectedNugetPackageName = null;
        }

        private async Task SelectNugetPackage(string selectedPackage)
        {
            this.SelectedNugetPackageName = selectedPackage;

            // populate versions dropdown
            var versionsResult = await this.Http.GetFromJsonAsync<IDictionary<string, object>>(
                $"https://api.nuget.org/v3-flatcontainer/{selectedPackage}/index.json");
            this.NugetPackageVersions = JsonSerializer.Deserialize<List<string>>(versionsResult["versions"].ToString());
            this.NugetPackageVersions.Reverse();
            this.SelectedNugetPackageVersion = this.NugetPackageVersions.FirstOrDefault();
        }

        private async Task InstallNugetPackage()
        {
            var rp = new DepProvider(this.Http);
            //var deps = await rp.GetDependenciesAsync(
            //    new LibraryIdentity("Blazored.Modal", new NuGetVersion(5, 1, 0), LibraryType.Package),
            //    NuGetFramework.Parse("net5.0"),
            //    new NullSourceCacheContext(),
            //    new NullLogger(),
            //    default);

            //Console.WriteLine(JsonSerializer.Serialize(deps));
            //return;

            var ctx = new RemoteWalkContext(new NullSourceCacheContext(), new NullLogger());
            ctx.RemoteLibraryProviders.Add(rp);
            var walker = new RemoteDependencyWalker(ctx);

            var res = await walker.WalkAsync(
                new LibraryRange("Blazored.Modal", LibraryDependencyTarget.All),
                new NuGetFramework("net5.0"),
                "net5.0",
                new RuntimeGraph(),
                recursive: true);

            Console.WriteLine(JsonSerializer.Serialize(res));

            return;

            var package = await this.Http.GetByteArrayAsync(
                $"https://api.nuget.org/v3-flatcontainer/{SelectedNugetPackageName}/{SelectedNugetPackageVersion}/{SelectedNugetPackageName}.{SelectedNugetPackageVersion}.nupkg");

            using var zippedStream = new MemoryStream(package);
            using var archive = new ZipArchive(zippedStream);
            var entry = archive.Entries.FirstOrDefault();

            Console.WriteLine(string.Join(',', archive.Entries.Select(e => e.FullName)));

            // get only the dll that we need
            // we could have more than one dll (netstandard2.0, netstandard2.1... folders)
            var dllEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".dll"));
            if (dllEntry != null)
            {
                // reuse AddZipEntryToCache
                using var dllMemoryStream = new MemoryStream();
                using var dllStream = dllEntry.Open();
                dllStream.CopyTo(dllMemoryStream);

                var dllBytes = dllMemoryStream.ToArray();
                this.CompilationService.AddReference(dllBytes);

                var dllBase64 = Convert.ToBase64String(dllBytes);
                await this.JsRuntime.InvokeVoidAsync(
                   "App.NugetPackageInstallerPopup.addNugetFileToCache",
                   dllEntry.Name,
                   dllBase64);

                var cssEntries = archive.Entries.Where(e => e.FullName.EndsWith(".css"));
                foreach (var cssEntry in cssEntries)
                {
                    // do we need this check?
                    if (cssEntry != null)
                    {
                        await this.AddZipEntryToCache(cssEntry);
                    }
                }

                var jsEntries = archive.Entries.Where(e => e.FullName.EndsWith(".js"));
                foreach (var jsEntry in jsEntries)
                {
                    // do we need this check?
                    if (jsEntry != null)
                    {
                        await this.AddZipEntryToCache(jsEntry);
                    }
                }

                this.Visible = false;
                await this.VisibleChanged.InvokeAsync(this.Visible);
            }
        }

        private Task CloseInternalAsync()
        {
            this.Visible = false;
            return this.VisibleChanged.InvokeAsync(this.Visible);
        }

        private async Task AddZipEntryToCache(ZipArchiveEntry entry)
        {
            using var memoryStream = new MemoryStream();
            using var stream = entry.Open();
            stream.CopyTo(memoryStream);

            var bytes = memoryStream.ToArray();

            var base64 = Convert.ToBase64String(bytes);
            await this.JsRuntime.InvokeVoidAsync(
               "App.NugetPackageInstallerPopup.addNugetFileToCache",
               entry.Name,
               base64);
        }
    }
}
