// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Build.ThreadSafeTaskAnalyzer;

/// <summary>
/// Roslyn analyzer that checks for thread-safety violations in MSBuild tasks
/// marked with <c>MSBuildMultiThreadableTaskAttribute</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ThreadSafeTaskAnalyzer : DiagnosticAnalyzer
{
    private const string MSBuildMultiThreadableTaskAttributeName = "MSBuildMultiThreadableTaskAttribute";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        DiagnosticDescriptors.EnvironmentModification,
        DiagnosticDescriptors.CurrentDirectoryUsage,
        DiagnosticDescriptors.ProcessTermination,
        DiagnosticDescriptors.PathGetFullPath,
        DiagnosticDescriptors.RelativePathWarning,
        DiagnosticDescriptors.ProcessStartUsage,
        DiagnosticDescriptors.CultureModification,
        DiagnosticDescriptors.ThreadPoolModification,
        DiagnosticDescriptors.AssemblyLoadingWarning,
        DiagnosticDescriptors.ProcessKill,
        DiagnosticDescriptors.StaticFieldWarning);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Register for class declarations to find tasks with MSBuildMultiThreadableTaskAttribute
        context.RegisterCompilationStartAction(compilationContext =>
        {
            // Register symbol action to check class declarations
            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol || typeSymbol.TypeKind != TypeKind.Class)
        {
            return;
        }

        // Check if this type has the MSBuildMultiThreadableTaskAttribute
        bool hasMultiThreadableAttribute = false;
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass?.Name == MSBuildMultiThreadableTaskAttributeName)
            {
                hasMultiThreadableAttribute = true;
                break;
            }
        }

        if (!hasMultiThreadableAttribute)
        {
            return;
        }

        // Now analyze the class for thread-safety violations
        foreach (var syntaxRef in typeSymbol.DeclaringSyntaxReferences)
        {
            var classDeclaration = syntaxRef.GetSyntax(context.CancellationToken) as ClassDeclarationSyntax;
            if (classDeclaration == null)
            {
                continue;
            }

            var semanticModel = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree);

            // Check for static fields
            AnalyzeStaticFields(context, typeSymbol);

            // Analyze method bodies for banned API usage
            foreach (var member in classDeclaration.Members)
            {
                AnalyzeMember(context, semanticModel, member);
            }
        }
    }

    private static void AnalyzeStaticFields(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol)
    {
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is IFieldSymbol field && field.IsStatic && !field.IsConst && !field.IsReadOnly)
            {
                foreach (var location in field.Locations)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.StaticFieldWarning,
                        location,
                        field.Name));
                }
            }
        }
    }

    private static void AnalyzeMember(SymbolAnalysisContext context, SemanticModel semanticModel, MemberDeclarationSyntax member)
    {
        SyntaxNode? body = member switch
        {
            MethodDeclarationSyntax method => (SyntaxNode?)method.Body ?? method.ExpressionBody,
            PropertyDeclarationSyntax property => (SyntaxNode?)property.ExpressionBody ?? property.AccessorList,
            ConstructorDeclarationSyntax ctor => (SyntaxNode?)ctor.Body ?? ctor.ExpressionBody,
            _ => null
        };

        if (body == null)
        {
            return;
        }

        var invocations = body.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            AnalyzeInvocation(context, semanticModel, invocation);
        }

        var memberAccesses = body.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
        foreach (var memberAccess in memberAccesses)
        {
            AnalyzeMemberAccess(context, semanticModel, memberAccess);
        }

        var objectCreations = body.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        foreach (var objectCreation in objectCreations)
        {
            AnalyzeObjectCreation(context, semanticModel, objectCreation);
        }

        var assignments = body.DescendantNodes().OfType<AssignmentExpressionSyntax>();
        foreach (var assignment in assignments)
        {
            AnalyzeAssignment(context, semanticModel, assignment);
        }
    }

    private static void AnalyzeInvocation(SymbolAnalysisContext context, SemanticModel semanticModel, InvocationExpressionSyntax invocation)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        var containingType = methodSymbol.ContainingType?.ToDisplayString();
        var methodName = methodSymbol.Name;

        // Check Environment methods
        if (containingType == "System.Environment")
        {
            switch (methodName)
            {
                case "Exit":
                case "FailFast":
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ProcessTermination,
                        invocation.GetLocation(),
                        $"Environment.{methodName}"));
                    break;
                case "SetEnvironmentVariable":
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.EnvironmentModification,
                        invocation.GetLocation(),
                        $"Environment.{methodName}"));
                    break;
                case "GetEnvironmentVariable":
                case "GetEnvironmentVariables":
                case "ExpandEnvironmentVariables":
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.EnvironmentModification,
                        invocation.GetLocation(),
                        $"Environment.{methodName}"));
                    break;
            }
        }

        // Check Path.GetFullPath
        if (containingType == "System.IO.Path" && methodName == "GetFullPath")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.PathGetFullPath,
                invocation.GetLocation(),
                "Path.GetFullPath"));
        }

        // Check Process.Start
        if (containingType == "System.Diagnostics.Process" && methodName == "Start")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ProcessStartUsage,
                invocation.GetLocation(),
                "Process.Start"));
        }

        // Check Process.Kill on current process
        if (methodName == "Kill" && methodSymbol.ContainingType?.ToDisplayString() == "System.Diagnostics.Process")
        {
            // Check if it's being called on GetCurrentProcess()
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is InvocationExpressionSyntax innerInvocation)
            {
                var innerSymbol = semanticModel.GetSymbolInfo(innerInvocation, context.CancellationToken).Symbol as IMethodSymbol;
                if (innerSymbol?.Name == "GetCurrentProcess" && innerSymbol.ContainingType?.ToDisplayString() == "System.Diagnostics.Process")
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ProcessKill,
                        invocation.GetLocation(),
                        "Process.GetCurrentProcess().Kill()"));
                }
            }
        }

        // Check ThreadPool modifications
        if (containingType == "System.Threading.ThreadPool")
        {
            if (methodName is "SetMinThreads" or "SetMaxThreads")
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ThreadPoolModification,
                    invocation.GetLocation(),
                    $"ThreadPool.{methodName}"));
            }
        }

        // Check Assembly loading
        if (containingType == "System.Reflection.Assembly")
        {
            if (methodName is "LoadFrom" or "LoadFile" or "Load" or "LoadWithPartialName")
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AssemblyLoadingWarning,
                    invocation.GetLocation(),
                    $"Assembly.{methodName}"));
            }
        }

        // Check Activator methods
        if (containingType == "System.Activator")
        {
            if (methodName is "CreateInstanceFrom" or "CreateInstance")
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AssemblyLoadingWarning,
                    invocation.GetLocation(),
                    $"Activator.{methodName}"));
            }
        }

        // Check AppDomain methods
        if (containingType == "System.AppDomain")
        {
            if (methodName is "Load" or "CreateInstanceFrom" or "CreateInstance")
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AssemblyLoadingWarning,
                    invocation.GetLocation(),
                    $"AppDomain.{methodName}"));
            }
        }

        // Check File and Directory methods (warning level)
        if (containingType is "System.IO.File" or "System.IO.Directory")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.RelativePathWarning,
                invocation.GetLocation(),
                $"{methodSymbol.ContainingType?.Name ?? "Unknown"}.{methodName}"));
        }
    }

    private static void AnalyzeMemberAccess(SymbolAnalysisContext context, SemanticModel semanticModel, MemberAccessExpressionSyntax memberAccess)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
        if (symbolInfo.Symbol is not IPropertySymbol propertySymbol)
        {
            return;
        }

        var containingType = propertySymbol.ContainingType?.ToDisplayString();
        var propertyName = propertySymbol.Name;

        // Check Environment.CurrentDirectory
        if (containingType == "System.Environment" && propertyName == "CurrentDirectory")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.CurrentDirectoryUsage,
                memberAccess.GetLocation(),
                "Environment.CurrentDirectory"));
        }

        // Check CultureInfo.DefaultThreadCurrentCulture/DefaultThreadCurrentUICulture
        if (containingType == "System.Globalization.CultureInfo")
        {
            if (propertyName is "DefaultThreadCurrentCulture" or "DefaultThreadCurrentUICulture")
            {
                // Only report for setters (assignments are handled separately)
                // This catches read access which is less severe but we'll report it anyway
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.CultureModification,
                    memberAccess.GetLocation(),
                    $"CultureInfo.{propertyName}"));
            }
        }
    }

    private static void AnalyzeObjectCreation(SymbolAnalysisContext context, SemanticModel semanticModel, ObjectCreationExpressionSyntax objectCreation)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(objectCreation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol constructorSymbol)
        {
            return;
        }

        var typeName = constructorSymbol.ContainingType?.ToDisplayString();

        // Check ProcessStartInfo creation
        if (typeName == "System.Diagnostics.ProcessStartInfo")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ProcessStartUsage,
                objectCreation.GetLocation(),
                "new ProcessStartInfo()"));
        }

        // Check FileInfo/DirectoryInfo creation
        if (typeName is "System.IO.FileInfo" or "System.IO.DirectoryInfo")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.RelativePathWarning,
                objectCreation.GetLocation(),
                $"new {constructorSymbol.ContainingType?.Name ?? "Unknown"}()"));
        }

        // Check FileStream creation
        if (typeName == "System.IO.FileStream")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.RelativePathWarning,
                objectCreation.GetLocation(),
                "new FileStream()"));
        }

        // Check StreamReader creation
        if (typeName == "System.IO.StreamReader")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.RelativePathWarning,
                objectCreation.GetLocation(),
                "new StreamReader()"));
        }
    }

    private static void AnalyzeAssignment(SymbolAnalysisContext context, SemanticModel semanticModel, AssignmentExpressionSyntax assignment)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken);
        if (symbolInfo.Symbol is not IPropertySymbol propertySymbol)
        {
            return;
        }

        var containingType = propertySymbol.ContainingType?.ToDisplayString();
        var propertyName = propertySymbol.Name;

        // Check Environment.CurrentDirectory setter
        if (containingType == "System.Environment" && propertyName == "CurrentDirectory")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.CurrentDirectoryUsage,
                assignment.GetLocation(),
                "Environment.CurrentDirectory (setter)"));
        }

        // Check CultureInfo.DefaultThreadCurrentCulture/DefaultThreadCurrentUICulture setters
        if (containingType == "System.Globalization.CultureInfo")
        {
            if (propertyName is "DefaultThreadCurrentCulture" or "DefaultThreadCurrentUICulture")
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.CultureModification,
                    assignment.GetLocation(),
                    $"CultureInfo.{propertyName} (setter)"));
            }
        }
    }
}
