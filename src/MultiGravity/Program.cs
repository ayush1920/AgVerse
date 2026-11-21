using System;
using System.Windows.Forms;

namespace MultiGravity
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            AntigravityCore.Initialize();

            // Headless launcher mode for Desktop Shortcuts
            // e.g. MultiGravity.exe --launch "ProfileName"
            if (args.Length >= 2 && (args[0].Equals("--launch", StringComparison.OrdinalIgnoreCase) ||
                                     args[0].Equals("-l", StringComparison.OrdinalIgnoreCase)))
            {
                string profileName = args[1].Trim();
                try
                {
                    AntigravityCore.LaunchProfile(profileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to launch profile: " + ex.Message, "MultiGravity Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            // Normal GUI mode
            try
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fatal MultiGravity Error:\n" + ex.ToString(), "MultiGravity Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
