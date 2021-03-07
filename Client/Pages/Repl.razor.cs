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
        private CodeFile activeCodeFile;

        [Inject]
        public SnippetsService SnippetsService { get; set; }

        [Inject]
        public CompilationService CompilationService { get; set; }

        [Inject]
        public IJSInProcessRuntime JsRuntime { get; set; }

        [Parameter]
        public string SnippetId { get; set; }

        [CascadingParameter]
        private Func<PageNotifications> GetPageNotificationsComponent { get; set; }

        private CodeEditor CodeEditorComponent { get; set; }

        private IDictionary<string, CodeFile> CodeFiles { get; set; } = new Dictionary<string, CodeFile>();

        private IList<string> CodeFileNames { get; set; } = new List<string>();

        private string CodeEditorPath => this.activeCodeFile?.Path;

        private string CodeEditorContent => this.activeCodeFile?.Content;

        private CodeFileType CodeFileType => this.activeCodeFile?.Type ?? CodeFileType.Razor;

        private PackageManager PackageManagerComponent { get; set; }

        private IReadOnlyCollection<Package> InstalledPackages =>
            this.PackageManagerComponent?.GetInstalledPackages() ?? Array.Empty<Package>();

        private int InstalledPackagesCount => this.InstalledPackages.Count;

        private ICollection<Package> PackagesToRestore { get; set; } = new List<Package>();

        private StaticAssets StaticAssets { get; } = new();

        private int StaticAssetsCount { get; set; }

        private bool PackageManagerVisible { get; set; }

        private bool StaticAssetManagerVisible { get; set; }

        private bool SaveSnippetPopupVisible { get; set; }

        private string ActivitySidebarExpandedClass =>
            this.PackageManagerVisible || this.StaticAssetManagerVisible ? "activity-sidebar-expanded" : string.Empty;

        private string SplittableContainerClass =>
            this.PackageManagerVisible || this.StaticAssetManagerVisible ? "splittable-container-shrunk" : "splittable-container-full";

        private IReadOnlyCollection<CompilationDiagnostic> Diagnostics { get; set; } = Array.Empty<CompilationDiagnostic>();

        private bool AreDiagnosticsShown { get; set; }

        private string LoaderText { get; set; }

        private bool ShowLoader { get; set; }

        private string SessionId { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

        [JSInvokable]
        public async Task TriggerCompileAsync()
        {
            await this.CompileAsync();

            this.StateHasChanged();
        }

        public void Dispose()
        {
            this.dotNetInstance?.Dispose();

            this.GetPageNotificationsComponent()?.Dispose();

            this.JsRuntime.InvokeVoid("App.Repl.dispose", this.SessionId);
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
                    this.dotNetInstance);
            }

            base.OnAfterRender(firstRender);
        }

        protected override async Task OnInitializedAsync()
        {
            await this.CompilationService.InitializeAsync();

            this.GetPageNotificationsComponent().Clear();

            if (!string.IsNullOrWhiteSpace(this.SnippetId))
            {
                try
                {
                    var snippetResponse = await this.SnippetsService.GetSnippetContentAsync(this.SnippetId);

                    this.CodeFiles = snippetResponse.Files?.ToDictionary(f => f.Path, f => f) ?? new Dictionary<string, CodeFile>();
                    if (!this.CodeFiles.Any())
                    {
                        this.GetPageNotificationsComponent().AddNotification(NotificationType.Error, "No files in snippet.");
                    }
                    else
                    {
                        this.activeCodeFile = this.CodeFiles.First().Value;

                        this.PackagesToRestore = snippetResponse.InstalledPackages?.ToList() ?? new List<Package>();

                        this.StaticAssets.Scripts = snippetResponse.StaticAssets?.Scripts ?? new HashSet<string>();
                        this.StaticAssets.Styles = snippetResponse.StaticAssets?.Styles ?? new HashSet<string>();
                        this.HandleStaticAssetsUpdated();

                        this.StateHasChanged();
                    }
                }
                catch (ArgumentException)
                {
                    this.GetPageNotificationsComponent().AddNotification(NotificationType.Error, "Invalid Snippet ID.");
                }
                catch (Exception)
                {
                    this.GetPageNotificationsComponent().AddNotification(
                        NotificationType.Error,
                        "Unable to get snippet content. Please try again later.");
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
            this.ShowLoader = true;
            this.LoaderText = "Processing";

            await Task.Delay(1); // Ensure rendering has time to be called

            if (this.PackagesToRestore.Any())
            {
                await this.PackageManagerComponent.RestorePackagesAsync();
            }

            CompileToAssemblyResult compilationResult = null;
            try
            {
                this.UpdateActiveCodeFileContent();

                this.CodeFiles.TryGetValue(CoreConstants.MainComponentFilePath, out var mainComponent);
                if (mainComponent == null)
                {
                    this.GetPageNotificationsComponent().AddNotification(
                        NotificationType.Error,
                        content: "Invalid set of code files for compilation. Please reload the app.");

                    return;
                }

                var codeFiles = new List<CodeFile>(this.CodeFiles.Count)
                {
                    // Add the necessary code prefix to main component
                    new() { Path = mainComponent.Path, Content = MainComponentCodePrefix + mainComponent.Content },
                };

                codeFiles.AddRange(this.CodeFiles.Values.Where(f => f.Path != CoreConstants.MainComponentFilePath));

                compilationResult = await this.CompilationService.CompileToAssemblyAsync(codeFiles, this.UpdateLoaderTextAsync);

                this.Diagnostics = compilationResult.Diagnostics.OrderByDescending(x => x.Severity).ThenBy(x => x.Code).ToList();
                this.AreDiagnosticsShown = true;
            }
            catch (Exception)
            {
                this.GetPageNotificationsComponent().AddNotification(NotificationType.Error, content: "Error while compiling the code.");
            }
            finally
            {
                this.ShowLoader = false;
            }

            if (compilationResult?.AssemblyBytes?.Length > 0)
            {
                // Make sure the DLL is updated before reloading the user page
                await this.JsRuntime.InvokeVoidAsync("App.CodeExecution.updateUserComponentsDll", compilationResult.AssemblyBytes);

                var userPagePath = this.InstalledPackagesCount > 0 || this.StaticAssetsCount > 0
                    ? $"{MainUserPagePath}#{this.SessionId}"
                    : MainUserPagePath;

                // TODO: Add error page in iframe
                this.JsRuntime.InvokeVoid("App.reloadIFrame", "user-page-window", userPagePath);
            }
        }

        private void ShowSaveSnippetPopup()
        {
            this.UpdateActiveCodeFileContent();

            this.SaveSnippetPopupVisible = true;
        }

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

            var newCodeFile = new CodeFile { Path = name };

            newCodeFile.Content = newCodeFile.Type == CodeFileType.CSharp
                ? string.Format(CoreConstants.DefaultCSharpFileContentFormat, nameWithoutExtension)
                : string.Format(CoreConstants.DefaultRazorFileContentFormat, nameWithoutExtension);

            this.CodeFiles.TryAdd(name, newCodeFile);

            // TODO: update method name when refactoring the code editor JS module
            this.JsRuntime.InvokeVoid(
                "App.Repl.setCodeEditorContainerHeight",
                newCodeFile.Type == CodeFileType.CSharp ? "csharp" : "razor");
        }

        private void HandleScaffoldStartupSettingClick()
        {
            this.UpdateActiveCodeFileContent();

            if (!this.CodeFiles.TryGetValue(CoreConstants.StartupClassFilePath, out var startupCodeFile))
            {
                startupCodeFile = new CodeFile
                {
                    Path = CoreConstants.StartupClassFilePath,
                    Content = CoreConstants.StartupClassDefaultFileContent,
                };

                this.CodeFiles.Add(CoreConstants.StartupClassFilePath, startupCodeFile);

                this.CodeFileNames = this.CodeFiles.Keys.ToList();
            }

            this.activeCodeFile = startupCodeFile;

            // TODO: update method name when refactoring the code editor JS module
            this.JsRuntime.InvokeVoid("App.Repl.setCodeEditorContainerHeight", "csharp");
        }

        private async Task HandleActivityToggleAsync(ActivityToggleEventArgs eventArgs)
        {
            switch (eventArgs?.Activity)
            {
                case nameof(PackageManager):
                    this.PackageManagerVisible = eventArgs.Visible;
                    this.StaticAssetManagerVisible = false;
                    break;

                case nameof(StaticAssetManager):
                    this.StaticAssetManagerVisible = eventArgs.Visible;
                    this.PackageManagerVisible = false;
                    break;

                default:
                    return;
            }

            this.StateHasChanged();
            await Task.Delay(1); // Ensure rendering has time to be called

            this.CodeEditorComponent.Resize();
        }

        private void HandleStaticAssetsUpdated() =>
            this.StaticAssetsCount = (this.StaticAssets.Scripts?.Count ?? 0) + (this.StaticAssets.Styles?.Count ?? 0);

        private void UpdateActiveCodeFileContent()
        {
            if (this.activeCodeFile == null)
            {
                this.GetPageNotificationsComponent().AddNotification(NotificationType.Error, "No active file to update.");
                return;
            }

            this.activeCodeFile.Content = this.CodeEditorComponent.GetCode();
        }

        private Task UpdateLoaderTextAsync(string loaderText)
        {
            this.LoaderText = loaderText;

            this.StateHasChanged();
            return Task.Delay(1); // Ensure rendering has time to be called
        }
    }
}
