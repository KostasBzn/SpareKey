using System.Security.Cryptography;
using System.Text;

namespace SpareKey.Core.Crypto
{
    /// <summary>
    /// The 24 recovery words - BIP39 English word list.
    ///
    /// The words are just a human friendly way to write down 32 random bytes:
    ///
    ///   32 random bytes  ==  24 words on paper
    ///
    /// Those 32 bytes are the secret for Box 2 that holdds the masterkey
    /// </summary>
    internal static class RecoveryWords
    {
        /* HOW 32 BYTES BECOME 24 WORDS
         * ------------------------------------------------------------
         * |         SECRET (32 bytes)          | CHECKSUM |
         * ------------------------------------------------------------
         *               256 bits                  8 bits
         *
         *   256 + 8 = 264 bits, cut into 24 pieces of 11 bits.
         *   11 bits = a number from 0 to 2047 = one word in the list.
         *
         *   CHECKSUM = the first byte of SHA-256(SECRET).
         *   One wrong word and the checksum won't matc
         */

        public const int WordCount = 24;
        public const int SecretSize = 32;

        private const int BitsPerWord = 11;
        private const int ListSize = 2048;

        // SHA-256 of the official BIP39 English list (words joined with "\n").
        // If the embedded file is ever damaged or edited, we refuse to use it.
        // A wrong list would print words that can't restore the vault later.
        private const string ExpectedListHash = "2f5eed53a4727b4bf8880d8f3f199efc90e58503646d9ff8eff3a2ed3b24dbda";

        private static readonly string[] wordList = LoadWordList();
        private static readonly Dictionary<string, int> wordIndex = BuildWordIndex(wordList);

        /// <summary>
        /// Makes 24 new random words. Used once, at vault setup.
        /// Turn them into the secret with WordsToBytes.
        /// </summary>
        public static string[] CreateNewWords()
        {
            byte[] secret = RandomNumberGenerator.GetBytes(SecretSize);
            try
            {
                return BytesToWords(secret);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secret);
            }
        }

        /// <summary>
        /// 32 bytes to 24 words.
        /// </summary>
        public static string[] BytesToWords(byte[] secret)
        {
            if (secret == null || secret.Length != SecretSize)
            {
                throw new ArgumentException("Secret must be " + SecretSize + " bytes.", nameof(secret));
            }

            string[] list = wordList;

            // Build one long row of bits. the 256 secret bits + 8 checksum bits
            byte checksum = SHA256.HashData(secret)[0];
            bool[] bits = new bool[WordCount * BitsPerWord];
            WriteBits(secret, bits, 0);
            WriteBits(new byte[] { checksum }, bits, SecretSize * 8);

            // Read it back 11 bits at a time, each 11-bit number is one word
            string[] words = new string[WordCount];
            for (int wordNumber = 0; wordNumber < WordCount; wordNumber++)
            {
                int index = 0;
                for (int bit = 0; bit < BitsPerWord; bit++)
                {
                    index = index * 2;
                    if (bits[wordNumber * BitsPerWord + bit])
                    {
                        index = index + 1;
                    }
                }
                words[wordNumber] = list[index];
            }

            return words;
        }

        /// <summary>
        /// 24 words to 32 bytes.
        /// if the count is wrong, a word is unknown, or the checksum fails.
        /// </summary>
        public static byte[] WordsToBytes(string[] words)
        {
            if (words == null || words.Length != WordCount)
            {
                int count = words == null ? 0 : words.Length;
                throw new FormatException("Expected " + WordCount + " words, got " + count + ".");
            }

            Dictionary<string, int> index = wordIndex;

            // Each word to its 11-bit number into the row of bits
            bool[] bits = new bool[WordCount * BitsPerWord];
            for (int wordNumber = 0; wordNumber < WordCount; wordNumber++)
            {
                string word = words[wordNumber].Trim().ToLowerInvariant();

                if (!index.ContainsKey(word))
                {
                    // Tell the user WHICH word, counting from 1 like on paper
                    throw new FormatException("Word " + (wordNumber + 1) + " (\"" + word + "\") is not in the word list.");
                }

                int wordValue = index[word];
                for (int bit = 0; bit < BitsPerWord; bit++)
                {
                    int shift = BitsPerWord - 1 - bit;
                    bits[wordNumber * BitsPerWord + bit] = ((wordValue >> shift) & 1) == 1;
                }
            }

            // First 256 bits = the secret, last 8 bits = the checksum
            byte[] secret = ReadBytes(bits, 0, SecretSize);
            byte checksumFromWords = ReadBytes(bits, SecretSize * 8, 1)[0];
            byte expectedChecksum = SHA256.HashData(secret)[0];

            if (checksumFromWords != expectedChecksum)
            {
                CryptographicOperations.ZeroMemory(secret);
                throw new FormatException("The words don't match their checksum. Check for a typo or a word in the wrong place.");
            }

            return secret;
        }

        /// <summary>
        /// Splits what the user typed or pasted into separate words.
        /// Accepts spaces, tabs, new lines, any mix.
        /// </summary>
        public static string[] SplitInput(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            char[] separators = new char[] { ' ', '\t', '\r', '\n' };
            return text.ToLowerInvariant().Split(separators, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// True if the word is in the list. Handy for the UI to mark a
        /// typo in red while the user is still typing.
        /// </summary>
        public static bool IsKnownWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                return false;
            }
            return wordIndex.ContainsKey(word.Trim().ToLowerInvariant());
        }

        // Copies the bits of each byte into the bool array, left bit first.
        private static void WriteBits(byte[] source, bool[] bits, int startBit)
        {
            for (int byteNumber = 0; byteNumber < source.Length; byteNumber++)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    int shift = 7 - bit;
                    bits[startBit + byteNumber * 8 + bit] = ((source[byteNumber] >> shift) & 1) == 1;
                }
            }
        }

        // The opposite of WriteBits: groups of 8 bits back into bytes.
        private static byte[] ReadBytes(bool[] bits, int startBit, int byteCount)
        {
            byte[] result = new byte[byteCount];
            for (int byteNumber = 0; byteNumber < byteCount; byteNumber++)
            {
                int value = 0;
                for (int bit = 0; bit < 8; bit++)
                {
                    value = value * 2;
                    if (bits[startBit + byteNumber * 8 + bit])
                    {
                        value = value + 1;
                    }
                }
                result[byteNumber] = (byte)value;
            }
            return result;
        }


        private static string[] LoadWordList()
        {
            // The whole word list file as one string 
            string fileText = Properties.Resources.english;

            // Splitting on any line ending means Windows (\r\n) and
            // Linux (\n) copies of the file both work.
            char[] lineBreaks = new char[] { '\r', '\n' };
            string[] words = fileText.Split(lineBreaks, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length != ListSize)
            {
                throw new InvalidOperationException("Word list has " + words.Length + " words, expected " + ListSize + ".");
            }

            // is this exactly the official list? we check the sha256
            string joined = string.Join("\n", words) + "\n";
            string actualHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined)));
            if (!string.Equals(actualHash, ExpectedListHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Word list is not the official BIP39 English list.");
            }

            return words;
        }

        // for fast lookups
        private static Dictionary<string, int> BuildWordIndex(string[] words)
        {
            Dictionary<string, int> index = new Dictionary<string, int>();
            for (int i = 0; i < words.Length; i++)
            {
                index.Add(words[i], i);
            }
            return index;
        }
    }
}