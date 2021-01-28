namespace BlazorRepl.Core
{
    using System;
    using System.Text.Json.Serialization;

    public class CodeFile
    {
        public string Path { get; set; }

        public string Content { get; set; }

        // TODO: Const
        [JsonIgnore]
        public bool IsRazorFile => string.Equals(System.IO.Path.GetExtension(this.Path), ".razor", StringComparison.OrdinalIgnoreCase);
    }
}
