namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Models;
    using BlazorRepl.Client.Services;
    using BlazorRepl.Core;
    using BlazorRepl.Core.PackageInstallation;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class NugetPackageInstallerPopup : IAsyncDisposable
    {
        private DotNetObjectReference<NugetPackageInstallerPopup> dotNetInstance;

        [Inject]
        public IJSUnmarshalledRuntime UnmarshalledJsRuntime { get; set; }

        [Inject]
        public IJSRuntime JsRuntime { get; set; }

        [Inject]
        public HttpClient Http { get; set; }

        [Inject]
        public CompilationService CompilationService { get; set; }

        [Inject]
        public SnippetsService SnippetsService { get; set; }

        [Inject]
        public NuGetPackageManager NuGetPackageManager { get; set; }

        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public EventCallback<bool> VisibleChanged { get; set; }

        [Parameter]
        public string SessionId { get; set; }

        [Parameter]
        public EventCallback<string> SessionIdChanged { get; set; }

        [CascadingParameter]
        private PageNotifications PageNotificationsComponent { get; set; }

        private string NugetPackageName { get; set; }

        private string SelectedNugetPackageName { get; set; }

        private string SelectedNugetPackageVersion { get; set; }

        private List<string> NugetPackages { get; set; } = new List<string>();

        private List<string> NugetPackageVersions { get; set; } = new List<string>();

        private string VisibleClass => this.Visible ? "show" : string.Empty;

        private string DisplayStyle => this.Visible ? string.Empty : "display: none;";

        [JSInvokable]
        public Task CloseAsync() => this.CloseInternalAsync();

        public ValueTask DisposeAsync()
        {
            this.dotNetInstance?.Dispose();
            this.PageNotificationsComponent?.Dispose();

            return this.JsRuntime.InvokeVoidAsync("App.NugetPackageInstallerPopup.dispose");
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

        // TODO: rename to search and move to the NuGet package manager
        private async Task GetNugetPackages()
        {
            var result = await this.Http.GetFromJsonAsync<IDictionary<string, object>>(
                $"https://api-v2v3search-0.nuget.org/autocomplete?q={this.NugetPackageName}");

            this.NugetPackages = JsonSerializer.Deserialize<List<string>>(result["data"].ToString()).Take(5).ToList();
            this.SelectedNugetPackageName = null;
        }

        // TODO: use method from NuGet package manager
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

        // TODO: think about doing this in the repl component (it is the management component)
        private async Task InstallNugetPackage()
        {
            var sw = Stopwatch.StartNew();

            // TODO: extract custom object for the package contents to prevent filtering
            var packageContents = await this.NuGetPackageManager.DownloadPackageContentsAsync(
                this.SelectedNugetPackageName,
                this.SelectedNugetPackageVersion);
            Console.WriteLine($"NuGetPackageManager.DownloadPackageContentsAsync - {sw.Elapsed}");

            sw.Restart();
            var dllsBytes = packageContents.Where(x => Path.GetExtension(x.Key) == ".dll").Select(x => x.Value);
            this.CompilationService.AddReferences(dllsBytes);
            Console.WriteLine($"CompilationService.AddReferences - {sw.Elapsed}");

            sw.Restart();
            // TODO: Move function to another JS module (+ the function for updating user components DLL) [proposal: ExecutionEngine]
            foreach (var (fileName, fileBytes) in packageContents)
            {
                this.UnmarshalledJsRuntime.InvokeUnmarshalled<string, byte[], object>(
                    "App.NugetPackageInstallerPopup.addPackageFilesToCache",
                    fileName,
                    fileBytes);
            }
            Console.WriteLine($"App.NugetPackageInstallerPopup.addPackageFilesToCache - {sw.Elapsed}");

            this.PageNotificationsComponent.AddNotification(
                NotificationType.Info,
                $"{this.SelectedNugetPackageName} package is successfully installed.");

            await this.CloseInternalAsync();
        }

        private Task CloseInternalAsync()
        {
            this.Visible = false;
            return this.VisibleChanged.InvokeAsync(this.Visible);
        }
    }
}
