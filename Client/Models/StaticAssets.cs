namespace BlazorRepl.Client.Models
{
    using System.Collections.Generic;

    public class StaticAssets
    {
        public IDictionary<string, bool> Scripts { get; set; } = new Dictionary<string, bool>();

        public IDictionary<string, bool> Styles { get; set; } = new Dictionary<string, bool>();
    }
}
