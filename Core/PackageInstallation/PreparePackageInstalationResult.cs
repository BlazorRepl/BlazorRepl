namespace BlazorRepl.Core.PackageInstallation
{
    using System.Collections.Generic;

    public class PreparePackageInstallationResult
    {
        public IEnumerable<PackageLicenseInfo> PackagesLicenseInfo { get; set; }
    }
}
