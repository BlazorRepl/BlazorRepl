namespace BlazorRepl.Client.Models
{
    using System.Collections.Generic;
    using System.Linq;
    using BlazorRepl.Core;
    using BlazorRepl.Core.PackageInstallation;

    public class SnippetResponse
    {
        public IEnumerable<CodeFile> Files { get; set; } = Enumerable.Empty<CodeFile>();

        public IEnumerable<Package> InstalledPackages { get; set; } = Enumerable.Empty<Package>();

        public StaticAssets StaticAssets { get; set; }
    }
}
