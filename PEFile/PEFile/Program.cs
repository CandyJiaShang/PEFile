using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace PEFile
{
    class Program
    {
        [DllImport("kernel32.dll")]
        public static extern Boolean AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern Boolean FreeConsole();

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("Kernel32.dll")]
        public static extern int GetProcessId(IntPtr hHandle);

        [DllImport("User32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hHandle, out int pProssId);

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            else
            {
                string file = args[0];
                string output = args[1];

                AttachConsole(-1);                
                Console.WriteLine("");  //写一个空行
                Console.WriteLine("hello");


                if (File.Exists(file))
                {
                    PEFile peFile = new PEFile(file);
                    peFile.Export(output);
                }
            }
        }
    }
}
