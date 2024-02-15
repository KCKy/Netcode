using System;

namespace Useful;

/// <summary>
/// Represents a result of an operation which does not return a value on success.
/// If it represents a failure, it contains a message describing the error.
/// </summary>
public struct Result
{
    string? message_;

    /// <summary>
    /// Construct an instance which represents failure.
    /// </summary>
    /// <param name="message">The text describing the error.</param>
    /// <returns>Object representing the error.</returns>
    public static Result FromFailure(string message)
    {
        Result result = new()
        {
            message_ = message
        };

        return result;
    }

    /// <summary>
    /// Construct an instance which represents a success.
    /// </summary>
    /// <returns>Object representing a success.</returns>
    public static Result FromSuccess()
    {
        Result result = new()
        {
            message_ = null
        };

        return result;
    }

    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Success => message_ is null;
    
    /// <summary>
    /// Returns the text of the error.
    /// </summary>
    /// <exception cref="InvalidOperationException">If the result represents a success.</exception>
    public string Error => message_ ?? throw new InvalidOperationException("Value is valid.");
}

/// <summary>
/// Represents a result of an operation which returns <reftypeparam name="T"/> on success.
/// If it represents a failure, it contains a message describing the error.
/// </summary>
/// <typeparam name="T">Non-null type of the result of the operation.</typeparam>
public struct Result<T> where T : notnull
{
    string? message_;

    /// <summary>
    /// Construct an instance which represents failure.
    /// </summary>
    /// <param name="message">The text describing the error.</param>
    /// <returns>Object representing the error.</returns>
    public static Result<T> FromFailure(string message)
    {
        Result<T> result = new()
        {
            message_ = message
        };

        return result;
    }

    /// <summary>
    /// Construct an instance which represents a success.
    /// </summary>
    /// <param name="value">The value the successful operation returns.</param>
    /// <returns>Object representing a success.</returns>
    public static Result<T> FromSuccess(T value)
    {
        Result<T> result = new()
        {
            Value = value,
            message_ = null
        };

        return result;
    }

    /// <summary>
    /// Converts to successful result holding this value.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <returns>The converted value.</returns>
    public static implicit operator Result<T>(T value) => FromSuccess(value);

    /// <summary>
    /// Returns the value if the operation was successful, null otherwise.
    /// </summary>
    public T? Value { get; private init; }

    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Success => message_ is null;

    /// <summary>
    /// Returns the text of the error.
    /// </summary>
    /// <exception cref="InvalidOperationException">If the result represents a success.</exception>
    public string Error => message_ ?? throw new InvalidOperationException("Value is valid");
}
