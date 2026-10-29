namespace Vst3.Interop;

/// <summary>VST3 tresult values. Kept as int to avoid bool marshalling.</summary>
public static class Result
{
    public const int Ok = 0;
    public const int True = 0;
    public const int False = 1;
    public const int NotImplemented = unchecked((int)0x80004001);
    public const int NoInterface = unchecked((int)0x80004002);
    public const int InternalError = unchecked((int)0x80004005);
    public const int InvalidArgument = unchecked((int)0x80070057);
}
