using System.Runtime.InteropServices;

namespace Vst3.Interop;

internal static unsafe class Ascii
{
    public static void Write(byte* destination, int capacity, string? value)
    {
        if (capacity <= 0)
            return;

        if (string.IsNullOrEmpty(value))
        {
            destination[0] = 0;
            return;
        }

        int count = Math.Min(value.Length, capacity - 1);
        for (int i = 0; i < count; i++)
        {
            char c = value[i];
            destination[i] = c <= 0x7F ? (byte)c : (byte)'?';
        }

        destination[count] = 0;
    }

    public static string Read(byte* source, int capacity)
    {
        int length = 0;
        while (length < capacity && source[length] != 0)
            length++;

        return System.Text.Encoding.ASCII.GetString(source, length);
    }
}
