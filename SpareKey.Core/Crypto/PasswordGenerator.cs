using System.Security.Cryptography;

namespace SpareKey.Core.Crypto
{
    /// <summary>
    /// Makes random passwords for new entries.
    /// </summary>
    public static class PasswordGenerator
    {
        public const int DefaultLength = 20;
        public const int MinLength = 4;
        public const int MaxLength = 128;

        private const string Alphabet = "abcdefghijklmnopqrstuvwxyz";
        private const string Digits = "0123456789";
        private const string Symbols = "!@#$%^&*()-_=+[]{};:,.?/<>~";

        /// <summary>
        /// 20 characters with lowercase, uppercase, digits and symbols.
        /// </summary>
        public static string Generate()
        {
            return Generate(DefaultLength, true, true, true, true);
        }

        /// <summary>
        /// Makes a password with the chosen length and character types.
        /// It always contains at least one character of every chosen type,
        /// </summary>
        public static string Generate(int length, bool useLowercase, bool useUppercase, bool useDigits, bool useSymbols)
        {
            if (length < MinLength || length > MaxLength)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be between " + MinLength + " and " + MaxLength + ".");

            List<string> groups = new List<string>();
            if (useLowercase)
                groups.Add(Alphabet);

            if (useUppercase)
                groups.Add(Alphabet.ToUpper());

            if (useDigits)
                groups.Add(Digits);

            if (useSymbols)
                groups.Add(Symbols);

            if (groups.Count == 0)
                throw new ArgumentException("Choose at least one character type.");

            string allCharacters = string.Concat(groups);
            // One character from each chosen group
            string required = "";
            foreach (string group in groups)
                required += PickRandomCharacter(group);

            // we add the remaining characters of the length randomly
            char[] password = (required + RandomNumberGenerator.GetString(allCharacters, length - groups.Count)).ToCharArray();
            // suffle
            RandomNumberGenerator.Shuffle(password.AsSpan());
            string result = new string(password);
            // Wipe 
            Array.Clear(password);
            return result;
        }

        private static char PickRandomCharacter(string characters)
        {
            int index = RandomNumberGenerator.GetInt32(characters.Length);
            return characters[index];
        }
    }
}