namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Models;
    using BlazorRepl.Client.Services;
    using BlazorRepl.Core;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class SaveSnippetPopup : IAsyncDisposable
    {
        private DotNetObjectReference<SaveSnippetPopup> dotNetInstance;

        [Inject]
        public IJSRuntime JsRuntime { get; set; }

        [Inject]
        public SnippetsService SnippetsService { get; set; }

        [Inject]
        public NavigationManager NavigationManager { get; set; }

        [CascadingParameter]
        public PageNotifications PageNotificationsComponent { get; set; }

        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public EventCallback<bool> VisibleChanged { get; set; }

        [Parameter]
        public string InvokerId { get; set; }

        [Parameter]
        public IEnumerable<CodeFile> CodeFiles { get; set; } = Enumerable.Empty<CodeFile>();

        [Parameter]
        public Func<Task> UpdateActiveCodeFileContentFunc { get; set; }

        private bool Loading { get; set; }

        private string SnippetLink { get; set; }

        private bool SnippetLinkCopied { get; set; }

        private string VisibleClass => this.Visible ? "show" : string.Empty;

        private string CopyButtonIcon => this.SnippetLinkCopied ? "icon-check" : "icon-copy";

        private string DisplayStyle => this.Visible ? string.Empty : "display: none;";

        public ValueTask DisposeAsync()
        {
            this.dotNetInstance?.Dispose();
            this.PageNotificationsComponent?.Dispose();

            return this.JsRuntime.InvokeVoidAsync("App.SaveSnippetPopup.dispose");
        }

        [JSInvokable]
        public Task CloseAsync() => this.CloseInternalAsync();

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                this.dotNetInstance = DotNetObjectReference.Create(this);

                await this.JsRuntime.InvokeVoidAsync(
                    "App.SaveSnippetPopup.init",
                    "save-snippet-popup",
                    this.InvokerId,
                    this.dotNetInstance);
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        private async Task CopyLinkToClipboardAsync()
        {
            await this.JsRuntime.InvokeVoidAsync("App.copyToClipboard", this.SnippetLink);
            this.SnippetLinkCopied = true;
        }

        private async Task SaveAsync()
        {
            this.Loading = true;

            try
            {
                await (this.UpdateActiveCodeFileContentFunc?.Invoke() ?? Task.CompletedTask);

                var snippetId = await this.SnippetsService.SaveSnippetAsync(this.CodeFiles);

                var urlBuilder = new UriBuilder(this.NavigationManager.BaseUri) { Path = $"repl/{snippetId}" };
                var url = urlBuilder.Uri.ToString();
                this.SnippetLink = url;

                await this.JsRuntime.InvokeVoidAsync("App.changeDisplayUrl", url);
            }
            catch (InvalidOperationException ex)
            {
                this.PageNotificationsComponent.AddNotification(NotificationType.Error, content: ex.Message);
            }
            catch (Exception)
            {
                this.PageNotificationsComponent.AddNotification(
                    NotificationType.Error,
                    content: "Error while saving snippet. Please try again later.");
            }
            finally
            {
                this.Loading = false;
            }
        }

        private Task CloseInternalAsync()
        {
            this.Visible = false;
            this.SnippetLink = null;
            this.SnippetLinkCopied = false;
            return this.VisibleChanged.InvokeAsync(this.Visible);
        }
    }
}
