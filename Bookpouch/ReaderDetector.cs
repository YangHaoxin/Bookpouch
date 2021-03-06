﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

//Trigger class UsbSync when connection of compatible USB storage device is detected
namespace Bookpouch
{
    static class ReaderDetector
    {        
        public static void HookDectection()
        {            
            MainWindow.MW.SourceInitialized += OnSourceInitialized;
        }

        public static void OnSourceInitialized(object sender, EventArgs e)
        {

            var windowHandle = (new WindowInteropHelper(MainWindow.MW)).Handle;
            var src = HwndSource.FromHwnd(windowHandle);

            if (src != null) 
                src.AddHook(WndProc);

        }

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);        

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {            
            if (msg != 0x219 && msg != 0xD903) //WM_DEVICECHANGE, postmessage msg - 55555
                return IntPtr.Zero;
            
            if (wParam.ToInt32() != 0x8000) //DBT_DEVICEARRIVAL
                return IntPtr.Zero;

            var devType = Marshal.ReadInt32(lParam, IntPtr.Size);

            if (devType != 0x00000002 && msg != 0xD903) //DBT_DEVTYP_VOLUME, postmessage msg - 55555
                return IntPtr.Zero;
                         
            if (msg != 0xD903) //If this is system message repost it as own message
            {                             
                PostMessage(hwnd, 55555, wParam, lParam);                            
            }
            else //If this is own postmessage start the usbsync class
            {
                DebugConsole.WriteLine("Autosync: USB storage device connection detected, triggering UsbSync");
                Debug.WriteLine("Triggering UsbSync");                
                UsbSync.Sync();
            }

            return IntPtr.Zero;
        }
    }
}
