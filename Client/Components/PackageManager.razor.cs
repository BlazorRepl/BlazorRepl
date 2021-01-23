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

    public partial class PackageManager : IAsyncDisposable
    {
        private DotNetObjectReference<PackageManager> dotNetInstance;

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
        public ICollection<Package> PackagePendingRestore { get; set; }

        [Parameter]
        public EventCallback<ICollection<Package>> PackagePendingRestoreChanged { get; set; }

        [CascadingParameter]
        private PageNotifications PageNotificationsComponent { get; set; }

        private string NuGetPackageName { get; set; }

        private IEnumerable<PackageLicenseInfo> PackagesToAcceptLicense { get; set; } = Enumerable.Empty<PackageLicenseInfo>();

        private bool LicensePopupVisible { get; set; }

        private string SelectedNuGetPackageName { get; set; }

        private string SelectedNuGetPackageVersion { get; set; }

        private IEnumerable<string> NuGetPackages { get; set; } = new List<string>();

        private IEnumerable<string> NuGetPackageVersions { get; set; } = new List<string>();

        private string VisibleClass => this.Visible ? "show" : string.Empty;

        private string DisplayStyle => this.Visible ? string.Empty : "display: none;";

        public ValueTask DisposeAsync()
        {
            this.dotNetInstance?.Dispose();
            this.PageNotificationsComponent?.Dispose();

            return ValueTask.CompletedTask;
        }

        public async Task RestoreSnippetPackages(Func<string, Task> updateStatusFunc)
        {
            var order = 1;
            var count = this.PackagePendingRestore.Count;
            foreach (var package in this.PackagePendingRestore)
            {
                await updateStatusFunc($"Restoring {package.Name} {order++}/{count}");
                await this.NuGetPackageManager.PreparePackageForDownloadAsync(package.Name, package.Version);

                await this.InstallNuGetPackageAsync();
            }

            this.PackagePendingRestore = new List<Package>();
            await this.PackagePendingRestoreChanged.InvokeAsync(this.PackagePendingRestore);
        }

        public IReadOnlyCollection<Package> GetInstalledPackages() => this.NuGetPackageManager.InstalledPackages;

        [JSInvokable]
        public Task CloseAsync() => this.CloseInternalAsync();

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                this.dotNetInstance = DotNetObjectReference.Create(this);
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        // TODO: rename to search and move to the NuGet package manager
        // TODO: handle no packages found (ex. "Newtonsoft.Json 12.0.3")
        private async Task GetNuGetPackages()
        {
            // Add constants
            var result = await this.Http.GetFromJsonAsync<IDictionary<string, object>>(
                $"https://api-v2v3search-0.nuget.org/autocomplete?q={this.NuGetPackageName}");

            // Add strongly typed model
            this.NuGetPackages = JsonSerializer.Deserialize<List<string>>(result["data"].ToString()).Take(10).ToList();
            this.SelectedNuGetPackageName = null;
        }

        // TODO: use method from NuGet package manager
        private async Task SelectNuGetPackage(string selectedPackage)
        {
            this.SelectedNuGetPackageName = selectedPackage;
            this.NuGetPackageName = selectedPackage;

            this.NuGetPackageVersions = new List<string>();

            // populate versions dropdown
            var versionsResult = await this.Http.GetFromJsonAsync<IDictionary<string, object>>(
                $"https://api.nuget.org/v3-flatcontainer/{selectedPackage}/index.json");

            // Add strongly typed model
            var versions = JsonSerializer.Deserialize<List<string>>(versionsResult["versions"].ToString());
            versions.Reverse();
            this.NuGetPackageVersions = versions;
            this.SelectedNuGetPackageVersion = this.NuGetPackageVersions.FirstOrDefault();
        }

        private async Task RestoreSnippetPackages()
        {
            foreach (var package in this.PackagePendingRestore)
            {
                await this.NuGetPackageManager.PreparePackageForDownloadAsync(package.Name, package.Version);

                await this.InstallNuGetPackageAsync();
            }

            this.PackagePendingRestore = new List<Package>();
            await this.PackagePendingRestoreChanged.InvokeAsync(this.PackagePendingRestore);
        }

        private async Task PreparePackageToInstallAsync()
        {
            var prepareResult = await this.NuGetPackageManager.PreparePackageForDownloadAsync(
                this.SelectedNuGetPackageName,
                this.SelectedNuGetPackageVersion);

            this.PackagesToAcceptLicense = prepareResult.PackagesLicenseInfo ?? Enumerable.Empty<PackageLicenseInfo>();

            if (this.PackagesToAcceptLicense.Any())
            {
                this.LicensePopupVisible = true;
            }
            else
            {
                await this.InstallNuGetPackageAsync();
            }
        }

        private void DeclineLicense()
        {
            this.NuGetPackageManager.CancelPackageInstallation();
            this.LicensePopupVisible = false;
        }

        // TODO: think about doing this in the repl component (it is the management component)
        private async Task InstallNuGetPackageAsync()
        {
            var sw = Stopwatch.StartNew();

            // TODO: extract custom object for the package contents to prevent filtering
            var packageContents = await this.NuGetPackageManager.DownloadPackagesContentsAsync();
            Console.WriteLine($"NuGetPackageManager.DownloadPackageContentsAsync - {sw.Elapsed}");

            sw.Restart();
            var dllsBytes = packageContents.Where(x => Path.GetExtension(x.Key) == ".dll").Select(x => x.Value);
            this.CompilationService.AddReferences(dllsBytes);
            Console.WriteLine($"CompilationService.AddReferences - {sw.Elapsed}");

            sw.Restart();

            // TODO: Move function to another JS module (+ the function for updating user components DLL) [proposal: ExecutionEngine]
            foreach (var (fileName, fileBytes) in packageContents)
            {
                this.UnmarshalledJsRuntime.InvokeUnmarshalled<string, string, byte[], object>(
                    "App.CodeExecution.storeNuGetPackageFile",
                    this.SessionId,
                    fileName,
                    fileBytes);
            }

            Console.WriteLine($"App.CodeExecution.storeNuGetPackageFile - {sw.Elapsed}");

            // consider separate notification from the installation
            this.PageNotificationsComponent.AddNotification(
                NotificationType.Info,
                $"{this.SelectedNuGetPackageName} package is successfully installed.");

            this.LicensePopupVisible = false;
            await this.CloseInternalAsync();
        }

        private Task CloseInternalAsync()
        {
            this.Visible = false;
            return this.VisibleChanged.InvokeAsync(this.Visible);
        }
    }
}
