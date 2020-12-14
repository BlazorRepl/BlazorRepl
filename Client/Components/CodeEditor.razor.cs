namespace BlazorRepl.Client.Components
{
    using System;
    using System.Threading.Tasks;
    using BlazorRepl.Core;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class CodeEditor : IAsyncDisposable
    {
        private const string EditorId = "user-code-editor";

        private bool hasCodeChanged;

        [Inject]
        public IJSRuntime JsRuntime { get; set; }

        [Parameter]
        public string Code { get; set; }

        public ValueTask FocusAsync() => this.JsRuntime.InvokeVoidAsync("App.CodeEditor.focus");

        public override Task SetParametersAsync(ParameterView parameters)
        {
            if (parameters.TryGetValue<string>(nameof(this.Code), out var parameterValue))
            {
                this.hasCodeChanged = this.Code != parameterValue;
            }

            return base.SetParametersAsync(parameters);
        }

        public ValueTask DisposeAsync() => this.JsRuntime.InvokeVoidAsync("App.CodeEditor.dispose");

        internal ValueTask<string> GetCodeAsync() => this.JsRuntime.InvokeAsync<string>("App.CodeEditor.getValue");

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await this.JsRuntime.InvokeVoidAsync(
                   "App.CodeEditor.init",
                   EditorId,
                   this.Code ?? CoreConstants.MainComponentDefaultFileContent);
            }
            else if (this.hasCodeChanged)
            {
                await this.JsRuntime.InvokeVoidAsync("App.CodeEditor.setValue", this.Code);
            }

            await base.OnAfterRenderAsync(firstRender);
        }
    }
}
