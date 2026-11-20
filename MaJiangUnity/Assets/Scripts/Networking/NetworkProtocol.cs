using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MaJiangLib
{
    /// <summary>局域网协议消息类型。帧版本与麻将规则版本分开管理。</summary>
    public enum RoomMessageType : ushort
    {
        Join = 1,
        Joined = 2,
        RoomState = 3,
        SetReady = 4,
        StartMatch = 5,
        Leave = 6,
        Result = 7,
        Heartbeat = 8,
        RequestState = 9,
        SetMatchType = 10,
        MatchSnapshot = 11,
        MatchLog = 12,
        MatchAck = 13,
        RequestMatchSync = 14,
        MatchCommand = 15,
        MatchCommandResult = 16,
        ConfirmSettlement = 17
    }

    /// <summary>已经完整组装的消息。</summary>
    public sealed class NetworkMessage
    {
        public RoomMessageType Type { get; }
        public uint Id { get; }
        public byte[] Payload { get; }

        public NetworkMessage(RoomMessageType type, uint id, byte[] payload)
        {
            Type = type;
            Id = id;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }
    }

    /// <summary>固定1024字节帧。所有整数采用小端，CRC覆盖除自身字段外的整帧。</summary>
    public static class NetworkFrameCodec
    {
        public const int FrameSize = 1024;
        public const ushort Version = 6;
        public const int PayloadOffset = 32;
        public const int PayloadCapacity = 984;
        public const int MaximumMessageSize = 65536;
        private static readonly byte[] Head = Encoding.ASCII.GetBytes("\u0002HaxMajP");
        private static readonly byte[] Tail = Encoding.ASCII.GetBytes("\u0003EndPack");

        /// <summary>消息的全部分片必须由同一个发送通道连续发送。</summary>
        public static IEnumerable<byte[]> Encode(NetworkMessage message)
        {
            if (message.Id == 0 || !Enum.IsDefined(typeof(RoomMessageType), message.Type)
                || message.Payload.Length > MaximumMessageSize)
            {
                throw new InvalidDataException("Invalid message header or length.");
            }
            int count = Math.Max(1, (message.Payload.Length + PayloadCapacity - 1) / PayloadCapacity);
            for (int index = 0; index < count; index++)
            {
                byte[] frame = new byte[FrameSize];
                Head.CopyTo(frame, 0);
                Tail.CopyTo(frame, 1016);
                BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(8), Version);
                BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(10), (ushort)message.Type);
                BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(12), message.Id);
                BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(16), message.Payload.Length);
                BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(20), (ushort)index);
                BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(22), (ushort)count);
                int length = Math.Min(PayloadCapacity, message.Payload.Length - index * PayloadCapacity);
                BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(24), (ushort)length);
                message.Payload.AsSpan(index * PayloadCapacity, length).CopyTo(frame.AsSpan(PayloadOffset));
                BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(28), CalculateChecksum(frame));
                yield return frame;
            }
        }

        /// <summary>验证固定偏移、版本、分片长度和校验，不搜索负载中的分隔符。</summary>
        public static void Validate(byte[] frame)
        {
            if (frame == null || frame.Length != FrameSize
                || !frame.AsSpan(0, 8).SequenceEqual(Head) || !frame.AsSpan(1016, 8).SequenceEqual(Tail))
            {
                throw new InvalidDataException("Invalid fixed frame boundary.");
            }
            if (BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(8)) != Version)
            {
                throw new InvalidDataException("Unsupported protocol version.");
            }
            if (!Enum.IsDefined(typeof(RoomMessageType), (RoomMessageType)BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(10)))
                || BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(12)) == 0
                || BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(26)) != 0)
            {
                throw new InvalidDataException("Invalid frame fields.");
            }
            int total = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(16));
            int index = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(20));
            int count = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(22));
            int length = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(24));
            if (total < 0 || total > MaximumMessageSize || count != Math.Max(1, (total + PayloadCapacity - 1) / PayloadCapacity)
                || index >= count || length != Math.Min(PayloadCapacity, total - index * PayloadCapacity))
            {
                throw new InvalidDataException("Invalid fragment dimensions.");
            }
            if (BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(28)) != CalculateChecksum(frame))
            {
                throw new InvalidDataException("Frame checksum mismatch.");
            }
        }

        private static uint CalculateChecksum(byte[] frame)
        {
            uint crc = uint.MaxValue;
            for (int index = 0; index < frame.Length; index++)
            {
                byte value = index >= 28 && index < 32 ? (byte)0 : frame[index];
                crc ^= value;
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xEDB88320u : 0u);
                }
            }
            return ~crc;
        }
    }

    /// <summary>每连接最多组装一个有界消息，分片必须连续且按序。只在完整消息到齐时交付。</summary>
    public sealed class NetworkMessageAssembler
    {
        private byte[] payload;
        private uint messageId;
        private RoomMessageType messageType;
        private int nextFragment;
        private uint previousMessageId;

        public bool HasPartialMessage => payload != null;

        public NetworkMessage AddFrame(byte[] frame)
        {
            NetworkFrameCodec.Validate(frame);
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(12));
            RoomMessageType type = (RoomMessageType)BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(10));
            int total = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(16));
            int index = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(20));
            int count = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(22));
            int length = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(24));
            if (payload == null)
            {
                uint expected = previousMessageId == uint.MaxValue ? 1 : previousMessageId + 1;
                if (index != 0 || id != expected)
                {
                    throw new InvalidDataException("Unexpected message or first fragment sequence.");
                }
                payload = new byte[total];
                messageId = id;
                messageType = type;
                nextFragment = 0;
            }
            if (id != messageId || type != messageType || total != payload.Length || index != nextFragment)
            {
                throw new InvalidDataException("Interleaved or out-of-order fragments.");
            }
            frame.AsSpan(NetworkFrameCodec.PayloadOffset, length).CopyTo(payload.AsSpan(index * NetworkFrameCodec.PayloadCapacity));
            nextFragment++;
            if (nextFragment != count)
            {
                return null;
            }
            NetworkMessage message = new(messageType, messageId, payload);
            previousMessageId = messageId;
            payload = null;
            return message;
        }
    }

    /// <summary>明确长度的消息字段读写器，拒绝截断、非法UTF-8、超长字符串及尾随数据。</summary>
    public sealed class RoomPayloadWriter : IDisposable
    {
        private readonly MemoryStream stream = new();
        private readonly BinaryWriter writer;
        private static readonly UTF8Encoding Utf8 = new(false, true);

        public RoomPayloadWriter() { writer = new BinaryWriter(stream, Utf8, true); }
        public void Byte(byte value) => writer.Write(value);
        public void Bool(bool value) => writer.Write((byte)(value ? 1 : 0));
        public void Int64(long value) => writer.Write(value);
        public void Int32(int value) => writer.Write(value);
        public void UInt32(uint value) => writer.Write(value);
        public void Guid(Guid value) => writer.Write(value.ToByteArray());
        public void Bytes(byte[] value, int maximumBytes)
        {
            if (value == null || value.Length > maximumBytes) { throw new InvalidDataException("Invalid binary field length."); }
            writer.Write(value.Length); writer.Write(value);
        }
        public void Text(string value, int maximumBytes)
        {
            byte[] bytes = Utf8.GetBytes(value ?? string.Empty);
            if (bytes.Length > maximumBytes) { throw new InvalidDataException("Text exceeds byte limit."); }
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }
        public byte[] ToArray() => stream.ToArray();
        public void Dispose() { writer.Dispose(); stream.Dispose(); }
    }

    public sealed class RoomPayloadReader : IDisposable
    {
        private readonly MemoryStream stream;
        private readonly BinaryReader reader;
        public RoomPayloadReader(byte[] bytes)
        {
            stream = new MemoryStream(bytes, false);
            reader = new BinaryReader(stream, new UTF8Encoding(false, true), true);
        }
        public byte Byte() => reader.ReadByte();
        public bool Bool()
        {
            byte value = Byte();
            if (value > 1) { throw new InvalidDataException("Invalid boolean."); }
            return value == 1;
        }
        public long Int64() => reader.ReadInt64();
        public int Int32() => reader.ReadInt32();
        public uint UInt32() => reader.ReadUInt32();
        public Guid Guid() => new(ReadExact(16));
        public byte[] Bytes(int maximumBytes)
        {
            int length = Int32();
            if (length < 0 || length > maximumBytes) { throw new InvalidDataException("Invalid binary field length."); }
            return ReadExact(length);
        }
        public string Text(int maximumBytes)
        {
            int length = reader.ReadUInt16();
            if (length > maximumBytes) { throw new InvalidDataException("Text exceeds byte limit."); }
            return new UTF8Encoding(false, true).GetString(ReadExact(length));
        }
        private byte[] ReadExact(int length)
        {
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length) { throw new EndOfStreamException("Truncated payload."); }
            return bytes;
        }
        public void Complete()
        {
            if (stream.Position != stream.Length) { throw new InvalidDataException("Trailing payload data."); }
        }
        public void Dispose() { reader.Dispose(); stream.Dispose(); }
    }
}
