// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Collections.Immutable;
using Arc.Visceral;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#pragma warning disable RS1036

namespace CrossChannel.Generator;

[Generator]
public class CrossChannelGeneratorV2 : IIncrementalGenerator, IGeneratorInformation
{
    public bool AttachDebugger { get; private set; }

    public bool GenerateToFile { get; private set; }

    public string? CustomNamespace { get; private set; }

    public string? AssemblyName { get; private set; }

    public int AssemblyId { get; private set; }

    public OutputKind OutputKind { get; private set; }

    public string? TargetFolder { get; private set; }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.CompilationProvider.Combine(
            context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) =>
                {// Interface declaration with one or more attributes.
                    return node is InterfaceDeclarationSyntax syntax && syntax.AttributeLists.Count > 0;
                },
                static (context, _) =>
                {
                    if (context.Node is InterfaceDeclarationSyntax syntax)
                    {
                        foreach (var attributeList in syntax.AttributeLists)
                        {
                            foreach (var attribute in attributeList.Attributes)
                            {
                                var name = attribute.Name.ToString();
                                if (name.EndsWith(CrossChannelGeneratorOptionAttributeMock.StandardName) ||
                                    name.EndsWith(CrossChannelGeneratorOptionAttributeMock.SimpleName))
                                {// [CrossChannelGeneratorOptionAttribute]
                                    return syntax;
                                }
                                else if (name.EndsWith(RadioServiceInterfaceAttributeMock.StandardName) ||
                                    name.EndsWith(RadioServiceInterfaceAttributeMock.SimpleName))
                                {// [RadioServiceInterfaceAttribute]
                                    return syntax;
                                }
                            }
                        }

                        /*if (syntax.BaseList is not null)
                        {// IRadioService (check later)
                            foreach (var baseType in syntax.BaseList.Types)
                            {
                                var name = baseType.ToString();
                                if (name.EndsWith(IRadioService.StandardName))
                                {
                                    return syntax;
                                }
                            }
                        }*/
                    }

                    return null;
                })
            .Collect());

        context.RegisterImplementationSourceOutput(provider, this.Emit);
    }

    private void Emit(SourceProductionContext context, (Compilation Compilation, ImmutableArray<InterfaceDeclarationSyntax?> Types) source)
    {
        var compilation = source.Compilation;

        var generatorOptionAttributeSymbol = compilation.GetTypeByMetadataName(CrossChannelGeneratorOptionAttributeMock.FullName);
        if (generatorOptionAttributeSymbol == null)
        {
            return;
        }

        var iRadioService = compilation.GetTypeByMetadataName(IRadioService.FullName);
        if (iRadioService == null)
        {
            return;
        }

        var radioServiceInterface = compilation.GetTypeByMetadataName(RadioServiceInterfaceAttributeMock.FullName);
        if (radioServiceInterface == null)
        {
            return;
        }

        this.AssemblyName = compilation.AssemblyName ?? string.Empty;
        this.AssemblyId = this.AssemblyName.GetHashCode();
        this.OutputKind = compilation.Options.OutputKind;

        var body = new CrossChannelBody(context);
#pragma warning disable RS1024 // Symbols should be compared for equality
        var processed = new HashSet<INamedTypeSymbol?>();
#pragma warning restore RS1024 // Symbols should be compared for equality

        var generatorOptionIsSet = false;
        foreach (var x in source.Types)
        {
            if (x == null)
            {
                continue;
            }

            context.CancellationToken.ThrowIfCancellationRequested();

            var model = compilation.GetSemanticModel(x.SyntaxTree);
            if (model.GetDeclaredSymbol(x) is INamedTypeSymbol symbol &&
                symbol.TypeKind == TypeKind.Interface &&
                !processed.Contains(symbol))
            {
                processed.Add(symbol);

                foreach (var y in symbol.GetAttributes())
                {
                    if (!generatorOptionIsSet &&
                        SymbolEqualityComparer.Default.Equals(y.AttributeClass, generatorOptionAttributeSymbol))
                    {// [CrossChannelGeneratorOption]
                        generatorOptionIsSet = true;
                        var attribute = new VisceralAttribute(CrossChannelGeneratorOptionAttributeMock.FullName, y);
                        var generatorOption = CrossChannelGeneratorOptionAttributeMock.FromArray(attribute.ConstructorArguments, attribute.NamedArguments);

                        this.AttachDebugger = generatorOption.AttachDebugger;
                        this.GenerateToFile = generatorOption.GenerateToFile;
                        this.CustomNamespace = generatorOption.CustomNamespace;
                        this.TargetFolder = Path.Combine(Path.GetDirectoryName(x.SyntaxTree.FilePath), "Generated");
                    }
                    else if (SymbolEqualityComparer.Default.Equals(y.AttributeClass, radioServiceInterface))
                    {// [RadioServiceInterface]
                        body.Add(symbol);
                        // if (symbol.AllInterfaces.Any(z => SymbolEqualityComparer.Default.Equals(z, iRadioService))) // IRadioService (check later)
                    }
                }
            }
        }

        context.CancellationToken.ThrowIfCancellationRequested();
        body.Prepare();
        if (body.Abort)
        {
            return;
        }

        context.CancellationToken.ThrowIfCancellationRequested();
        body.Generate(this, context.CancellationToken);
    }
}
