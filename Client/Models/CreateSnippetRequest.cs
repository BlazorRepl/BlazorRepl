namespace BlazorRepl.Client.Models
{
    using System;
    using System.Collections.Generic;
    using BlazorRepl.Core;
    using BlazorRepl.Core.PackageInstallation;

    public class CreateSnippetRequest
    {
        public IEnumerable<CodeFile> Files { get; set; } = Array.Empty<CodeFile>();

        public IEnumerable<Package> InstalledPackages { get; set; } = Array.Empty<Package>();
    }
}
