namespace TabHistorian.Parsing;

/// <summary>
/// Parses Chrome SNSS (Session Storage) binary files into a list of commands.
/// Supports version 1 (legacy) and version 3 (modern, with marker).
/// </summary>
public class SnssParser
{
    private const uint Magic = 0x53534E53; // "SNSS" in little-endian
    private const byte MarkerCommandId = 255;

    public List<SnssCommand> Parse(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        uint signature = reader.ReadUInt32();
        if (signature != Magic)
            throw new InvalidDataException($"Not an SNSS file (got 0x{signature:X8}, expected 0x{Magic:X8})");

        int version = reader.ReadInt32();
        if (version != 1 && version != 3)
            throw new InvalidDataException($"Unsupported SNSS version: {version}");

        var commands = new List<SnssCommand>();

        while (stream.Position < stream.Length)
        {
            if (stream.Length - stream.Position < 2)
                break;

            ushort recordSize = reader.ReadUInt16();
            if (recordSize == 0)
                break;

            if (stream.Length - stream.Position < recordSize)
                break; // truncated record, stop

            byte commandId = reader.ReadByte();
            byte[] payload = reader.ReadBytes(recordSize - 1);

            if (commandId == MarkerCommandId)
                continue; // version 3 initialization marker, skip

            commands.Add(new SnssCommand(commandId, payload));
        }

        return commands;
    }
}
