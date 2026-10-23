using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace GripRehab
{
    // Windows DPAPI, CurrentUser scope. The optional entropy identifies this
    // application; it is not a password. No credential or encryption key is
    // stored in the project or shipped with the player.
    public static class WindowsProfileProtection
    {
        const uint UiForbidden = 0x1;
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("GripRehab|local-profile-catalog|v2");

        [StructLayout(LayoutKind.Sequential)]
        struct DataBlob
        {
            public int Length;
            public IntPtr Data;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CryptProtectData(ref DataBlob data, string description,
            ref DataBlob entropy, IntPtr reserved, IntPtr prompt, uint flags,
            out DataBlob protectedData);

        [DllImport("crypt32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CryptUnprotectData(ref DataBlob data, IntPtr description,
            ref DataBlob entropy, IntPtr reserved, IntPtr prompt, uint flags,
            out DataBlob clearData);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LocalFree(IntPtr memory);

        public static byte[] Protect(byte[] clear)
        {
            if (clear == null || clear.Length == 0) throw new ArgumentException("Empty profile catalog");
            CheckWindows();
            var input = Allocate(clear);
            var entropy = Allocate(Entropy);
            DataBlob output = default;
            try
            {
                if (!CryptProtectData(ref input, "GripRehab local profiles", ref entropy,
                    IntPtr.Zero, IntPtr.Zero, UiForbidden, out output))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "DPAPI protect failed");
                return Copy(output);
            }
            finally
            {
                ReleaseInput(input);
                ReleaseInput(entropy);
                if (output.Data != IntPtr.Zero) LocalFree(output.Data);
            }
        }

        public static byte[] Unprotect(byte[] encrypted)
        {
            if (encrypted == null || encrypted.Length == 0) throw new ArgumentException("Empty protected catalog");
            CheckWindows();
            var input = Allocate(encrypted);
            var entropy = Allocate(Entropy);
            DataBlob output = default;
            try
            {
                if (!CryptUnprotectData(ref input, IntPtr.Zero, ref entropy,
                    IntPtr.Zero, IntPtr.Zero, UiForbidden, out output))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "DPAPI unprotect failed");
                return Copy(output);
            }
            finally
            {
                ReleaseInput(input);
                ReleaseInput(entropy);
                if (output.Data != IntPtr.Zero) LocalFree(output.Data);
            }
        }

        static void CheckWindows()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                throw new PlatformNotSupportedException("Local patient storage requires Windows DPAPI");
        }

        static DataBlob Allocate(byte[] bytes)
        {
            var blob = new DataBlob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
            Marshal.Copy(bytes, 0, blob.Data, bytes.Length);
            return blob;
        }

        static byte[] Copy(DataBlob blob)
        {
            if (blob.Length < 0 || blob.Length > 16 * 1024 * 1024 || blob.Data == IntPtr.Zero)
                throw new InvalidOperationException("Invalid DPAPI payload size");
            var result = new byte[blob.Length];
            Marshal.Copy(blob.Data, result, 0, blob.Length);
            return result;
        }

        static void ReleaseInput(DataBlob blob)
        {
            if (blob.Data == IntPtr.Zero) return;
            for (var i = 0; i < blob.Length; i++) Marshal.WriteByte(blob.Data, i, 0);
            Marshal.FreeHGlobal(blob.Data);
        }
    }
}
