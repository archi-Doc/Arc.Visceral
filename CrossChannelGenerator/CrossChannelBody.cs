// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Text;
using System.Threading;
using Arc.Visceral;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

#pragma warning disable RS2008
#pragma warning disable SA1310 // Field names should not contain underscore
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable SA1117 // Parameters should be on same line or separate lines

namespace CrossChannel.Generator;

public class CrossChannelBody : VisceralBody<CrossChannelObject>
{
    public const string Name = "CrossChannel";
    public const string GeneratorName = "CrossChannelGenerator";
    public const string InitializerName = "__InitializeCC__";

    public static readonly DiagnosticDescriptor Error_NotPartialParent = new DiagnosticDescriptor(
        id: "CCG001", title: "Partial class/struct", messageFormat: "Parent type '{0}' is not a partial class/struct",
        category: GeneratorName, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor Error_IRadioService = new DiagnosticDescriptor(
        id: "CCG002", title: "IRadioService", messageFormat: "Types with the RadioServiceInterface attribute must derive from IRadioService",
        category: GeneratorName, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor Error_MethodReturnType = new DiagnosticDescriptor(
        id: "CCG003", title: "Method return type", messageFormat: "The return type of the method must be void, Task, RadioResult<T>, Task<RadioResult<T>>",
        category: GeneratorName, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public CrossChannelBody(SourceProductionContext context)
        : base(context)
    {
    }

    internal List<CrossChannelObject> Objects = new();

    internal Dictionary<string, List<CrossChannelObject>> Namespaces = new();

    public void Prepare()
    {
        // Configure objects.
        var array = this.FullNameToObject.Values.ToArray();
        foreach (var x in array)
        {
            x.Configure();
        }

        this.FlushDiagnostic();
        if (this.Abort)
        {
            return;
        }

        foreach (var x in array)
        {
            x.ConfigureRelation();
        }

        // Check
        foreach (var x in array)
        {
            x.Check();
        }

        this.FlushDiagnostic();
        if (this.Abort)
        {
            return;
        }
    }

    public void Generate(IGeneratorInformation generator, CancellationToken cancellationToken)
    {
        var rootObject = this.GenerateMain(generator, cancellationToken);
        this.GenerateInitializer(generator, rootObject, cancellationToken);
    }

    public List<CrossChannelObject> GenerateMain(IGeneratorInformation generator, CancellationToken cancellationToken)
    {
        ScopingStringBuilder ssb = new();
        List<CrossChannelObject> rootObjects = new();

        // Namespace
        foreach (var x in this.Namespaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.GenerateHeader(ssb);
            ssb.AppendLine($"namespace {x.Key};");
            ssb.AppendLine();

            rootObjects.AddRange(x.Value); // For loader generation

            var firstFlag = true;
            foreach (var y in x.Value)
            {
                if (firstFlag)
                {
                    firstFlag = false;
                }
                else
                {
                    ssb.AppendLine();
                }

                y.Generate(ssb); // Primary objects
            }

            var result = ssb.Finalize();

            if (generator.GenerateToFile && generator.TargetFolder != null && Directory.Exists(generator.TargetFolder))
            {
                this.StringToFile(result, Path.Combine(generator.TargetFolder, $"gen.{Name}.{x.Key}.cs"));
            }
            else
            {
                this.Context?.AddSource($"gen.{Name}.{x.Key}", SourceText.From(result, Encoding.UTF8));
                this.Context2?.AddSource($"gen.{Name}.{x.Key}", SourceText.From(result, Encoding.UTF8));
            }
        }

        return rootObjects;
    }

    public void GenerateInitializer(IGeneratorInformation generator, List<CrossChannelObject> rootObjects, CancellationToken cancellationToken)
    {
        var ssb = new ScopingStringBuilder();
        this.GenerateHeader(ssb);

        using (var scopeFormatter = ssb.ScopeNamespace($"{Name}.Generated"))
        {
            using (var methods = ssb.ScopeBrace("internal static class Root"))
            {
                CrossChannelObject.GenerateInitializer(ssb, null, rootObjects);
            }
        }

        // Namespace
        var @namespace = Name;
        var assemblyId = string.Empty; // Assembly ID
        if (!string.IsNullOrEmpty(generator.CustomNamespace))
        {// Custom namespace.
            @namespace = generator.CustomNamespace;
        }
        else
        {// Other (Apps)
            // assemblyId = "_" + generator.AssemblyId.ToString("x");
            if (!string.IsNullOrEmpty(generator.AssemblyName))
            {
                assemblyId = VisceralHelper.AssemblyNameToIdentifier("_" + generator.AssemblyName);
            }
        }

        ssb.AppendLine();
        using (var scopeCrossLink = ssb.ScopeNamespace(@namespace!))
        {
            using (var scopeClass = ssb.ScopeBrace($"public static class {Name}Module" + assemblyId))
            {
                ssb.AppendLine("private static bool Initialized;");
                ssb.AppendLine();
                ssb.AppendLine("[ModuleInitializer]");

                using (var scopeMethod = ssb.ScopeBrace("public static void Initialize()"))
                {
                    ssb.AppendLine("if (Initialized) return;");
                    ssb.AppendLine("Initialized = true;");

                    ssb.AppendLine($"{Name}.Generated.Root.{CrossChannelBody.InitializerName}();");
                }
            }
        }

        var result = ssb.Finalize();

        if (generator.GenerateToFile && generator.TargetFolder != null && Directory.Exists(generator.TargetFolder))
        {
            this.StringToFile(result, Path.Combine(generator.TargetFolder, $"gen.{Name}.cs"));
        }
        else
        {
            this.Context?.AddSource($"gen.{Name}", SourceText.From(result, Encoding.UTF8));
            this.Context2?.AddSource($"gen.{Name}", SourceText.From(result, Encoding.UTF8));
        }
    }

    private void GenerateHeader(ScopingStringBuilder ssb)
    {
        ssb.AddHeader("// <auto-generated/>");
        ssb.AddUsing("System");
        ssb.AddUsing("System.Collections.Generic");
        ssb.AddUsing("System.Diagnostics.CodeAnalysis");
        ssb.AddUsing("System.Linq");
        ssb.AddUsing("System.Runtime.CompilerServices");
        ssb.AddUsing("System.Threading.Tasks");
        ssb.AddUsing("CrossChannel");

        ssb.AppendLine("#nullable enable", false);
        ssb.AppendLine("#pragma warning disable CS1591", false);
        ssb.AppendLine("#pragma warning disable CS1998", false);
        ssb.AppendLine();
    }
}
