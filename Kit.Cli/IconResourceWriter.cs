using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Kit.Cli;

internal static class IconResourceWriter
{
    // ReSharper disable InconsistentNaming
    private const uint RT_ICON       = 3;
    private const uint RT_GROUP_ICON = 14;
    // ReSharper enable InconsistentNaming

    public static void WriteIcon(string executablePath, string iconPath)
    {
        if (!File.Exists(iconPath))
        {
            throw new FileNotFoundException("Icon file not found.", iconPath);
        }

        var iconData = File.ReadAllBytes(iconPath);

        using var ms     = new MemoryStream(iconData);
        using var reader = new BinaryReader(ms);

        var header = ReadStruct<Native.ICONDIR>(reader);
        if (header.Reserved != 0 || header.Type != 1)
        {
            throw new InvalidDataException("Invalid .ico file format.");
        }

        if (header.Count == 0)
        {
            throw new InvalidDataException("The .ico file does not contain any icon images.");
        }

        var entries = new Native.ICONDIRENTRY[header.Count];
        for (var i = 0; i < header.Count; i++)
        {
            entries[i] = ReadStruct<Native.ICONDIRENTRY>(reader);
        }

        var hUpdate = Native.BeginUpdateResource(executablePath, false);
        if (hUpdate == IntPtr.Zero)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to begin resource update.");
        }

        try
        {
            var grpHeader = new Native.GRPICONDIR
            {
                Reserved = 0,
                Type     = 1,
                Count    = header.Count
            };

            var grpEntries = new Native.GRPICONDIRENTRY[header.Count];

            for (ushort i = 0; i < header.Count; i++)
            {
                var entry          = entries[i];
                var imageEndOffset = (ulong)entry.ImageOffset + entry.BytesInRes;
                if (entry.ImageOffset >= ms.Length || imageEndOffset > (ulong)ms.Length)
                {
                    throw new InvalidDataException($"Icon image {i + 1} points outside the .ico file.");
                }

                ms.Seek(entry.ImageOffset, SeekOrigin.Begin);
                var imageBytes = reader.ReadBytes((int)entry.BytesInRes);
                if (imageBytes.Length != entry.BytesInRes)
                {
                    throw new EndOfStreamException($"Icon image {i + 1} could not be fully read from the .ico file.");
                }

                // use i + 1 as the ID for each icon resource
                var iconId = (ushort)(i + 1);
                if (!Native.UpdateResource(hUpdate, RT_ICON, iconId, 0, imageBytes, (uint)imageBytes.Length))
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), $"Failed to update RT_ICON resource {iconId}.");
                }

                grpEntries[i] = new Native.GRPICONDIRENTRY
                {
                    Width      = entry.Width,
                    Height     = entry.Height,
                    ColorCount = entry.ColorCount,
                    Reserved   = entry.Reserved,
                    Planes     = entry.Planes,
                    BitCount   = entry.BitCount,
                    BytesInRes = entry.BytesInRes,
                    ID         = iconId
                };
            }

            // most Windows apps use ID 1 for the main icon group
            var grpData = StructureToByteArray(grpHeader, grpEntries);
            if (!Native.UpdateResource(hUpdate, RT_GROUP_ICON, 1, 0, grpData, (uint)grpData.Length))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to update RT_GROUP_ICON resource.");
            }

            if (!Native.EndUpdateResource(hUpdate, false))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to finalize resource update.");
            }
        }
        catch
        {
            Native.EndUpdateResource(hUpdate, true); // discard changes on error
            throw;
        }
    }

    private static T ReadStruct<T>(BinaryReader reader)
        where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();

        var bytes = reader.ReadBytes(size);
        if (bytes.Length != size)
        {
            throw new EndOfStreamException("Unexpected end of file while reading icon metadata.");
        }

        return MemoryMarshal.Read<T>(bytes);
    }

    private static byte[] StructureToByteArray(Native.GRPICONDIR header, Native.GRPICONDIRENTRY[] entries)
    {
        var headerSize = Marshal.SizeOf<Native.GRPICONDIR>();
        var entrySize  = Marshal.SizeOf<Native.GRPICONDIRENTRY>();
        var totalSize  = headerSize + (entrySize * entries.Length);
        var bytes      = new byte[totalSize];

        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var ptr = handle.AddrOfPinnedObject();
            Marshal.StructureToPtr(header, ptr, false);
            ptr += headerSize;
            foreach (var entry in entries)
            {
                Marshal.StructureToPtr(entry, ptr, false);
                ptr += entrySize;
            }
        }
        finally
        {
            handle.Free();
        }

        return bytes;
    }
}

