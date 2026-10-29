namespace Vst3;

/// <summary>Marks an <see cref="AudioPlugin"/> as the VST3 class exposed by this assembly.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class VstPluginAttribute : Attribute
{
    public VstPluginAttribute(string name) => Name = name;

    /// <summary>Class name shown by the host.</summary>
    public string Name { get; }

    public string Vendor { get; set; } = "Unknown";

    public string Version { get; set; } = "1.0.0";

    public string Url { get; set; } = "";

    public string Email { get; set; } = "";

    /// <summary>VST3 sub-categories; defaults to <c>Fx</c> or <c>Instrument|Synth</c>.</summary>
    public string? SubCategories { get; set; }

    public PluginKind Kind { get; set; } = PluginKind.Effect;

    /// <summary>Custom editor type deriving from <c>Vst3.Editor.EditorView</c>; default is the generic parameter panel.</summary>
    public Type? Editor { get; set; }
}

/// <summary>Declares a <see cref="FloatParameter"/> field as an automatable parameter.</summary>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ParameterAttribute : Attribute
{
    public ParameterAttribute(string name) => Title = name;

    /// <summary>Display name shown by the host.</summary>
    public string Title { get; }

    public string? ShortTitle { get; set; }

    public string Unit { get; set; } = "";

    public double Min { get; set; }

    public double Max { get; set; } = 1.0;

    public double Default { get; set; }

    /// <summary>Number of discrete steps; 0 means continuous.</summary>
    public int Steps { get; set; }

    public bool CanAutomate { get; set; } = true;
}
