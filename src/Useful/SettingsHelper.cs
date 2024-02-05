using System.Security;
using System.Xml.Serialization;

namespace Useful;

/// <summary>
/// Collection of methods for working with setting files.
/// </summary>
public static class SettingsHelper
{
    /// <summary>
    /// Tries to open given path as a reading text stream.
    /// </summary>
    /// <param name="path">Path to open.</param>
    /// <returns>Result holding a <see cref="TextReader"/> of file at path if successful or an error.</returns>
    public static Result<TextReader> OpenRead(string path)
    {
        try
        {
            return new StreamReader(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException or ArgumentException)
        {
            return Result<TextReader>.FromFailure($"{path} is invalid:\n{ex.Message}");
        }
    }
    /// <summary>
    /// Tries to open given path as a writing text stream.
    /// </summary>
    /// <param name="path">Path to open.</param>
    /// <returns>Result holding a <see cref="TextReader"/> of file at path if successful or an error.</returns>
    public static Result<TextWriter> OpenWrite(string path)
    {
        try
        {
            return new StreamWriter(path);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or ArgumentException or IOException or DirectoryNotFoundException or PathTooLongException or IOException or SecurityException)
        {
            return Result<TextWriter>.FromFailure($"{path} could not be written to or created. Please check corresponding permissions.\n{ex.Message}");
        }
    }

    /// <summary>
    /// Try to open given path to read settings, in the case of failure try to create a default file.
    /// </summary>
    /// <param name="path">Path to the settings.</param>
    /// <param name="createDefaultCallback">Delegate which will fill a newly created file with defaults.</param>
    /// <returns>Result of the read operation.</returns>
    public static Result<TextReader> HandleSettingsOpen(string path, Action<TextWriter> createDefaultCallback)
    {
        var open = OpenRead(path);
        if (open.Value is { } reader)
            return reader;

        string firstPart = $"Settings file {path} could not be open:\n{open.Error}";

        var write = OpenWrite(path);
        if (write.Value is not { } writer)
        {
            return Result<TextReader>.FromFailure(firstPart + write.Error);
        }

        try
        {
            createDefaultCallback(writer);
        }
        catch (Exception ex)
        {
            return Result<TextReader>.FromFailure($"{firstPart} Default config could be not created due to an exception: {ex}.");
        }

        return Result<TextReader>.FromFailure(firstPart + "Default config created. Please set it up.");
    }
}

/// <summary>
/// Helper methods for XML settings represented by <typeparamref name="TSettings"/>.
/// </summary>
/// <typeparam name="TSettings">The type which shall serialize into the settings.</typeparam>
public static class SettingsHelper<TSettings> where TSettings : notnull
{
    static readonly XmlSerializer Serializer = new(typeof(TSettings));
    
    /// <summary>
    /// Serialize settings into a writer.
    /// </summary>
    /// <param name="settings">Settings object to serialize.</param>
    /// <param name="writer">Writer to serialize to.</param>
    public static void Serialize(TSettings settings, TextWriter writer)
    {
        Serializer.Serialize(writer, settings);
    }

    /// <summary>
    /// Try to deserialize given reader into <typeparamref name="TSettings"/>.
    /// </summary>
    /// <param name="reader">Reader to serialize from.</param>
    /// <returns>Result holding the representation of settings on success, error otherwise.</returns>
    public static Result<TSettings> Deserialize(TextReader reader)
    {
        try
        {
            return (TSettings)Serializer.Deserialize(reader)!;
        }
        catch (InvalidOperationException ex)
        {
            return Result<TSettings>.FromFailure($"Settings are in an invalid format:\n{ex.Message}");
        }
    }
}
