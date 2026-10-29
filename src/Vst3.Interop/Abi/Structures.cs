using System.Runtime.InteropServices;

namespace Vst3.Interop;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PFactoryInfo
{
    public const int VendorSize = 64;
    public const int UrlSize = 256;
    public const int EmailSize = 128;

    public const int FlagUnicode = 1 << 4;

    public fixed byte Vendor[VendorSize];
    public fixed byte Url[UrlSize];
    public fixed byte Email[EmailSize];
    public int Flags;

    public string GetVendor()
    {
        fixed (byte* p = Vendor)
            return Ascii.Read(p, VendorSize);
    }

    public string GetUrl()
    {
        fixed (byte* p = Url)
            return Ascii.Read(p, UrlSize);
    }

    public string GetEmail()
    {
        fixed (byte* p = Email)
            return Ascii.Read(p, EmailSize);
    }

    public void SetVendor(string? value)
    {
        fixed (byte* p = Vendor)
            Ascii.Write(p, VendorSize, value);
    }

    public void SetUrl(string? value)
    {
        fixed (byte* p = Url)
            Ascii.Write(p, UrlSize, value);
    }

    public void SetEmail(string? value)
    {
        fixed (byte* p = Email)
            Ascii.Write(p, EmailSize, value);
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PClassInfo
{
    public const int CategorySize = 32;
    public const int NameSize = 64;
    public const int ManyInstances = 0x7FFFFFFF;

    /// <summary>Class ID. TUID and Guid share the same 16-byte layout on Windows.</summary>
    public Guid Cid;

    public int Cardinality;

    public fixed byte Category[CategorySize];
    public fixed byte Name[NameSize];

    public string GetCategory()
    {
        fixed (byte* p = Category)
            return Ascii.Read(p, CategorySize);
    }

    public string GetName()
    {
        fixed (byte* p = Name)
            return Ascii.Read(p, NameSize);
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
}
