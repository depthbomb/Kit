using System.ComponentModel;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace Kit.Updater;

public static class NtfsCompressor
{
    // ReSharper disable InconsistentNaming
    private const uint FSCTL_SET_COMPRESSION = 0x9C040;

    private const short COMPRESSION_FORMAT_DEFAULT = 1;

    private const uint GENERIC_READ  = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;

    private const uint FILE_SHARE_READ   = 0x00000001;
    private const uint FILE_SHARE_WRITE  = 0x00000002;
    private const uint FILE_SHARE_DELETE = 0x00000004;

    private const uint OPEN_EXISTING = 3;

    private const uint FILE_ATTRIBUTE_NORMAL      = 0x80;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    // ReSharper enable InconsistentNaming

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string lpFileName,
                                                    uint   dwDesiredAccess,
                                                    uint   dwShareMode,
                                                    IntPtr lpSecurityAttributes,
                                                    uint   dwCreationDisposition,
                                                    uint   dwFlagsAndAttributes,
                                                    IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern unsafe bool DeviceIoControl(SafeFileHandle hDevice,
                                                      uint           dwIoControlCode,
                                                      void*          lpInBuffer,
                                                      uint           nInBufferSize,
                                                      void*          lpOutBuffer,
                                                      uint           nOutBufferSize,
                                                      out uint       lpBytesReturned,
                                                      IntPtr         lpOverlapped);

    public static void CompressDirectoryRecursive(string rootPath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!TryCompressDirectory(rootPath))
        {
            return;
        }

        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootPath);

        while (pendingDirectories.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var directory = pendingDirectories.Pop();
            string[] entries;
            try
            {
                entries = Directory.GetFileSystemEntries(directory);
            }
            catch (Exception exception) when (IsOptionalCompressionFailure(exception))
            {
                continue;
            }

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();

                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (Exception exception) when (IsOptionalCompressionFailure(exception))
                {
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (TryCompressDirectory(entry))
                    {
                        pendingDirectories.Push(entry);
                    }
                }
                else
                {
                    TryCompressFile(entry);
                }
            }
        }
    }

    private static bool TryCompressDirectory(string path)
    {
        try
        {
            CompressDirectory(path);
            return true;
        }
        catch (Exception exception) when (IsOptionalCompressionFailure(exception))
        {
            return false;
        }
    }

    private static void TryCompressFile(string path)
    {
        try
        {
            CompressFile(path);
        }
        catch (Exception exception) when (IsOptionalCompressionFailure(exception))
        {
            // NTFS compression is an optional optimization.
        }
    }

    private static bool IsOptionalCompressionFailure(Exception exception)
        => exception is Win32Exception
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException;

    private static void CompressDirectory(string path)
    {
        using (var handle = OpenDirectoryHandle(path))
        {
            SetCompression(handle, COMPRESSION_FORMAT_DEFAULT);
        }
    }

    private static void CompressFile(string path)
    {
        using (var handle = OpenFileHandle(path))
        {
            SetCompression(handle, COMPRESSION_FORMAT_DEFAULT);
        }
    }

    private static SafeFileHandle OpenFileHandle(string path)
    {
        var handle = CreateFile(
            path,
            GENERIC_READ    | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);

        return handle.IsInvalid ? throw new Win32Exception(Marshal.GetLastWin32Error()) : handle;
    }

    private static SafeFileHandle OpenDirectoryHandle(string path)
    {
        var handle = CreateFile(
            path,
            GENERIC_READ    | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);

        return handle.IsInvalid ? throw new Win32Exception(Marshal.GetLastWin32Error()) : handle;
    }

    private static unsafe void SetCompression(SafeFileHandle handle, short compressionFormat)
    {
        var success = DeviceIoControl(
            handle,
            FSCTL_SET_COMPRESSION,
            &compressionFormat,
            sizeof(short),
            null,
            0,
            out _,
            IntPtr.Zero);

        if (!success)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
