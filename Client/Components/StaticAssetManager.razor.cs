namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Models;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class StaticAssetManager
    {
        private const string JsFileExtension = ".js";

        private static readonly string[] SupportedStaticAssetFileExtensions = { JsFileExtension, ".css" };

        [Inject]
        public IJSRuntime JsRuntime { get; set; }

        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public IDictionary<string, bool> Scripts { get; set; } = new Dictionary<string, bool>();

        [Parameter]
        public IDictionary<string, bool> Styles { get; set; } = new Dictionary<string, bool>();

        [Parameter]
        public EventCallback OnStaticAssetsUpdated { get; set; }

        [Parameter]
        public string SessionId { get; set; }

        [CascadingParameter]
        private Func<PageNotifications> GetPageNotificationsComponent { get; set; }

        private IDictionary<string, string> ScriptUrlToFileNameMappings { get; set; } = new Dictionary<string, string>();

        private IDictionary<string, string> StyleUrlToFileNameMappings { get; set; } = new Dictionary<string, string>();

        private string StaticAssetUrl { get; set; }

        private string DisplayStyle => this.Visible ? string.Empty : "display:none;";

        public override async Task SetParametersAsync(ParameterView parameters)
        {
            await base.SetParametersAsync(parameters);

            var updateStaticAssets = false;
            if (this.Scripts != null && this.Scripts.Any())
            {
                this.ScriptUrlToFileNameMappings = this.Scripts.ToDictionary(a => a.Key, a => Path.GetFileName(a.Key));
                updateStaticAssets = true;
            }

            if (this.Styles != null && this.Styles.Any())
            {
                this.StyleUrlToFileNameMappings = this.Styles.ToDictionary(a => a.Key, a => Path.GetFileName(a.Key));
                updateStaticAssets = true;
            }

            if (updateStaticAssets)
            {
                await this.JsRuntime.InvokeVoidAsync(
                    "App.CodeExecution.updateStaticAssets",
                    this.SessionId,
                    this.Scripts,
                    this.Styles);
            }
        }

        private async Task ToggleStyle(string url, bool enable)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            if (this.Styles.ContainsKey(url))
            {
                this.Styles[url] = enable;

                await this.JsRuntime.InvokeVoidAsync(
                    "App.CodeExecution.updateStaticAssets",
                    this.SessionId,
                    this.Scripts,
                    this.Styles);
            }
        }

        private async Task ToggleScript(string url, bool enable)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            if (this.Scripts.ContainsKey(url))
            {
                this.Scripts[url] = enable;

                await this.JsRuntime.InvokeVoidAsync(
                    "App.CodeExecution.updateStaticAssets",
                    this.SessionId,
                    this.Scripts,
                    this.Styles);
            }
        }

        private async Task AddStaticAssetAsync()
        {
            if (string.IsNullOrWhiteSpace(this.StaticAssetUrl))
            {
                return;
            }

            if (!Uri.TryCreate(this.StaticAssetUrl, UriKind.Absolute, out var uri))
            {
                this.GetPageNotificationsComponent().AddNotification(NotificationType.Error, "Invalid static file URL.");
                return;
            }

            var fileExtension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (!SupportedStaticAssetFileExtensions.Contains(fileExtension))
            {
                this.GetPageNotificationsComponent().AddNotification(
                    NotificationType.Error,
                    $"Static assets with extension '{fileExtension}' are not supported. Supported extensions: {string.Join(", ", SupportedStaticAssetFileExtensions)}");

                return;
            }

            if (fileExtension == JsFileExtension)
            {
                if (this.Scripts.ContainsKey(uri.AbsoluteUri))
                {
                    this.GetPageNotificationsComponent().AddNotification(NotificationType.Error, "Static asset already added.");
                    return;
                }

                this.Scripts.Add(uri.AbsoluteUri, true);
                this.ScriptUrlToFileNameMappings.Add(uri.AbsoluteUri, Path.GetFileName(uri.AbsolutePath));
            }
            else
            {
                if (this.Styles.ContainsKey(uri.AbsoluteUri))
                {
                    this.GetPageNotificationsComponent().AddNotification(NotificationType.Error, "Static asset already added.");
                    return;
                }

                this.Styles.Add(uri.AbsoluteUri, true);
                this.StyleUrlToFileNameMappings.Add(uri.AbsoluteUri, Path.GetFileName(uri.AbsolutePath));
            }

            await this.JsRuntime.InvokeVoidAsync("App.CodeExecution.updateStaticAssets", this.SessionId, this.Scripts, this.Styles);

            await this.OnStaticAssetsUpdated.InvokeAsync();

            this.StaticAssetUrl = null;
        }
    }
}
