using System.Security.Cryptography;

namespace ShortnerUrl.Services
{
    public static class CodeGenerator
    {
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const int CodeLength = 7;

        public static string GenerateCryptographicCode()
        {
            var bytes = RandomNumberGenerator.GetBytes(CodeLength);
            var chars = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++)
            {
                chars[i] = Alphabet[bytes[i] % Alphabet.Length];
            }
            return new string(chars);
        }

        public static string EncodeBase62(long id)
        {
            const string base62 = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            if (id == 0) return base62[0].ToString();

            var chars = new List<char>();
            while (id > 0)
            {
                chars.Add(base62[(int)(id % 62)]);
                id /= 62;
            }
            chars.Reverse();
            return new string(chars.ToArray());
        }
    }
}
