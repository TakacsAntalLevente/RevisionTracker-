using System;
using System.Reflection;
using System.Windows.Forms;
using RevisionTracker.Core;

[assembly: AssemblyTitle("RevisionTracker")]
[assembly: AssemblyProduct("RevisionTracker")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0")]

namespace RevisionTracker.App
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => ShowError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowError(e.ExceptionObject as Exception);

            var root = args.Length > 1 && args[0] == "--data" ? args[1] : Repository.DefaultRoot;
            Repository repo;
            try { repo = new Repository(root); }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Edition.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }
            using (repo)
                Application.Run(new MainForm(repo));
            return 0;
        }

        private static void ShowError(Exception ex)
        {
            MessageBox.Show("Something went wrong:\n\n" + (ex != null ? ex.Message : "unknown error") +
                            "\n\nYour saved versions are safe.", Edition.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
