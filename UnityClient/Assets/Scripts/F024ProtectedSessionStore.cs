using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public sealed class F024ProtectedSessionStore
{
    [StructLayout(LayoutKind.Sequential)]
    struct DataBlob
    {
        public int size;
        public IntPtr data;
    }

    const int ProtectUiForbidden = 0x1;
    static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HearthstoneClone.AccountSession.v1");
    readonly string path;

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptProtectData(ref DataBlob input, string description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr memory);

    public F024ProtectedSessionStore(string path)
    {
        this.path = path;
    }

    public bool TryLoad(out string token)
    {
        token = null;
        if (!IsWindows() || String.IsNullOrEmpty(path) || !File.Exists(path)) return false;
        byte[] encrypted = null;
        byte[] clear = null;
        try
        {
            encrypted = File.ReadAllBytes(path);
            if (!TryTransform(encrypted, false, out clear)) return false;
            token = Encoding.UTF8.GetString(clear);
            return !String.IsNullOrEmpty(token);
        }
        catch { token = null; return false; }
        finally
        {
            Clear(encrypted);
            Clear(clear);
        }
    }

    public bool TrySave(string token)
    {
        if (!IsWindows() || String.IsNullOrEmpty(path) || String.IsNullOrEmpty(token)) return false;
        byte[] clear = Encoding.UTF8.GetBytes(token);
        byte[] encrypted = null;
        try
        {
            if (!TryTransform(clear, true, out encrypted)) return false;
            string directory = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, encrypted);
            return true;
        }
        catch { return false; }
        finally
        {
            Clear(clear);
            Clear(encrypted);
        }
    }

    public void Clear()
    {
        if (!String.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
    }

    static bool TryTransform(byte[] input, bool protect, out byte[] output)
    {
        output = null;
        if (input == null || input.Length == 0) return false;
        IntPtr inputMemory = IntPtr.Zero;
        IntPtr entropyMemory = IntPtr.Zero;
        IntPtr entropyBlobMemory = IntPtr.Zero;
        IntPtr outputMemory = IntPtr.Zero;
        try
        {
            inputMemory = Marshal.AllocHGlobal(input.Length);
            Marshal.Copy(input, 0, inputMemory, input.Length);
            var inputBlob = new DataBlob { size = input.Length, data = inputMemory };
            entropyMemory = Marshal.AllocHGlobal(Entropy.Length);
            Marshal.Copy(Entropy, 0, entropyMemory, Entropy.Length);
            var entropyBlob = new DataBlob { size = Entropy.Length, data = entropyMemory };
            entropyBlobMemory = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DataBlob)));
            Marshal.StructureToPtr(entropyBlob, entropyBlobMemory, false);
            DataBlob result;
            bool ok = protect
                ? CryptProtectData(ref inputBlob, "HearthstoneClone account session", entropyBlobMemory,
                    IntPtr.Zero, IntPtr.Zero, ProtectUiForbidden, out result)
                : CryptUnprotectData(ref inputBlob, IntPtr.Zero, entropyBlobMemory,
                    IntPtr.Zero, IntPtr.Zero, ProtectUiForbidden, out result);
            if (!ok || result.data == IntPtr.Zero || result.size <= 0) return false;
            outputMemory = result.data;
            output = new byte[result.size];
            Marshal.Copy(outputMemory, output, 0, result.size);
            return true;
        }
        catch { Clear(output); output = null; return false; }
        finally
        {
            if (outputMemory != IntPtr.Zero) LocalFree(outputMemory);
            if (entropyBlobMemory != IntPtr.Zero) Marshal.FreeHGlobal(entropyBlobMemory);
            if (entropyMemory != IntPtr.Zero) Marshal.FreeHGlobal(entropyMemory);
            if (inputMemory != IntPtr.Zero) Marshal.FreeHGlobal(inputMemory);
        }
    }

    static bool IsWindows()
    {
        return Environment.OSVersion.Platform == PlatformID.Win32NT;
    }

    static void Clear(byte[] bytes)
    {
        if (bytes != null) Array.Clear(bytes, 0, bytes.Length);
    }
}
