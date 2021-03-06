﻿using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Bookpouch
{
    /// <summary>
    /// Console window to display debugging messages from the application
    /// </summary>
    static class DebugConsole
    {
        [DllImport("Kernel32")]
        private static extern bool AllocConsole();

        [DllImport("Kernel32")]
        private static extern bool FreeConsole();

        [DllImport("Kernel32")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("Kernel32")]
        private static extern int GetConsoleOutputCP();

        public static bool HasConsole
        {
            get { return GetConsoleWindow() != IntPtr.Zero; }
        }

        static public void Open()
        {
            if (HasConsole) 
                return;

            AllocConsole();
            Console.WriteLine("Debug console is running and is ready to show output.\nClosing this window will exit the entire application.");
        }

        static public void WriteLine(string text)
        {
            if (!HasConsole)
                return;

            var time = DateTime.Now;

            Console.WriteLine("[" + time + "] " + text);            

            using (var sw = new StreamWriter("Debug_log.txt", true))       
                sw.Write("[" + time + "] " + text + "\n");               
        }
    }
}
