namespace BeniceSoft.Core.Strategy;

/// <summary>
/// encodes and decodes text based on specified key.
/// </summary>
/// <typeparam name="TKey">Type of the key.</typeparam>
public interface IEncoding<TKey>
{
    /// <summary>
    /// encodes text using specified key.
    /// </summary>
    /// <param name="text">Text to be encoded.</param>
    /// <param name="key">PropertyName that will be used to encode the text.</param>
    /// <returns>Encoded text.</returns>
    string Encode(string text, TKey key);

    /// <summary>
    /// decodes text that was encoded using specified key.
    /// </summary>
    /// <param name="text">Text to be decoded.</param>
    /// <param name="key">PropertyName that was used to encode the text.</param>
    /// <returns>Decoded text.</returns>
    string Decode(string text, TKey key);
}
