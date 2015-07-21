using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Diagnostics;
using System.Windows.Forms;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Drawing;

namespace serverApplication
{
    class Program
    {
        #region native methods
        // Declare the SetConsoleCtrlHandler function
        // as external and receiving a delegate.
        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        // A delegate type to be used as the handler routine
        // for SetConsoleCtrlHandler.
        public delegate bool HandlerRoutine(CtrlTypes CtrlType);
        #endregion

        #region enums
        // An enumerated type for the control messages
        // sent to the handler routine.
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }
        #endregion

        public static int Main(string[] args)
        {
            // Program Start
            SetConsoleCtrlHandler(new HandlerRoutine(ConsoleCtrlCheck), true);
            AsyncSocketListener.MoveMousePointerOutofBound(AsyncSocketListener.UPPER_RIGHT);
            System.Threading.Timer _timer = new System.Threading.Timer(TimerCallback, null, 0, 1000);
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            AsyncSocketListener.StartListening(); // Start Aynchronous Listener

            return 0;
        }

        private static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
        {
            // Put your own handler here
            switch (ctrlType)
            {
                case CtrlTypes.CTRL_C_EVENT:
                    AsyncSocketListener.isclosing = true;
                    Console.WriteLine("CTRL+C received!");
                    break;

                case CtrlTypes.CTRL_BREAK_EVENT:
                    AsyncSocketListener.isclosing = true;
                    Console.WriteLine("CTRL+BREAK received!");
                    break;

                case CtrlTypes.CTRL_CLOSE_EVENT:
                    AsyncSocketListener.isclosing = true;
                    CloseAllProcess();
                    Console.WriteLine("Program being closed!");
                    break;

                case CtrlTypes.CTRL_LOGOFF_EVENT:
                case CtrlTypes.CTRL_SHUTDOWN_EVENT:
                    CloseAllProcess();
                    AsyncSocketListener.isclosing = true;
                    Console.WriteLine("User is logging off!");
                    break;

            }
            return true;
        }

        private static void TimerCallback(Object o)
        {
            IntPtr handle = IntPtr.Zero; // Create a handle to manipulate the windows
            //Always put the CMS at back every 1 sec if ActiveWindow is ZONE
            if (AsyncSocketListener.ActiveWindow == 1)
            {
                try
                {
                    handle = AsyncSocketListener.proc[AsyncSocketListener.CMS].MainWindowHandle;
                    // Hide CMS window
                    AsyncSocketListener.SetWindowPos(handle, (IntPtr)AsyncSocketListener.HWND_NOTTOPMOST, 0, 0, 0, 0, AsyncSocketListener.SWP_NOMOVE | AsyncSocketListener.SWP_NOSIZE);
                    AsyncSocketListener.SetWindowPos(handle, (IntPtr)AsyncSocketListener.HWND_BOTTOM, 0, 0, 0, 0, AsyncSocketListener.SWP_NOSIZE | AsyncSocketListener.SWP_NOMOVE);
                    AsyncSocketListener.MoveMousePointerOutofBound(AsyncSocketListener.UPPER_RIGHT);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error : " + ex.Message);
                }
            }
            // Force a garbage collection to occur for this demo.
            GC.Collect();
        }

        static void CloseAllProcess()
        {
            // Show Taskbar
            AsyncSocketListener.ShowWindow(AsyncSocketListener.FindWindow("Shell_TrayWnd", ""), AsyncSocketListener.ShowWindowCommand.SW_SHOW);
            // Show Start Orb
            AsyncSocketListener.ShowWindow(AsyncSocketListener.FindWindow("Button", "Start"), AsyncSocketListener.ShowWindowCommand.SW_SHOW);

            // Close sockets
            try
            {
                AsyncSocketListener.state.workSocket.Shutdown(SocketShutdown.Both);
                AsyncSocketListener.state.workSocket.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            try
            {
                AsyncSocketListener.proc[AsyncSocketListener.CMS].Kill();
            }
            catch (Exception e)
            {
                
            }

            try
            {
                AsyncSocketListener.proc[AsyncSocketListener.ZONE].CloseMainWindow();
            }
            catch (Exception e)
            {
                
            }

            try
            {
                if (AsyncSocketListener.splashShown)
                    AsyncSocketListener.proc[AsyncSocketListener.SPLASH].Kill();
            }
            catch (Exception e)
            {
                
            }

            try
            {
                AsyncSocketListener.KillApps();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        static void OnProcessExit(object sender, EventArgs e)
        {
            try
            {
                Console.WriteLine("Closing sockets..");
                AsyncSocketListener.state.workSocket.Shutdown(SocketShutdown.Both);
                AsyncSocketListener.state.workSocket.Close();
                AsyncSocketListener.muteApp(AsyncSocketListener.CMS, false);
                CloseAllProcess();
            }
            catch (Exception err)
            {
                Console.WriteLine(err.ToString());
            }
            //Close all Process
        }
    }
}
