using System.Text;

namespace BeniceSoft.Core.Strategy;

/// <summary>
/// encodes using vigenere cypher.
/// </summary>
public class VigenereEncoding : IEncoding<string>
{
    private readonly CaesarEncoding _caesarEncoder = new();

    /// <summary>
    /// encodes text using specified key,
    /// time complexity: O(n),
    /// space complexity: O(n),
    /// where n - text length.
    /// </summary>
    /// <param name="text">Text to be encoded.</param>
    /// <param name="key">PropertyName that will be used to encode the text.</param>
    /// <returns>Encoded text.</returns>
    public string Encode(string text, string key)
    {
        return Cipher(text, key, _caesarEncoder.Encode);
    }

    /// <summary>
    /// decodes text that was encoded using specified key,
    /// time complexity: O(n),
    /// space complexity: O(n),
    /// where n - text length.
    /// </summary>
    /// <param name="text">Text to be decoded.</param>
    /// <param name="key">PropertyName that was used to encode the text.</param>
    /// <returns>Decoded text.</returns>
    public string Decode(string text, string key)
    {
        return Cipher(text, key, _caesarEncoder.Decode);
    }

    private static string Cipher(string text, string key, Func<string, int, string> symbolCipher)
    {
        key = AppendKey(key, text.Length);
        var encodedTextBuilder = new StringBuilder(text.Length);
        foreach (var i in text.Length)
        {
            if (char.IsDigit(text[i]))
            {
                var digit = symbolCipher(text[i].ToString(), i);
                encodedTextBuilder.Append(digit);
                continue;
            }
            else if (!char.IsLetter(text[i]))
            {
                encodedTextBuilder.Append(text[i]);
                continue;
            }

            var letterZ = char.IsUpper(key[i]) ? 'Z' : 'z';

            var encodedSymbol = symbolCipher(text[i].ToString(), letterZ - key[i]);
            encodedTextBuilder.Append(encodedSymbol);
        }

        return encodedTextBuilder.ToString();
    }

    private static string AppendKey(string key, int length)
    {
        var keyBuilder = new StringBuilder(key, length);
        while (keyBuilder.Length < length)
        {
            keyBuilder.Append(key);
        }

        return keyBuilder.ToString();
    }
}
