using System.Text;

// ReSharper disable once CheckNamespace
namespace Shared;

internal static class StampPayload
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("UPDATER-CFG-V1");

    public static void WriteConfigurationJson(string executablePath, string json)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(json);
        var trimmedBytes = RemoveExistingStamp(File.ReadAllBytes(executablePath));

        using (var output = new FileStream(executablePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            output.Write(trimmedBytes, 0, trimmedBytes.Length);
            output.Write(payloadBytes, 0, payloadBytes.Length);
            output.Write(BitConverter.GetBytes(payloadBytes.Length), 0, sizeof(int));
            output.Write(Magic, 0, Magic.Length);
        }
    }

    public static string ReadConfigurationJson(string executablePath)
    {
        using (var stream = new FileStream(executablePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            if (stream.Length < Magic.Length + sizeof(int))
            {
                throw new InvalidOperationException("The updater binary is not stamped.");
            }

            stream.Seek(-Magic.Length, SeekOrigin.End);

            var tailMagic = new byte[Magic.Length];

            ReadExact(stream, tailMagic, 0, tailMagic.Length);

            if (!BytesEqual(tailMagic, Magic))
            {
                throw new InvalidOperationException("The updater binary does not contain stamped configuration data.");
            }

            stream.Seek(-(Magic.Length + sizeof(int)), SeekOrigin.End);

            var lengthBytes = new byte[sizeof(int)];

            ReadExact(stream, lengthBytes, 0, lengthBytes.Length);

            var payloadLength = BitConverter.ToInt32(lengthBytes, 0);
            if (payloadLength <= 0 || payloadLength > stream.Length - Magic.Length - sizeof(int))
            {
                throw new InvalidOperationException("The stamped configuration data is invalid.");
            }

            stream.Seek(-(Magic.Length + sizeof(int) + payloadLength), SeekOrigin.End);

            var payloadBytes = new byte[payloadLength];

            ReadExact(stream, payloadBytes, 0, payloadBytes.Length);

            return Encoding.UTF8.GetString(payloadBytes);
        }
    }

    private static byte[] RemoveExistingStamp(byte[] fileBytes)
    {
        if (fileBytes.Length < Magic.Length + sizeof(int))
        {
            return fileBytes;
        }

        var magicStart = fileBytes.Length - Magic.Length;
        if (Magic.Where((t, index) => fileBytes[magicStart + index] != t).Any())
        {
            return fileBytes;
        }

        var lengthOffset  = fileBytes.Length - Magic.Length - sizeof(int);
        var payloadLength = BitConverter.ToInt32(fileBytes, lengthOffset);
        var trimmedLength = fileBytes.Length - Magic.Length - sizeof(int) - payloadLength;
        if (payloadLength <= 0 || trimmedLength <= 0)
        {
            return fileBytes;
        }

        var trimmedBytes = new byte[trimmedLength];

        Buffer.BlockCopy(fileBytes, 0, trimmedBytes, 0, trimmedLength);

        return trimmedBytes;
    }

    private static void ReadExact(Stream stream, byte[] buffer, int offset, int count)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (read <= 0)
            {
                throw new EndOfStreamException("Unexpected end of file while reading stamped configuration data.");
            }

            totalRead += read;
        }
    }

    private static bool BytesEqual(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        return !left.Where((t, i) => t != right[i]).Any();
    }
}
