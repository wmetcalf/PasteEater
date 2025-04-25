using System;
using System.Windows.Forms;
using System.Threading;

namespace PasteEater
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Enable visual styles while using Application.Run with a hidden form
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Create mutex to ensure only one instance runs
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "PateEaterInstance", out createdNew))
            {
                if (createdNew)
                {
                    // Create the hidden form that will process messages
                    MainForm form = new MainForm();
                    
                    // Run the application with the hidden form
                    Application.Run(form);
                }
                else
                {
                    // Application already running
                    MessageBox.Show("PasteEater is already running.", 
                                   "Already Running", 
                                   MessageBoxButtons.OK, 
                                   MessageBoxIcon.Information);
                }
            }
        }
    }
}
