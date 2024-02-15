using System;
using MemoryPack;
using Useful;

namespace Core.Utility;

/// <summary>
/// Extensions for <see cref="PooledBufferWriter{T}"/>.
/// </summary>
public static class PooledBufferWriterExtensions
{
    /// <summary>
    /// Use MemoryPack to serialize given object into the pooled buffer writer, then extract and replace the buffer. 
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="writer">The writer to serialize to.</param>
    /// <param name="value">The object to be serialized.</param>
    /// <returns>The extracted buffer of the serialized object.</returns>
    public static Memory<byte> MemoryPackSerialize<T>(this PooledBufferWriter<byte> writer, in T value)
    {
        writer.Reset();
        MemoryPackSerializer.Serialize(writer, value);
        return writer.ExtractAndReplace();
    }

    /// <summary>
    /// Copy given object using MemoryPack and the pooled buffer writer.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="writer">The writer to use for copying.</param>
    /// <param name="original">The object to copy.</param>
    /// <param name="copy">The destination to copy to.</param>
    public static void Copy<T>(this PooledBufferWriter<byte> writer, in T original, ref T? copy)
    {
        writer.Reset();
        MemoryPackSerializer.Serialize(writer, original);
        MemoryPackSerializer.Deserialize(writer.WrittenSpan, ref copy);
        writer.Reset();
    }

    /// <summary>
    /// Write given <see cref="int"/> value in little-endian into the buffer of the writer(the value is appended).
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to write.</param>
    public static void Write(this PooledBufferWriter<byte> writer, int value)
    {
        const int size = sizeof(int);
        var target = writer.GetSpan(size);
        Bits.Write(value, target);
        writer.Advance(size);
    }

    /// <summary>
    /// Write given <see cref="long"/> value in little-endian into the buffer of the writer(the value is appended).
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to write.</param>
    public static void Write(this PooledBufferWriter<byte> writer, long value)
    {
        const int size = sizeof(long);
        var target = writer.GetSpan(size);
        Bits.Write(value, target);
        writer.Advance(size);
    }

    /// <summary>
    /// Write given nullable <see cref="int"/> in the format of <see cref="Bits.Write(int?,Span{byte})"/>.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to write.</param>
    public static void Write(this PooledBufferWriter<byte> writer, int? value)
    {
        const int size = Bits.NullableIntSize;
        var target = writer.GetSpan(size);
        Bits.Write(value, target);
        writer.Advance(size);
    }

    /// <summary>
    /// Write given nullable <see cref="long"/> in the format of <see cref="Bits.Write(long?,Span{byte})"/>.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to write.</param>
    public static void Write(this PooledBufferWriter<byte> writer, long? value)
    {
        const int size = Bits.NullableLongSize;
        var target = writer.GetSpan(size);
        Bits.Write(value, target);
        writer.Advance(size);
    }
    
    /// <summary>
    /// Skip given amount of bytes in the buffer writer without writing anything.
    /// </summary>
    /// <param name="writer">The writer to use.</param>
    /// <param name="amount">The amount of bytes to skip.</param>
    public static void Skip(this PooledBufferWriter<byte> writer, int amount)
    {
        writer.GetSpan(amount);
        writer.Advance(amount);
    }
}
