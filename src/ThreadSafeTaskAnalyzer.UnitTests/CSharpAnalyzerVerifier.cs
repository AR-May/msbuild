// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Build.ThreadSafeTaskAnalyzer.UnitTests;

/// <summary>
/// Result for diagnostic verification.
/// </summary>
public record DiagnosticResult(string Id, int Line, int Column);

/// <summary>
/// Helper class for verifying C# analyzers in tests using direct Roslyn APIs.
/// This avoids the NuGet dependency issues with Microsoft.CodeAnalysis.Testing in this repo.
/// </summary>
/// <typeparam name="TAnalyzer">The type of analyzer to verify.</typeparam>
public static class CSharpAnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    private static readonly MetadataReference[] s_references = GetRuntimeReferences();

    /// <summary>
    /// Creates a <see cref="DiagnosticResult"/> for the expected diagnostic.
    /// </summary>
    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
        => new(descriptor.Id, 0, 0);

    /// <summary>
    /// Verifies the analyzer produces the expected diagnostics for the given source.
    /// </summary>
    public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var analyzer = new TAnalyzer();
        var diagnostics = await GetDiagnosticsAsync(source, analyzer);

        // Verify expected diagnostics
        var actualIds = diagnostics.Select(d => d.Id).OrderBy(x => x).ToList();
        var expectedIds = expected.Select(e => e.Id).OrderBy(x => x).ToList();

        // Build assertion message
        if (!actualIds.SequenceEqual(expectedIds))
        {
            var actualDetails = string.Join("\n", diagnostics.Select(d =>
            {
                var location = d.Location.GetLineSpan();
                return $"  {d.Id} at ({location.StartLinePosition.Line + 1},{location.StartLinePosition.Character + 1}): {d.GetMessage()}";
            }));
            var expectedDetails = string.Join(", ", expectedIds);

            throw new Xunit.Sdk.XunitException(
                $"Diagnostic mismatch.\nExpected: [{expectedDetails}]\nActual diagnostics:\n{(string.IsNullOrEmpty(actualDetails) ? "  (none)" : actualDetails)}");
        }
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source, DiagnosticAnalyzer analyzer)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestCompilation",
            [syntaxTree],
            s_references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Check for compiler errors (for debugging)
        var compilerErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (compilerErrors.Count > 0)
        {
            var errorMessages = string.Join("\n", compilerErrors.Take(5).Select(e => $"  {e.Id}: {e.GetMessage()}"));
            throw new Xunit.Sdk.XunitException($"Compilation has errors:\n{errorMessages}");
        }

        var compilationWithAnalyzers = compilation.WithAnalyzers([analyzer]);
        var allDiagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        // Filter to only analyzer diagnostics (not compiler diagnostics)
        return allDiagnostics;
    }

    private static MetadataReference[] GetRuntimeReferences()
    {
        // Get essential runtime assemblies for compilation
        var assemblies = new[]
        {
            typeof(object).Assembly,                          // System.Private.CoreLib
            typeof(System.Diagnostics.Process).Assembly,      // System.Diagnostics.Process
            typeof(System.IO.File).Assembly,                  // System.IO.FileSystem
            typeof(System.Threading.ThreadPool).Assembly,     // System.Threading.ThreadPool
            typeof(System.Reflection.Assembly).Assembly,      // System.Reflection
            typeof(System.Globalization.CultureInfo).Assembly, // System.Globalization
            typeof(System.Linq.Enumerable).Assembly,          // System.Linq
            typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly, // System.Runtime
        };

        // Get additional framework assemblies needed for compilation
        var coreLibPath = typeof(object).Assembly.Location;
        var runtimeDir = System.IO.Path.GetDirectoryName(coreLibPath)!;

        var additionalRefs = new List<MetadataReference>();

        // Add all assemblies from the list
        foreach (var assembly in assemblies)
        {
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                additionalRefs.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        // Add System.Runtime.dll if it exists (needed for attribute definitions)
        var systemRuntimePath = System.IO.Path.Combine(runtimeDir, "System.Runtime.dll");
        if (System.IO.File.Exists(systemRuntimePath))
        {
            additionalRefs.Add(MetadataReference.CreateFromFile(systemRuntimePath));
        }

        // Add netstandard.dll if available
        var netstandardPath = System.IO.Path.Combine(runtimeDir, "netstandard.dll");
        if (System.IO.File.Exists(netstandardPath))
        {
            additionalRefs.Add(MetadataReference.CreateFromFile(netstandardPath));
        }

        return additionalRefs.Distinct().ToArray();
    }
}
