using System;
using System.Security.Cryptography;

namespace GripRehab
{
    [Serializable]
    public sealed class LocalPasswordRecord
    {
        public int iterations;
        public string saltBase64;
        public string hashBase64;
    }

    // Password verifier only. Records belong inside the DPAPI-protected local
    // catalog; a successful password check is not authorization for a role.
    public static class LocalPasswordHash
    {
        public const int CurrentIterations = 210000;

        public static LocalPasswordRecord Create(string password)
        {
            CheckPassword(password);
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            var hash = Derive(password, salt, CurrentIterations);
            return new LocalPasswordRecord
            {
                iterations = CurrentIterations,
                saltBase64 = Convert.ToBase64String(salt),
                hashBase64 = Convert.ToBase64String(hash)
            };
        }

        public static bool Verify(string password, LocalPasswordRecord record)
        {
            if (record == null || password == null || password.Length > 128 ||
                record.iterations < 100000 || record.iterations > 1000000) return false;
            try
            {
                var salt = Convert.FromBase64String(record.saltBase64);
                var expected = Convert.FromBase64String(record.hashBase64);
                if (salt.Length != 16 || expected.Length != 32) return false;
                var actual = Derive(password, salt, record.iterations);
                var difference = 0;
                for (var i = 0; i < expected.Length; i++) difference |= expected[i] ^ actual[i];
                Array.Clear(actual, 0, actual.Length);
                return difference == 0;
            }
            catch (FormatException) { return false; }
            catch (ArgumentNullException) { return false; }
        }

        static byte[] Derive(string password, byte[] salt, int iterations)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                return kdf.GetBytes(32);
        }

        static void CheckPassword(string password)
        {
            if (password == null || password.Length < 12 || password.Length > 128)
                throw new ArgumentException("Password must contain 12–128 characters");
        }
    }
}
