using System.Runtime.InteropServices;

namespace Vst3.Interop;

public static class MediaTypes
{
    public const int Audio = 0;
    public const int Event = 1;
}

public static class BusDirections
{
    public const int Input = 0;
    public const int Output = 1;
}

public static class BusTypes
{
    public const int Main = 0;
    public const int Aux = 1;
}

public static class BusFlags
{
    public const int DefaultActive = 1 << 0;
}

public static class SampleSizes
{
    public const int Sample32 = 0;
    public const int Sample64 = 1;
}

public static class ProcessModes
{
    public const int Realtime = 0;
    public const int Prefetch = 1;
    public const int Offline = 2;
}

public static class ParameterFlags
{
    public const int CanAutomate = 1 << 0;
    public const int IsReadOnly = 1 << 1;
    public const int IsHidden = 1 << 4;
    public const int IsBypass = 1 << 6;
}

public static unsafe class String128
{
    public const int Capacity = 128;

    public static void Write(char* destination, string? value)
    {
        int count = value is null ? 0 : Math.Min(value.Length, Capacity - 1);
        for (int i = 0; i < count; i++)
            destination[i] = value![i];

        destination[count] = '\0';
    }

    public static string Read(char* source)
    {
        int length = 0;
        while (length < Capacity && source[length] != '\0')
            length++;

        return new string(source, 0, length);
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PClassInfo2
{
    public const int CategorySize = 32;
    public const int NameSize = 64;
    public const int VendorSize = 64;
    public const int VersionSize = 64;
    public const int SubCategoriesSize = 128;

    public Guid Cid;
    public int Cardinality;
    public fixed byte Category[CategorySize];
    public fixed byte Name[NameSize];
    public uint ClassFlags;
    public fixed byte SubCategories[SubCategoriesSize];
    public fixed byte Vendor[VendorSize];
    public fixed byte Version[VersionSize];
    public fixed byte SdkVersion[VersionSize];

    public string GetSubCategories()
    {
        fixed (byte* p = SubCategories)
            return Ascii.Read(p, SubCategoriesSize);
    }

    public void SetCategory(string? value)
    {
        fixed (byte* p = Category)
            Ascii.Write(p, CategorySize, value);
    }

    public void SetName(string? value)
    {
        fixed (byte* p = Name)
            Ascii.Write(p, NameSize, value);
    }

    public void SetSubCategories(string? value)
    {
        fixed (byte* p = SubCategories)
            Ascii.Write(p, SubCategoriesSize, value);
    }

    public void SetVendor(string? value)
    {
        fixed (byte* p = Vendor)
            Ascii.Write(p, VendorSize, value);
    }

    public void SetVersion(string? value)
    {
        fixed (byte* p = Version)
            Ascii.Write(p, VersionSize, value);
    }

    public void SetSdkVersion(string? value)
    {
        fixed (byte* p = SdkVersion)
            Ascii.Write(p, VersionSize, value);
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct BusInfo
{
    public const int NameSize = 128;

    public int MediaType;
    public int Direction;
    public int ChannelCount;
    public fixed char Name[NameSize];
    public int BusType;
    public uint Flags;

    public void SetName(string? value)
    {
        fixed (char* p = Name)
        {
            int count = value is null ? 0 : Math.Min(value.Length, NameSize - 1);
            for (int i = 0; i < count; i++)
                p[i] = value![i];
            p[count] = '\0';
        }
    }

    public string GetName()
    {
        fixed (char* p = Name)
            return String128.Read(p);
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct ProcessSetup
{
    public int ProcessMode;
    public int SymbolicSampleSize;
    public int MaxSamplesPerBlock;
    public double SampleRate;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AudioBusBuffers
{
    public int NumChannels;
    public ulong SilenceFlags;
    public float** ChannelBuffers32;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ProcessData
{
    public int ProcessMode;
    public int SymbolicSampleSize;
    public int NumSamples;
    public int NumInputs;
    public int NumOutputs;
    public AudioBusBuffers* Inputs;
    public AudioBusBuffers* Outputs;
    public void* InputParameterChanges;
    public void* OutputParameterChanges;
    public void* InputEvents;
    public void* OutputEvents;
    public void* ProcessContext;
}

[StructLayout(LayoutKind.Sequential)]
public struct ViewRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ParameterInfo
{
    public const int StringSize = 128;

    public uint Id;
    public fixed char Title[StringSize];
    public fixed char ShortTitle[StringSize];
    public fixed char Units[StringSize];
    public int StepCount;
    public double DefaultNormalizedValue;
    public int UnitId;
    public int Flags;

    public void SetTitle(string? value)
    {
        fixed (char* p = Title)
            String128.Write(p, value);
    }

    public void SetShortTitle(string? value)
    {
        fixed (char* p = ShortTitle)
            String128.Write(p, value);
    }

    public void SetUnits(string? value)
    {
        fixed (char* p = Units)
            String128.Write(p, value);
    }
}
