using System;
using System.Threading;
using System.Windows.Forms;

namespace BongoPlusInstaller;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(false, @"Local\BongoPlus.PortableInstaller");
        bool acquired;
        try { acquired = mutex.WaitOne(args.Length == 2 ? 5000 : 0); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) { MessageBox.Show("BongoPlus is already open.", "BongoPlus", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerWindow(args.Length == 2 && args[0] == "--game" ? args[1] : null));
        }
        finally { mutex.ReleaseMutex(); }
    }
}
