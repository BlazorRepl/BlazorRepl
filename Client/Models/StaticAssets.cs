namespace BlazorRepl.Client.Models
{
    using System.Collections.Generic;

    public class StaticAssets
    {
        public IList<StaticAsset> Scripts { get; set; } = new List<StaticAsset>();

        public IList<StaticAsset> Styles { get; set; } = new List<StaticAsset>();
    }
}
