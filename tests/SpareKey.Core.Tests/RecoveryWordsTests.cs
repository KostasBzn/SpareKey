using System;
using System.Security.Cryptography;
using Xunit;
using SpareKey.Core.Crypto;

namespace SpareKey.Core.Tests.Crypto
{
    /// <summary>
    /// Step 2a: proves our 24 words follow the BIP39 standard exactly.
    /// If these fail, words written on paper today might not restore the vault
    /// in a future version.
    /// </summary>
    public class RecoveryWordsTests
    {
        // Official BIP39 test vectors (256-bit entries) from:
        // https://github.com/trezor/python-mnemonic/blob/master/vectors.json
        [Theory]
        [InlineData("0000000000000000000000000000000000000000000000000000000000000000",
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon art")]
        [InlineData("7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f",
            "legal winner thank year wave sausage worth useful legal winner thank year wave sausage worth useful legal winner thank year wave sausage worth title")]
        [InlineData("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
            "zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo vote")]
        public void BytesToWords_MatchesOfficialTestVectors(string secretHex, string expectedWords)
        {
            byte[] secret = Convert.FromHexString(secretHex);

            string[] words = RecoveryWords.BytesToWords(secret);

            Assert.Equal(expectedWords, string.Join(" ", words));
        }

        [Fact]
        public void WordsToBytes_GivesBackTheSameBytes()
        {
            byte[] secret = RandomNumberGenerator.GetBytes(32);

            string[] words = RecoveryWords.BytesToWords(secret);
            byte[] result = RecoveryWords.WordsToBytes(words);

            Assert.Equal(secret, result);
        }
    }
}
