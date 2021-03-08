namespace BlazorRepl.Client.Models
{
    public class StaticAsset
    {
        public string Url { get; set; }

        public bool Enabled { get; set; }

        public StaticAssetSource Source { get; set; }
    }
}
