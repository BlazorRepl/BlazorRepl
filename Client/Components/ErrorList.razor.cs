namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using BlazorRepl.Core;
    using Microsoft.AspNetCore.Components;
    using Microsoft.CodeAnalysis;

    public partial class ErrorList
    {
        [Parameter]
        public IReadOnlyCollection<CompilationDiagnostic> Diagnostics { get; set; } = Array.Empty<CompilationDiagnostic>();

        [Parameter]
        public int UserComponentCodeStartLine { get; set; }

        [Parameter]
        public bool Show { get; set; }

        [Parameter]
        public EventCallback<bool> ShowChanged { get; set; }

        private int ErrorsCount { get; set; }

        private int WarningsCount { get; set; }

        private bool ShowIcon => this.Diagnostics.Any();

        protected override void OnInitialized()
        {
            this.ErrorsCount = this.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
            this.WarningsCount = this.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
        }

        private Task ToggleDiagnosticsAsync()
        {
            this.Show = !this.Show;
            return this.ShowChanged.InvokeAsync(this.Show);
        }
    }
}
