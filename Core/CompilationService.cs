namespace BlazorRepl.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Runtime;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components.Routing;
    using Microsoft.AspNetCore.Razor.Language;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Razor;
    using Microsoft.JSInterop;

    public class CompilationService
    {
        public const string DefaultRootNamespace = "BlazorRepl.UserComponents";

        private const string WorkingDirectory = "/BlazorRepl/";
        private const string DefaultImports = @"@using System.ComponentModel.DataAnnotations
@using System.Linq
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.JSInterop";

        // Creating the initial compilation + reading references is on the order of 250ms without caching
        // so making sure it doesn't happen for each run.
        private static CSharpCompilation baseCompilation;
        private static CSharpParseOptions cSharpParseOptions;

        private readonly RazorProjectFileSystem fileSystem = new VirtualRazorProjectFileSystem();
        private readonly RazorConfiguration configuration = RazorConfiguration.Create(
            RazorLanguageVersion.Latest,
            configurationName: "Blazor",
            extensions: Array.Empty<RazorExtension>());

        public static async Task InitAsync(HttpClient httpClient)
        {
            var basicReferenceAssemblyRoots = new[]
            {
                typeof(Console).Assembly, // System.Console
                typeof(Uri).Assembly, // System.Private.Uri
                typeof(AssemblyTargetedPatchBandAttribute).Assembly, // System.Private.CoreLib
                typeof(NavLink).Assembly, // Microsoft.AspNetCore.Components.Web
                typeof(IQueryable).Assembly, // System.Linq.Expressions
                typeof(HttpClientJsonExtensions).Assembly, // System.Net.Http.Json
                typeof(HttpClient).Assembly, // System.Net.Http
                typeof(IJSRuntime).Assembly, // Microsoft.JSInterop
                typeof(RequiredAttribute).Assembly, // System.ComponentModel.Annotations
            };

            var assemblyNames = basicReferenceAssemblyRoots
                .SelectMany(assembly => assembly.GetReferencedAssemblies().Concat(new[] { assembly.GetName() }))
                .Select(x => x.Name)
                .Distinct()
                .ToList();

            var assemblyStreams = await GetStreamFromHttpAsync(httpClient, assemblyNames);

            var allReferenceAssemblies = assemblyStreams.ToDictionary(a => a.Key, a => MetadataReference.CreateFromStream(a.Value));

            var basicReferenceAssemblies = allReferenceAssemblies
                .Where(a => basicReferenceAssemblyRoots
                    .Select(x => x.GetName().Name)
                    .Union(basicReferenceAssemblyRoots.SelectMany(y => y.GetReferencedAssemblies().Select(z => z.Name)))
                    .Any(n => n == a.Key))
                .Select(a => a.Value)
                .ToList();

            baseCompilation = CSharpCompilation.Create(
                "BlazorRepl.UserComponents",
                Array.Empty<SyntaxTree>(),
                basicReferenceAssemblies,
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    concurrentBuild: false,
                    //// Warnings CS1701 and CS1702 are disabled when compiling in VS too
                    specificDiagnosticOptions: new[]
                    {
                        new KeyValuePair<string, ReportDiagnostic>("CS1701", ReportDiagnostic.Suppress),
                        new KeyValuePair<string, ReportDiagnostic>("CS1702", ReportDiagnostic.Suppress),
                    }));

            cSharpParseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        }

        public async Task<CompileToAssemblyResult> CompileToAssemblyAsync(
            ICollection<CodeFile> codeFiles,
            string preset,
            Func<string, Task> updateStatusFunc) // TODO: try convert to event
        {
            if (codeFiles == null)
            {
                throw new ArgumentNullException(nameof(codeFiles));
            }

            var cSharpResults = await this.CompileToCSharpAsync(codeFiles, updateStatusFunc);

            await (updateStatusFunc?.Invoke("Compiling Assembly") ?? Task.CompletedTask);
            var result = CompileToAssembly(cSharpResults);

            return result;
        }

        private static async Task<IDictionary<string, Stream>> GetStreamFromHttpAsync(HttpClient httpClient, IEnumerable<string> assemblyNames)
        {
            var streams = new ConcurrentDictionary<string, Stream>();

            await Task.WhenAll(
                assemblyNames.Select(async assemblyName =>
                {
                    var result = await httpClient.GetAsync($"/_framework/{assemblyName}.dll");

                    result.EnsureSuccessStatusCode();

                    streams.TryAdd(assemblyName, await result.Content.ReadAsStreamAsync());
                }));

            return streams;
        }

        private static CompileToAssemblyResult CompileToAssembly(IReadOnlyCollection<CompileToCSharpResult> cSharpResults)
        {
            if (cSharpResults.Any(r => r.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)))
            {
                return new CompileToAssemblyResult { Diagnostics = cSharpResults.SelectMany(r => r.Diagnostics).ToList() };
            }

            var syntaxTrees = new List<SyntaxTree>(cSharpResults.Count);
            foreach (var cSharpResult in cSharpResults)
            {
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(cSharpResult.Code, cSharpParseOptions));
            }

            var finalCompilation = baseCompilation.AddSyntaxTrees(syntaxTrees);

            var compilationDiagnostics = finalCompilation.GetDiagnostics().Where(d => d.Severity > DiagnosticSeverity.Info);

            var result = new CompileToAssemblyResult
            {
                Compilation = finalCompilation,
                Diagnostics = compilationDiagnostics
                    .Select(CompilationDiagnostic.FromCSharpDiagnostic)
                    .Concat(cSharpResults.SelectMany(r => r.Diagnostics))
                    .ToList(),
            };

            if (result.Diagnostics.All(x => x.Severity != DiagnosticSeverity.Error))
            {
                using var peStream = new MemoryStream();
                finalCompilation.Emit(peStream);

                result.AssemblyBytes = peStream.ToArray();
            }

            return result;
        }

        private static RazorProjectItem CreateRazorProjectItem(string fileName, string fileContent)
        {
            var fullPath = WorkingDirectory + fileName;

            // File paths in Razor are always of the form '/a/b/c.razor'
            var filePath = fileName;
            if (!filePath.StartsWith('/'))
            {
                filePath = '/' + filePath;
            }

            fileContent = fileContent.Replace("\r", string.Empty);

            return new VirtualProjectItem(
                WorkingDirectory,
                filePath,
                fullPath,
                fileName,
                FileKinds.Component,
                Encoding.UTF8.GetBytes(fileContent.TrimStart()));
        }

        private async Task<IReadOnlyCollection<CompileToCSharpResult>> CompileToCSharpAsync(
            ICollection<CodeFile> codeFiles,
            Func<string, Task> updateStatusFunc)
        {
            // The first phase won't include any metadata references for component discovery. This mirrors what the build does.
            var projectEngine = this.CreateRazorProjectEngine(Array.Empty<MetadataReference>());

            // Result of generating declarations
            var declarations = new CompileToCSharpResult[codeFiles.Count];
            var index = 0;
            foreach (var codeFile in codeFiles)
            {
                // TODO: abstract
                if (Path.GetExtension(codeFile.Path) == ".razor")
                {
                    var projectItem = CreateRazorProjectItem(codeFile.Path, codeFile.Content);

                    var codeDocument = projectEngine.ProcessDeclarationOnly(projectItem);
                    var cSharpDocument = codeDocument.GetCSharpDocument();

                    Console.WriteLine(JsonSerializer.Serialize(cSharpDocument.GeneratedCode));
                    declarations[index] = new CompileToCSharpResult
                    {
                        ProjectItem = projectItem,
                        Code = cSharpDocument.GeneratedCode,
                        Diagnostics = cSharpDocument.Diagnostics.Select(CompilationDiagnostic.FromRazorDiagnostic).ToList(),
                    };
                }
                else
                {
                    declarations[index] = new CompileToCSharpResult
                    {
                        Code = codeFile.Content,
                        Diagnostics = Enumerable.Empty<CompilationDiagnostic>(), // Will be actually evaluated later
                    };
                }

                index++;
            }

            // Result of doing 'temp' compilation
            var tempAssembly = CompileToAssembly(declarations);
            if (tempAssembly.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                return new[] { new CompileToCSharpResult { Diagnostics = tempAssembly.Diagnostics } };
            }

            // Add the 'temp' compilation as a metadata reference
            var references = new List<MetadataReference>(baseCompilation.References) { tempAssembly.Compilation.ToMetadataReference() };
            projectEngine = this.CreateRazorProjectEngine(references);

            await (updateStatusFunc?.Invoke("Preparing Project") ?? Task.CompletedTask);

            var results = new CompileToCSharpResult[declarations.Length];
            index = 0;
            foreach (var declaration in declarations)
            {
                // Only Razor file declarations have project item
                if (declaration.ProjectItem != null)
                {
                    var codeDocument = projectEngine.Process(declaration.ProjectItem);
                    var cSharpDocument = codeDocument.GetCSharpDocument();

                    Console.WriteLine(JsonSerializer.Serialize(cSharpDocument.GeneratedCode));
                    results[index] = new CompileToCSharpResult
                    {
                        ProjectItem = declaration.ProjectItem,
                        Code = cSharpDocument.GeneratedCode,
                        Diagnostics = cSharpDocument.Diagnostics.Select(CompilationDiagnostic.FromRazorDiagnostic).ToList(),
                    };
                }
                else
                {
                    results[index] = declaration;
                }

                index++;
            }

            return results;
        }

        private RazorProjectEngine CreateRazorProjectEngine(IReadOnlyList<MetadataReference> references) =>
            RazorProjectEngine.Create(this.configuration, this.fileSystem, b =>
            {
                b.SetRootNamespace(DefaultRootNamespace);
                b.AddDefaultImports(DefaultImports);

                // Features that use Roslyn are mandatory for components
                CompilerFeatures.Register(b);

                b.Features.Add(new CompilationTagHelperFeature());
                b.Features.Add(new DefaultMetadataReferenceFeature { References = references });
            });
    }
}
