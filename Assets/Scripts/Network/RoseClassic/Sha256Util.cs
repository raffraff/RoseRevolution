using System;
using System.Security.Cryptography;
using System.Text;

namespace RoseClassic
{
    public static class Sha256Util
    {
        public static string HashHex(string input)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
                builder.Append(b.ToString("x2"));

            return builder.ToString();
        }
    }
}
