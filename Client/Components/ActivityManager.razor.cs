namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using BlazorRepl.Core.PackageInstallation;
    using Microsoft.AspNetCore.Components;

    public partial class ActivityManager
    {
        [Parameter]
        public string SessionId { get; set; }

        [Parameter]
        public ICollection<Package> PackagesToRestore { get; set; }

        [Parameter]
        public Func<string, Task> UpdateLoaderTextAsync { get; set; }

        [Parameter]
        public bool ShowLoader { get; set; }

        [Parameter]
        public EventCallback<bool> ShowLoaderChanged { get; set; }

        [Parameter]
        public EventCallback<bool> VisibleChanged { get; set; }

        private PackageManager PackageManagerComponent { get; set; }

        private bool PackageManagerVisible { get; set; }

        private bool StaticAssetManagerVisible { get; set; }

        private bool ActivityVisible => this.PackageManagerVisible || this.StaticAssetManagerVisible;

        private string ActivityVisibleClass => this.ActivityVisible ? "activity-manager-expanded" : "activity-manager-collapsed";

        private string PackageManagerActivityActiveClass => this.PackageManagerVisible ? "active-activity-option" : string.Empty;

        private string StaticAssetManagerActivityActiveClass => this.StaticAssetManagerVisible ? "active-activity-option" : string.Empty;

        internal IEnumerable<Package> GetInstalledPackages() => this.PackageManagerComponent?.GetInstalledPackages();

        internal Task RestorePackagesAsync() => this.PackageManagerComponent?.RestorePackagesAsync();

        private async Task TogglePackageManagerAsync()
        {
            if (this.StaticAssetManagerVisible)
            {
                await this.ToggleStaticAssetManagerAsync();
            }

            this.PackageManagerVisible = !this.PackageManagerVisible;

            await this.VisibleChanged.InvokeAsync(this.ActivityVisible);
        }

        private async Task ToggleStaticAssetManagerAsync()
        {
            if (this.PackageManagerVisible)
            {
                await this.TogglePackageManagerAsync();
            }

            this.StaticAssetManagerVisible = !this.StaticAssetManagerVisible;

            await this.VisibleChanged.InvokeAsync(this.ActivityVisible);
        }
    }
}
