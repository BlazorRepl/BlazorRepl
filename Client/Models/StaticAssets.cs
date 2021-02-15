namespace BlazorRepl.Client.Models
{
    using System.Collections.Generic;

    public class StaticAssets
    {
        public ISet<string> Scripts { get; set; }

        public ISet<string> Styles { get; set; }
    }
}
