namespace BlazorRepl.Client.Components
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using BlazorRepl.Core.PackageInstallation;
    using Microsoft.AspNetCore.Components;

    public partial class PackageLicensePopup
    {
        [Parameter]
        public IEnumerable<PackageLicenseInfo> PackagesToAcceptLicense { get; set; } = Enumerable.Empty<PackageLicenseInfo>();

        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public EventCallback OnAccept { get; set; }

        [Parameter]
        public EventCallback OnDecline { get; set; }

        private string VisibleClass => this.Visible ? "show" : string.Empty;

        private string DisplayStyle => this.Visible ? string.Empty : "display: none;";

        private Task AcceptAsync() => this.OnAccept.InvokeAsync();

        private Task DeclineAsync() => this.OnDecline.InvokeAsync();
    }
}
