namespace BlazorRepl.Client.Models
{
    using System.Collections.Generic;

    public class StaticAssets
    {
        public ISet<string> Scripts { get; set; } = new HashSet<string>();

        public ISet<string> Styles { get; set; } = new HashSet<string>();
    }
}
