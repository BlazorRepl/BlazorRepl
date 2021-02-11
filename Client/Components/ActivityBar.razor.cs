namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using BlazorRepl.Core.PackageInstallation;
    using Microsoft.AspNetCore.Components;

    // TODO: Rename to ActivityManager
    public partial class ActivityBar
    {
        // Package Manager
        [Parameter]
        public bool PackageManagerVisible { get; set; }

        [Parameter]
        public EventCallback<bool> PackageManagerVisibleChanged { get; set; }

        [Parameter]
        public string SessionId { get; set; }

        [Parameter]
        public ICollection<Package> PackagesToRestore { get; set; }

        [Parameter]
        public Func<string, Task> UpdateLoaderTextAsync { get; set; }

        [Parameter]
        public bool Loading { get; set; }

        [Parameter]
        public EventCallback<bool> LoadingChanged { get; set; }

        // Static Asset Manager
        [Parameter]
        public bool StaticAssetManagerVisible { get; set; }

        [Parameter]
        public EventCallback<bool> StaticAssetManagerVisibleChanged { get; set; }

        private bool ActivityVisible => this.PackageManagerVisible || this.StaticAssetManagerVisible;

        private string ActivityVisibleClass => this.ActivityVisible ? "activity-manager-expanded" : "activity-manager-collapsed";

        private PackageManager PackageManagerComponent { get; set; }

        private StaticAssetManager StaticAssetManagerComponent { get; set; }

        private async Task TogglePackageManagerAsync()
        {
            if (this.StaticAssetManagerVisible)
            {
                await this.ToggleStaticAssetManagerAsync();
            }

            this.PackageManagerVisible = !this.PackageManagerVisible;
            await this.PackageManagerVisibleChanged.InvokeAsync(this.PackageManagerVisible);
        }

        private async Task ToggleStaticAssetManagerAsync()
        {
            if (this.PackageManagerVisible)
            {
                await this.TogglePackageManagerAsync();
            }

            this.StaticAssetManagerVisible = !this.StaticAssetManagerVisible;
            await this.StaticAssetManagerVisibleChanged.InvokeAsync(this.StaticAssetManagerVisible);
        }
    }
}
