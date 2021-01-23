namespace BlazorRepl.Client.Pages
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Components;
    using BlazorRepl.Client.Models;
    using BlazorRepl.Client.Services;
    using BlazorRepl.Core;
    using BlazorRepl.Core.PackageInstallation;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class Repl : IDisposable
    {
        private const string MainComponentCodePrefix = "@page \"/__main\"\n";
        private const string MainUserPagePath = "/__main";

        private DotNetObjectReference<Repl> dotNetInstance;
        private string errorMessage;
        private CodeFile activeCodeFile;

        [Inject]
        public SnippetsService SnippetsService { get; set; }

        [Inject]
        public CompilationService CompilationService { get; set; }

        [Inject]
        public IJSInProcessRuntime JsRuntime { get; set; }

        [Inject]
        public IJSUnmarshalledRuntime UnmarshalledJsRuntime { get; set; }

        [Parameter]
        public string SnippetId { get; set; }

        public CodeEditor CodeEditorComponent { get; set; }

        public IDictionary<string, CodeFile> CodeFiles { get; set; } = new Dictionary<string, CodeFile>();

        [CascadingParameter]
        private PageNotifications PageNotificationsComponent { get; set; }

        private IList<string> CodeFileNames { get; set; } = new List<string>();

        private string CodeEditorContent => this.activeCodeFile?.Content;

        private PackageManager PackageManager { get; set; }

        private IReadOnlyCollection<Package> InstalledPackages => this.PackageManager?.GetInstalledPackages();

        private ICollection<Package> PackagePendingRestore { get; set; } = Array.Empty<Package>();

        private bool SaveSnippetPopupVisible { get; set; }

        private string Preset { get; set; } = "basic";

        private IReadOnlyCollection<CompilationDiagnostic> Diagnostics { get; set; } = Array.Empty<CompilationDiagnostic>();

        private bool AreDiagnosticsShown { get; set; }

        private string LoaderText { get; set; }

        private bool Loading { get; set; }

        private string SessionId { get; set; }

        [JSInvokable]
        public async Task TriggerCompileAsync()
        {
            await this.CompileAsync();

            this.StateHasChanged();
        }

        public void Dispose()
        {
            this.dotNetInstance?.Dispose();
            this.PageNotificationsComponent?.Dispose();

            this.JsRuntime.InvokeVoid("App.Repl.dispose");
        }

        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                this.dotNetInstance = DotNetObjectReference.Create(this);

                this.JsRuntime.InvokeVoid(
                    "App.Repl.init",
                    "user-code-editor-container",
                    "user-page-window-container",
                    "user-code-editor",
                    this.dotNetInstance);
            }

            if (!string.IsNullOrWhiteSpace(this.errorMessage) && this.PageNotificationsComponent != null)
            {
                this.PageNotificationsComponent.AddNotification(NotificationType.Error, content: this.errorMessage);

                this.errorMessage = null;
            }

            base.OnAfterRender(firstRender);
        }

        protected override async Task OnInitializedAsync()
        {
            this.PageNotificationsComponent?.Clear();

            if (!string.IsNullOrWhiteSpace(this.SnippetId))
            {
                try
                {
                    var snippetResponse = await this.SnippetsService.GetSnippetContentAsync(this.SnippetId);
                    this.CodeFiles = snippetResponse?.Files?.ToDictionary(f => f.Path, f => f);
                    if (!(this.CodeFiles?.Any() ?? false))
                    {
                        this.errorMessage = "No files in snippet.";
                    }
                    else
                    {
                        this.activeCodeFile = this.CodeFiles.First().Value;
                        this.PackagePendingRestore = snippetResponse.InstalledPackages.ToList();
                    }
                }
                catch (ArgumentException)
                {
                    this.errorMessage = "Invalid Snippet ID.";
                }
                catch (Exception)
                {
                    this.errorMessage = "Unable to get snippet content. Please try again later.";
                }
            }

            if (!this.CodeFiles.Any())
            {
                this.activeCodeFile = new CodeFile
                {
                    Path = CoreConstants.MainComponentFilePath,
                    Content = CoreConstants.MainComponentDefaultFileContent,
                };
                this.CodeFiles.Add(CoreConstants.MainComponentFilePath, this.activeCodeFile);
            }

            this.CodeFileNames = this.CodeFiles.Keys.ToList();

            await base.OnInitializedAsync();
        }

        private async Task CompileAsync()
        {
            this.Loading = true;
            this.LoaderText = "Processing";

            await Task.Delay(10); // Ensure rendering has time to be called

            if (this.PackagePendingRestore.Any())
            {
                await this.PackageManager.RestoreSnippetPackages(this.UpdateLoaderTextAsync);
                await this.UpdateLoaderTextAsync("Prepare components for compilation");
            }

            CompileToAssemblyResult compilationResult = null;
            CodeFile mainComponent = null;
            string originalMainComponentContent = null;
            try
            {
                this.UpdateActiveCodeFileContent();

                // Add the necessary main component code prefix and store the original content so we can revert right after compilation.
                if (this.CodeFiles.TryGetValue(CoreConstants.MainComponentFilePath, out mainComponent))
                {
                    originalMainComponentContent = mainComponent.Content;
                    mainComponent.Content = MainComponentCodePrefix + originalMainComponentContent;
                }

                compilationResult = await this.CompilationService.CompileToAssemblyAsync(
                    this.CodeFiles.Values,
                    this.Preset,
                    this.UpdateLoaderTextAsync);

                this.Diagnostics = compilationResult.Diagnostics.OrderByDescending(x => x.Severity).ThenBy(x => x.Code).ToList();
                this.AreDiagnosticsShown = true;
            }
            catch (Exception)
            {
                this.PageNotificationsComponent.AddNotification(NotificationType.Error, content: "Error while compiling the code.");
            }
            finally
            {
                if (mainComponent != null)
                {
                    mainComponent.Content = originalMainComponentContent;
                }

                this.Loading = false;
            }

            if (compilationResult?.AssemblyBytes?.Length > 0)
            {
                this.UnmarshalledJsRuntime.InvokeUnmarshalled<byte[], object>(
                    "App.Repl.updateUserAssemblyInCacheStorage",
                    compilationResult.AssemblyBytes);

                var userPagePath = string.IsNullOrWhiteSpace(this.SessionId)
                    ? MainUserPagePath
                    : $"{MainUserPagePath}#{this.SessionId}";

                // TODO: Add error page in iframe
                this.JsRuntime.InvokeVoid("App.reloadIFrame", "user-page-window", MainUserPagePath);
            }
        }

        private void ShowSaveSnippetPopup() => this.SaveSnippetPopupVisible = true;

        private void HandleTabActivate(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            this.UpdateActiveCodeFileContent();

            if (this.CodeFiles.TryGetValue(name, out var codeFile))
            {
                this.activeCodeFile = codeFile;

                this.CodeEditorComponent.Focus();
            }
        }

        private void HandleTabClose(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            this.CodeFiles.Remove(name);
        }

        private void HandleTabCreate(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(name);

            this.CodeFiles.TryAdd(name, new CodeFile { Path = name, Content = $"<h1>{nameWithoutExtension}</h1>" });

            this.JsRuntime.InvokeVoid("App.Repl.setCodeEditorContainerHeight");
        }

        private void UpdateActiveCodeFileContent()
        {
            if (this.activeCodeFile == null)
            {
                this.PageNotificationsComponent.AddNotification(NotificationType.Error, "No active file to update.");
                return;
            }

            this.activeCodeFile.Content = this.CodeEditorComponent.GetCode();
        }

        private Task UpdateLoaderTextAsync(string loaderText)
        {
            this.LoaderText = loaderText;

            this.StateHasChanged();

            return Task.Delay(10); // Ensure rendering has time to be called
        }
    }
}
