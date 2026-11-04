using System.Diagnostics;
using System.Runtime.InteropServices;

public static class NativeWindow
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    static extern bool SetWindowText(System.IntPtr handle,string text);
    public static void SetTitle(string title)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        var handle=Process.GetCurrentProcess().MainWindowHandle;
        if(handle!=System.IntPtr.Zero)SetWindowText(handle,title);
#endif
    }
}
