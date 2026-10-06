using System;
using System.Security.Cryptography;

namespace GripRehab.Product
{
    /// <summary>Salted PBKDF2-SHA256; records never contain plaintext passwords.</summary>
    public static class PasswordKdf
    {
        public const int Iterations = 210000;
        public static PasswordVerifier Create(string password)
        {
            if (password == null || password.Length < 12 || password.Length > 128)
                throw new ArgumentException("Use a password of 12–128 characters.");
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            var hash = Derive(password, salt, Iterations);
            try { return new PasswordVerifier { iterations = Iterations,
                saltBase64 = Convert.ToBase64String(salt), hashBase64 = Convert.ToBase64String(hash) }; }
            finally { Array.Clear(hash, 0, hash.Length); }
        }
        public static bool Verify(string password, PasswordVerifier record)
        {
            if (password == null || password.Length > 128 || record == null ||
                record.iterations < 100000 || record.iterations > 1000000) return false;
            byte[] actual = null;
            try
            {
                var salt = Convert.FromBase64String(record.saltBase64);
                var expected = Convert.FromBase64String(record.hashBase64);
                if (salt.Length != 16 || expected.Length != 32) return false;
                actual = Derive(password, salt, record.iterations); int difference = 0;
                for (int i = 0; i < expected.Length; i++) difference |= expected[i] ^ actual[i];
                return difference == 0;
            }
            catch (FormatException) { return false; }
            catch (ArgumentNullException) { return false; }
            finally { if (actual != null) Array.Clear(actual, 0, actual.Length); }
        }
        static byte[] Derive(string password, byte[] salt, int iterations)
        { using (var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256)) return kdf.GetBytes(32); }
    }
}
