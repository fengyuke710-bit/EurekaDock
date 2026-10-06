using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyCompany("EurekaOrette")]
[assembly: System.Reflection.AssemblyCopyright("Copyright © 2026 EurekaOrette")]

internal static class PortableLauncher
{
    private static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            result.Append(c);
            slashes = 0;
        }
        result.Append('\\', slashes * 2);
        result.Append('"');
        return result.ToString();
    }

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string app = Path.Combine(root, "app", "EurekaDock.exe");
            if (!File.Exists(app))
            {
                MessageBox.Show("EurekaDock program files are missing. Please extract the complete package and try again.", "EurekaDock", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var start = new ProcessStartInfo(app);
            start.WorkingDirectory = root;
            start.UseShellExecute = false;
            Environment.SetEnvironmentVariable("DOTNET_ROOT", root);
            Environment.SetEnvironmentVariable("DOTNET_ROOT_X64", root);
            Environment.SetEnvironmentVariable("DOTNET_MULTILEVEL_LOOKUP", "0");
            Environment.SetEnvironmentVariable("EUREKADOCK_LAUNCHER", Path.Combine(root, "EurekaDock.exe"));
            var commandLine = new StringBuilder();
            foreach (string arg in args)
            {
                if (commandLine.Length != 0) commandLine.Append(' ');
                commandLine.Append(Quote(arg));
            }
            start.Arguments = commandLine.ToString();
            Process.Start(start);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "EurekaDock could not start", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
