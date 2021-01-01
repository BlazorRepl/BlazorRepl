namespace BlazorRepl.Client.Shared
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class EmptyLayout
    {
        [Inject]
        public IJSUnmarshalledRuntime JsRuntime { get; set; }

        [Inject]
        public NavigationManager NavigationManager { get; set; }

        private bool RenderBody { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var sessionId = new Uri(this.NavigationManager.Uri).Fragment.Trim('#');
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                // TODO: Extract to a service
                this.JsRuntime.InvokeUnmarshalled<string, object>("App.loadNuGetPackageFiles", sessionId);
                Console.WriteLine("C#");

                //var sw = new Stopwatch();

                //foreach (var dll in dlls)
                //{
                //    sw.Restart();
                //    var dllBytes = Convert.FromBase64String(dll);
                //    Console.WriteLine($"string to byte[] DLL - {sw.Elapsed}");

                //    sw.Restart();
                //    AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(dllBytes));
                //    Console.WriteLine($"loading DLL - {sw.Elapsed}");
                //}
            }

            this.RenderBody = true;

            await base.OnInitializedAsync();
        }
    }
}