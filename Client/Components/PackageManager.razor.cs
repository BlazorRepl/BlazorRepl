namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Models;
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
        public CompilationService CompilationService { get; set; }

        [Inject]
        public NuGetPackageManagementService NuGetPackageManagementService { get; set; }

        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public EventCallback<bool> VisibleChanged { get; set; }

        [Parameter]
        public string SessionId { get; set; }

        [Parameter]
        public ICollection<Package> PackagesToRestore { get; set; }

        [Parameter]
        public bool Loading { get; set; }

        [Parameter]
        public EventCallback<bool> LoadingChanged { get; set; }

        [Parameter]
        public Func<string, Task> UpdateLoaderTextFunc { get; set; }

        [CascadingParameter]
        private PageNotifications PageNotificationsComponent { get; set; }

        private string PackageSearchQuery { get; set; }

        private bool PackageSearchResultsFetched { get; set; }

        private string SelectedPackageName { get; set; }

        private string SelectedPackageVersion { get; set; }

        private IEnumerable<string> Packages { get; set; } = Enumerable.Empty<string>();

        private IEnumerable<string> PackageVersions { get; set; } = Enumerable.Empty<string>();

        private IEnumerable<PackageLicenseInfo> PackagesToAcceptLicense { get; set; } = Enumerable.Empty<PackageLicenseInfo>();

        private bool LicensePopupVisible { get; set; }

        private string VisibleClass => this.Visible ? "show" : string.Empty;

        private string DisplayStyle => this.Visible ? string.Empty : "display: none;";

        public IReadOnlyCollection<Package> GetInstalledPackages() => this.NuGetPackageManagementService.InstalledPackages;

        public async Task RestorePackagesAsync(bool handleLoading = false)
        {
            if (handleLoading)
            {
                await this.ToggleLoadingAsync(true);
            }

            var index = 1;
            foreach (var package in this.PackagesToRestore)
            {
                if (this.UpdateLoaderTextFunc != null)
                {
                    await this.UpdateLoaderTextFunc($"[{index}/{this.PackagesToRestore.Count}] Restoring package: {package.Name}");
                    index++;
                }

                await this.NuGetPackageManagementService.PreparePackageForDownloadAsync(package.Name, package.Version);

                await this.InstallPackageAsync();
            }

            this.PackagesToRestore.Clear();

            if (handleLoading)
            {
                await this.ToggleLoadingAsync(false);
            }
        }

        public ValueTask DisposeAsync()
        {
            this.dotNetInstance?.Dispose();
            this.PageNotificationsComponent?.Dispose();

            return ValueTask.CompletedTask;
        }

        [JSInvokable]
        public Task CloseAsync() => this.CloseInternalAsync();

        protected override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                this.dotNetInstance = DotNetObjectReference.Create(this);
            }

            return base.OnAfterRenderAsync(firstRender);
        }

        // TODO: handle no packages found (ex. "Newtonsoft.Json 12.0.3")
        private async Task SearchPackagesAsync()
        {
            this.Packages = await this.NuGetPackageManagementService.SearchPackagesAsync(this.PackageSearchQuery);

            this.SelectedPackageName = null;
            this.PackageSearchResultsFetched = true;
        }

        private async Task SelectPackageAsync(string selectedPackage)
        {
            this.SelectedPackageName = selectedPackage;

            this.PackageVersions = await this.NuGetPackageManagementService.GetPackageVersionsAsync(this.SelectedPackageName);

            this.SelectedPackageVersion = this.PackageVersions.FirstOrDefault();
        }

        private async Task PreparePackageToInstallAsync()
        {
            var prepareResult = await this.NuGetPackageManagementService.PreparePackageForDownloadAsync(
                this.SelectedPackageName,
                this.SelectedPackageVersion);

            this.PackagesToAcceptLicense = prepareResult.PackagesToAcceptLicense ?? Enumerable.Empty<PackageLicenseInfo>();

            if (this.PackagesToAcceptLicense.Any())
            {
                this.LicensePopupVisible = true;
            }
            else
            {
                await this.AcceptPackageLicenseAsync();
            }
        }

        private void DeclinePackageLicense()
        {
            this.NuGetPackageManagementService.CancelPackageInstallation();

            this.LicensePopupVisible = false;
        }

        private async Task AcceptPackageLicenseAsync()
        {
            this.LicensePopupVisible = false;

            await this.ToggleLoadingAsync(true);

            if (this.UpdateLoaderTextFunc != null)
            {
                await this.UpdateLoaderTextFunc($"Installing package: {this.SelectedPackageName}");
            }

            await this.InstallPackageAsync();

            this.PageNotificationsComponent.AddNotification(
                NotificationType.Info,
                $"{this.SelectedPackageName} package is successfully installed.");

            await this.ToggleLoadingAsync(false);

            this.PackageSearchQuery = null;
            this.SelectedPackageName = null;
            this.SelectedPackageVersion = null;
            this.Packages = Enumerable.Empty<string>();
            this.PackageVersions = Enumerable.Empty<string>();
            this.PackageSearchResultsFetched = false;
        }

        private async Task InstallPackageAsync()
        {
            var sw = Stopwatch.StartNew();

            var packagesContents = await this.NuGetPackageManagementService.DownloadPackagesContentsAsync();
            Console.WriteLine($"NuGetPackageManager.DownloadPackageContentsAsync - {sw.Elapsed}");

            sw.Restart();
            this.CompilationService.AddAssemblyReferences(packagesContents.DllFiles.Values);
            Console.WriteLine($"CompilationService.AddReferences - {sw.Elapsed}");

            sw.Restart();

            var allPackageFiles = packagesContents.DllFiles.Concat(packagesContents.JavaScriptFiles).Concat(packagesContents.CssFiles);
            foreach (var (fileName, fileBytes) in allPackageFiles)
            {
                this.UnmarshalledJsRuntime.InvokeUnmarshalled<string, string, byte[], object>(
                    "App.CodeExecution.storePackageFile",
                    this.SessionId,
                    fileName,
                    fileBytes);
            }

            Console.WriteLine($"App.CodeExecution.storePackageFile - {sw.Elapsed}");
        }

        private Task CloseInternalAsync()
        {
            this.Visible = false;
            return this.VisibleChanged.InvokeAsync(this.Visible);
        }

        private Task ToggleLoadingAsync(bool value)
        {
            this.Loading = value;
            return this.LoadingChanged.InvokeAsync(this.Loading);
        }
    }
}
