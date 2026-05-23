using System.Runtime.InteropServices;

namespace Kit.Cli;

// TODO Port interop code to CsWin32

internal static class Native
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr BeginUpdateResource(string pFileName, [MarshalAs(UnmanagedType.Bool)] bool bDeleteExistingResources);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool UpdateResource(IntPtr hUpdate, uint lpType, ushort lpName, ushort wLanguage, byte[] lpData, uint cbData);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

    // ReSharper disable InconsistentNaming
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ICONDIR
    {
        public ushort Reserved;
        public ushort Type;
        public ushort Count;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ICONDIRENTRY
    {
        public byte   Width;
        public byte   Height;
        public byte   ColorCount;
        public byte   Reserved;
        public ushort Planes;
        public ushort BitCount;
        public uint   BytesInRes;
        public uint   ImageOffset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GRPICONDIR
    {
        public ushort Reserved;
        public ushort Type;
        public ushort Count;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GRPICONDIRENTRY
    {
        public byte   Width;
        public byte   Height;
        public byte   ColorCount;
        public byte   Reserved;
        public ushort Planes;
        public ushort BitCount;
        public uint   BytesInRes;
        public ushort ID;
    }
    // ReSharper enable InconsistentNaming
}
