using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Vst3.Plugin.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class Vst3PluginGenerator : IIncrementalGenerator
{
    private const string PluginAttributeName = "Vst3.VstPluginAttribute";
    private const string ParameterAttributeName = "Vst3.ParameterAttribute";
    private const string PluginBaseTypeName = "Vst3.AudioPlugin";
    private const string ParameterTypeName = "Vst3.FloatParameter";
    private const string EditorFactoryTypeName = "Vst3.Editor.ImGuiEditorFactory";

    private static readonly DiagnosticDescriptor MultiplePlugins = new(
        "VST3001",
        "Only one [VstPlugin] class is supported per assembly",
        "Assembly '{0}' declares more than one [VstPlugin] class; a VST3 module exposes exactly one plugin class",
        "Vst3.Plugin.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NotAPlugin = new(
        "VST3002",
        "[VstPlugin] class must derive from AudioPlugin",
        "'{0}' is marked [VstPlugin] but does not derive from Vst3.AudioPlugin",
        "Vst3.Plugin.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor BadParameterField = new(
        "VST3003",
        "[Parameter] field must be a FloatParameter",
        "Field '{0}' is marked [Parameter] but is not of type Vst3.FloatParameter",
        "Vst3.Plugin.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NotPartial = new(
        "VST3004",
        "[VstPlugin] class must be partial",
        "'{0}' must be declared partial so the parameter bindings can be generated",
        "Vst3.Plugin.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedShape = new(
        "VST3005",
        "[VstPlugin] class has an unsupported shape",
        "'{0}' must be a non-generic, non-nested class",
        "Vst3.Plugin.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor StaticParameter = new(
        "VST3006",
        "[Parameter] field must be an instance field",
        "Field '{0}' is static; each plugin instance needs its own parameter state",
        "Vst3.Plugin.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<PluginModel?> models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                PluginAttributeName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, ct) => Extract(ctx, ct))
            .Where(static model => model is not null);

        IncrementalValueProvider<(Compilation Left, ImmutableArray<PluginModel?> Right)> input =
            context.CompilationProvider.Combine(models.Collect());

        context.RegisterSourceOutput(input, static (spc, pair) => Execute(spc, pair.Left, pair.Right));
    }

    private static PluginModel? Extract(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol type)
            return null;

        AttributeData? attribute = context.Attributes.FirstOrDefault(
            a => a.AttributeClass?.ToDisplayString() == PluginAttributeName);
        if (attribute is null)
            return null;

        var diagnostics = new List<Diagnostic>();
        Location location = type.Locations.FirstOrDefault() ?? Location.None;

        if (!IsPartial(type, ct))
            diagnostics.Add(Diagnostic.Create(NotPartial, location, type.Name));

        if (!DerivesFrom(type, PluginBaseTypeName))
            diagnostics.Add(Diagnostic.Create(NotAPlugin, location, type.Name));

        if (type.Arity != 0 || type.ContainingType is not null)
            diagnostics.Add(Diagnostic.Create(UnsupportedShape, location, type.Name));

        string pluginName = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string ?? type.Name
            : type.Name;

        int kind = GetNamedInt(attribute, "Kind", 0);
        bool isInstrument = kind == 1;
        string? subCategories = GetNamedString(attribute, "SubCategories");

        var model = new PluginModel
        {
            FullTypeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Namespace = type.ContainingNamespace.IsGlobalNamespace
                ? null
                : type.ContainingNamespace.ToDisplayString(),
            ClassName = type.Name,
            Name = pluginName,
            Vendor = GetNamedString(attribute, "Vendor") ?? "Unknown",
            Url = GetNamedString(attribute, "Url") ?? "",
            Email = GetNamedString(attribute, "Email") ?? "",
            Version = GetNamedString(attribute, "Version") ?? "1.0.0",
            SubCategories = subCategories ?? (isInstrument ? "Instrument|Synth" : "Fx"),
            IsInstrument = isInstrument,
            ClassId = CreateClassId(pluginName, GetNamedString(attribute, "Vendor") ?? "Unknown"),
            Parameters = ExtractParameters(type, diagnostics),
            Diagnostics = diagnostics.ToImmutableArray(),
            Location = location,
        };

        return model;
    }

    private static ImmutableArray<ParameterModel> ExtractParameters(
        INamedTypeSymbol type,
        List<Diagnostic> diagnostics)
    {
        var parameters = ImmutableArray.CreateBuilder<ParameterModel>();

        IEnumerable<IFieldSymbol> fields = type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == ParameterAttributeName))
            .OrderBy(f => f.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue);

        int index = 0;
        foreach (IFieldSymbol field in fields)
        {
            Location location = field.Locations.FirstOrDefault() ?? Location.None;
            AttributeData attribute = field.GetAttributes().First(
                a => a.AttributeClass?.ToDisplayString() == ParameterAttributeName);

            if (field.IsStatic)
            {
                diagnostics.Add(Diagnostic.Create(StaticParameter, location, field.Name));
                continue;
            }

            if (field.Type.ToDisplayString() != ParameterTypeName)
            {
                diagnostics.Add(Diagnostic.Create(BadParameterField, location, field.Name));
                continue;
            }

            string title = attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as string ?? field.Name
                : field.Name;

            parameters.Add(new ParameterModel
            {
                Index = index++,
                FieldName = field.Name,
                Title = title,
                ShortTitle = GetNamedString(attribute, "ShortTitle") ?? title,
                Units = GetNamedString(attribute, "Unit") ?? "",
                Min = GetNamedDouble(attribute, "Min", 0.0),
                Max = GetNamedDouble(attribute, "Max", 1.0),
                Default = GetNamedDouble(attribute, "Default", 0.0),
                StepCount = GetNamedInt(attribute, "Steps", 0),
                CanAutomate = GetNamedBool(attribute, "CanAutomate", true),
            });
        }

        return parameters.ToImmutable();
    }

    private static void Execute(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<PluginModel?> models)
    {
        if (models.IsDefaultOrEmpty)
            return;

        bool hasEditor = compilation.GetTypeByMetadataName(EditorFactoryTypeName) is not null;

        for (int i = 0; i < models.Length; i++)
        {
            PluginModel? model = models[i];
            if (model is null)
                continue;

            bool failed = false;
            foreach (Diagnostic diagnostic in model.Diagnostics)
            {
                context.ReportDiagnostic(diagnostic);
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    failed = true;
            }

            if (failed)
                continue;

            if (i > 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MultiplePlugins,
                    model.Location ?? Location.None,
                    compilation.AssemblyName ?? "unknown"));
                continue;
            }

            string source = PluginEmitter.Emit(model, hasEditor);
            context.AddSource(model.ClassName + ".Vst3.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private static bool IsPartial(INamedTypeSymbol type, CancellationToken ct)
    {
        foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(ct) is not ClassDeclarationSyntax declaration)
                return false;

            bool partial = false;
            foreach (SyntaxToken modifier in declaration.Modifiers)
            {
                if (modifier.IsKind(SyntaxKind.PartialKeyword))
                {
                    partial = true;
                    break;
                }
            }

            if (!partial)
                return false;
        }

        return true;
    }

    private static bool DerivesFrom(INamedTypeSymbol type, string baseTypeName)
    {
        for (INamedTypeSymbol? current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == baseTypeName)
                return true;
        }

        return false;
    }

    private static string CreateClassId(string name, string vendor)
    {
        byte[] hash;
        using (var sha = SHA256.Create())
            hash = sha.ComputeHash(Encoding.UTF8.GetBytes("Vst3Sharp/" + vendor + "/" + name));

        var bytes = new byte[16];
        Array.Copy(hash, bytes, 16);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes).ToString("D");
    }

    private static string? GetNamedString(AttributeData attribute, string name)
    {
        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (argument.Key == name)
                return argument.Value.Value as string;
        }

        return null;
    }

    private static double GetNamedDouble(AttributeData attribute, string name, double fallback)
    {
        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (argument.Key == name && argument.Value.Value is not null)
                return Convert.ToDouble(argument.Value.Value, CultureInfo.InvariantCulture);
        }

        return fallback;
    }

    private static int GetNamedInt(AttributeData attribute, string name, int fallback)
    {
        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (argument.Key == name && argument.Value.Value is not null)
                return Convert.ToInt32(argument.Value.Value, CultureInfo.InvariantCulture);
        }

        return fallback;
    }

    private static bool GetNamedBool(AttributeData attribute, string name, bool fallback)
    {
        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (argument.Key == name)
                return argument.Value.Value is bool value ? value : fallback;
        }

        return fallback;
    }
}
