namespace BlazorRepl.Client.Components
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class TabSettingsPopup : IDisposable
    {
        private DotNetObjectReference<TabSettingsPopup> dotNetInstance;

        [Inject]
        public IJSInProcessRuntime JsRuntime { get; set; }

        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public EventCallback<bool> VisibleChanged { get; set; }

        [Parameter]
        public EventCallback OnScaffoldStartupSettingClick { get; set; }

        private string VisibleClass => this.Visible ? "show" : string.Empty;

        private string DisplayStyle => this.Visible ? string.Empty : "display: none;";

        public void Dispose()
        {
            this.dotNetInstance?.Dispose();

            this.JsRuntime.InvokeVoid("App.TabSettingsPopup.dispose");
        }

        [JSInvokable]
        public Task CloseAsync() => this.CloseInternalAsync();

        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                this.dotNetInstance = DotNetObjectReference.Create(this);

                this.JsRuntime.InvokeVoid("App.TabSettingsPopup.init", "tab-settings-popup", "tab-settings-btn", this.dotNetInstance);
            }

            base.OnAfterRender(firstRender);
        }

        private async Task ScaffoldStartupClass()
        {
            await this.OnScaffoldStartupSettingClick.InvokeAsync();

            await this.CloseInternalAsync();
        }

        private Task CloseInternalAsync()
        {
            this.Visible = false;
            return this.VisibleChanged.InvokeAsync(this.Visible);
        }
    }
}
