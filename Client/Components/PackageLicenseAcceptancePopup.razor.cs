namespace BlazorRepl.Client.Components
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using BlazorRepl.Core.PackageInstallation;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class PackageLicenseAcceptancePopup
    {
        [Parameter]
        public IEnumerable<PackageLicenseInfo> PackagesToAcceptLicense { get; set; } = Enumerable.Empty<PackageLicenseInfo>();

        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public EventCallback<bool> VisibleChanged { get; set; }

        [Parameter]
        public EventCallback OnAccept { get; set; }

        [Parameter]
        public EventCallback OnReject { get; set; }

        private string VisibleClass => this.Visible ? "show" : string.Empty;

        private string DisplayStyle => this.Visible ? string.Empty : "display: none;";

        private async Task AcceptAsync()
        {
            await this.CloseInternalAsync();
            await this.OnAccept.InvokeAsync();
        }

        private async Task DeclineAsync()
        {
            // TODO: close from outside
            await this.CloseInternalAsync();
            await this.OnReject.InvokeAsync();
        }

        // call on reject
        private Task CloseInternalAsync()
        {
            this.Visible = false;
            return this.VisibleChanged.InvokeAsync(this.Visible);
        }
    }
}
