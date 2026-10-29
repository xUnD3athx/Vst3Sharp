using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Vst3.Plugin.Generator;

internal sealed class PluginModel
{
    public string FullTypeName = "";
    public string? Namespace;
    public string ClassName = "";
    public string Name = "";
    public string Vendor = "";
    public string Url = "";
    public string Email = "";
    public string Version = "";
    public string SubCategories = "";
    public bool IsInstrument;
    public string ClassId = "";
    public ImmutableArray<ParameterModel> Parameters = ImmutableArray<ParameterModel>.Empty;
    public ImmutableArray<Diagnostic> Diagnostics = ImmutableArray<Diagnostic>.Empty;
    public Location? Location;
}

internal sealed class ParameterModel
{
    public int Index;
    public string FieldName = "";
    public string Title = "";
    public string ShortTitle = "";
    public string Units = "";
    public double Min;
    public double Max;
    public double Default;
    public int StepCount;
    public bool CanAutomate;
}
