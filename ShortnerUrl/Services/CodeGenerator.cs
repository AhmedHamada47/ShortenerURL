using System.Security.Cryptography;

namespace ShortnerUrl.Services
{
    public static class CodeGenerator
    {
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const int CodeLength = 7;
        private const int MaxAlphabetMultiple = 248; // largest multiple of 62 <= 256

        public static string GenerateCryptographicCode()
        {
            var chars = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++)
            {
                byte b;
                do
                {
                    b = RandomNumberGenerator.GetBytes(1)[0];
                } while (b >= MaxAlphabetMultiple);

                chars[i] = Alphabet[b % Alphabet.Length];
            }
            return new string(chars);
        }
    }
}
