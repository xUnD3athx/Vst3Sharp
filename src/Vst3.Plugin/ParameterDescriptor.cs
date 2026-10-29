using Vst3.Interop;

namespace Vst3.Plugin;

/// <summary>Declarative parameter definition. Values are plain (not normalized).</summary>
public sealed record ParameterDescriptor(
    uint Id,
    string Title,
    string ShortTitle = "",
    string Units = "",
    double Min = 0.0,
    double Max = 1.0,
    double Default = 0.0,
    int StepCount = 0,
    int Flags = ParameterFlags.CanAutomate);
