namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Services;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class NugetPackageInstallerPopup : IDisposable
    {
        private DotNetObjectReference<NugetPackageInstallerPopup> dotNetInstance;

        [Inject]
        public IJSRuntime JsRuntime { get; set; }

        [Inject]
        public HttpClient Http { get; set; }

        [Inject]
        public SnippetsService SnippetsService { get; set; }

        [CascadingParameter]
        public PageNotifications PageNotificationsComponent { get; set; }

        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public EventCallback<bool> VisibleChanged { get; set; }

        public string NugetPackageName { get; set; }

        public List<string> NugetPackages { get; set; } = new List<string>();

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

                await this.JsRuntime.InvokeVoidAsync(
                    "App.NugetPackageInstallerPopup.init",
                    this.dotNetInstance);
            }

            await base.OnAfterRenderAsync(firstRender);
        }


        private async Task GetNugetPackages()
        {
            var result = await this.Http.GetFromJsonAsync<IDictionary<string, object>>($"https://api-v2v3search-0.nuget.org/autocomplete?q={NugetPackageName}");


            this.NugetPackages = JsonSerializer.Deserialize<List<string>>(result["data"].ToString());
        }

        private Task CloseInternalAsync()
        {
            this.Visible = false;
            return this.VisibleChanged.InvokeAsync(this.Visible);
        }
    }
}
