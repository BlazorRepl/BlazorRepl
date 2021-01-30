namespace BlazorRepl.Client.Components
{
    using System;
    using System.Threading.Tasks;
    using BlazorRepl.Core;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class CodeEditor : IDisposable
    {
        private const string EditorId = "user-code-editor";

        private bool hasCodeChanged;

        [Inject]
        public IJSInProcessRuntime JsRuntime { get; set; }

        [Parameter]
        public string Code { get; set; }

        [Parameter]
        public CodeFileType CodeFileType { get; set; }

        public override Task SetParametersAsync(ParameterView parameters)
        {
            if (parameters.TryGetValue<string>(nameof(this.Code), out var parameterValue))
            {
                this.hasCodeChanged = this.Code != parameterValue;
            }

            return base.SetParametersAsync(parameters);
        }

        public void Dispose() => this.JsRuntime.InvokeVoid("App.CodeEditor.dispose");

        internal void Focus() => this.JsRuntime.InvokeVoid("App.CodeEditor.focus");

        internal string GetCode() => this.JsRuntime.Invoke<string>("App.CodeEditor.getValue");

        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                this.JsRuntime.InvokeVoid(
                   "App.CodeEditor.init",
                   EditorId,
                   this.Code ?? CoreConstants.MainComponentDefaultFileContent);
            }
            else if (this.hasCodeChanged)
            {
                var language = this.CodeFileType == CodeFileType.CSharp ? "csharp" : "razor";
                this.JsRuntime.InvokeVoid("App.CodeEditor.setValue", this.Code, language);
            }

            base.OnAfterRender(firstRender);
        }
    }
}
