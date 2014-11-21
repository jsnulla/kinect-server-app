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
using System.Runtime.InteropServices;

namespace serverApplication
{
    class Program
    {
        private static string[] configPaths = new string[9];
        public static uint pID;
        const short CMS = 1;
        const short ZONE = 2;

        // Set Window Position
        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);
        // Window Position Flags
        const short SWP_NOMOVE = 0X2;
        const short SWP_NOSIZE = 1;
        const short SWP_NOZORDER = 0X4;
        const int SWP_SHOWWINDOW = 0x0040;

        // sound
        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string strClassName, string strWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        //

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", EntryPoint = "PostMessageA")]
        static extern bool PostMessage(IntPtr hWnd, uint msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        static extern byte VkKeyScan(char ch);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool ShowWindow(IntPtr handle, ShowWindowCommand command);

        const uint WM_KEYDOWN = 0x100;

        private enum ShowWindowCommand : int
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_NORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMAXIMIZED = 3,
            SW_MAXIMIZE = 3,
            SW_SHOWNOACTIVATE = 4,
            SW_SHOW = 5,
            SW_MINIMIZE = 6,
            SW_SHOWMINNOACTIVE = 7,
            SW_SHOWNA = 8,
            SW_RESTORE = 9,
            SW_SHOWDEFAULT = 10,
            SW_FORCEMINIMIZE = 11,
            SW_MAX = 11,
        }

        public class StateObject
        {
            // Client Socket
            public Socket workSocket = null;
            // Size of receive buffer
            public const int BufferSize = 1024;
            // Receive buffer
            public byte[] buffer = new byte[BufferSize];
            // Received data string
            public StringBuilder sb = new StringBuilder();
        }

        public class AsyncSocketListener
        {
            public static ManualResetEvent allDone = new ManualResetEvent(false);
            public static Process[] proc = new Process[3];
            public static StateObject state;
            public static bool splashShown = false;
            public static bool appsStarted = false;

            public AsyncSocketListener()
            {
                // Construct
            }

            static void logMsg(string msg)
            {
                string timeNow = System.DateTime.Now.Hour.ToString() + ":";
                if (int.Parse(System.DateTime.Now.Minute.ToString()) < 10)
                    timeNow += ("0" + System.DateTime.Now.Minute.ToString());
                else
                    timeNow += System.DateTime.Now.Minute.ToString();
                timeNow += ":" + System.DateTime.Now.Second.ToString();

                Console.WriteLine(timeNow + "| " + msg);
            }

            public static bool readConfig() {
                logMsg("reading config...");
                string configPath = AppDomain.CurrentDomain.BaseDirectory.ToString() + @"config_file.ini";

                try
                {
                    if (!File.Exists(configPath)) // If file does not exist
                    {
                        logMsg("config file not found");
                        using (StreamWriter sw = File.CreateText(configPath))
                        {
                            logMsg("creating config");
                            sw.WriteLine("CMS path here");
                            sw.WriteLine("CMS class name here");
                            sw.WriteLine("CMS display name here");
                            sw.WriteLine("Zone path here");
                            sw.WriteLine("Zone class name here (UnityWndClass)");
                            sw.WriteLine("Zone display name here");
                            sw.WriteLine("Splash path here");
                            sw.WriteLine("Splash class name here (UnityWndClass)");
                            sw.WriteLine("Splash display name here");
                        }
                        logMsg("config created");
                        readConfig();
                    }
                    else
                    {
                        logMsg("config found");
                        using (StreamReader sr = File.OpenText(configPath))
                        {
                            logMsg("reading config");
                            int lineNumber = 0;
                            string s = "";
                            while ((s = sr.ReadLine()) != null)
                            {
                                configPaths[lineNumber] = s;
                                lineNumber++;
                            }
                        }
                        Console.Clear();
                        logMsg("config read");
                        return true;
                    }
                }
                catch (Exception e)
                {
                    logMsg("config read error: " + e.ToString());
                    return false;
                }
                return true;
            }

            public static void StartApps()
            {
                logMsg("Starting apps...");
                proc[ZONE] = Process.Start(@"" + configPaths[3]); // Start Zone
                appsStarted = true;
            }

            public static void KillApps()
            {
                proc[CMS].CloseMainWindow(); // Kill CMS
                proc[ZONE].CloseMainWindow(); // Kill Zone
                appsStarted = false;
            }

            public static void StartListening()
            {
                logMsg("started");
                while (readConfig() == false) {
                    // Keep reading config file
                }
                AsyncSocketListener.StartApps();

                // Data buffer for incoming data
                byte[] bytes = new Byte[1024];

                // Create a TCP/IP socket
                Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    sock.Bind(new IPEndPoint(0, 1234));
                    sock.Listen(100);

                    while (true)
                    {
                        // Set the event to nonsignaled state.
                        allDone.Reset();

                        // Start async socket to listen for connections
                        sock.BeginAccept(new AsyncCallback(AcceptCallback), sock);

                        // Wait until a connection is made before continuing
                        allDone.WaitOne();
                        logMsg("Connection successful");
                    }
                }
                catch (Exception e)
                {
                    // Error
                    logMsg(e.ToString());
                }
            }

            public static void AcceptCallback(IAsyncResult asyncResult)
            {
                // Signal main thread to continue
                allDone.Set();

                // Get the socket that handles the client request
                Socket listener = (Socket) asyncResult.AsyncState;
                Socket handler = listener.EndAccept(asyncResult);

                // Create the state object
                state = new StateObject();
                state.workSocket = handler;
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
            }

            public static void ReadCallback(IAsyncResult asyncResult)
            {
                String content = String.Empty;

                // Retreive the state object and the handler socket
                // from the asynchronous state object
                StateObject state = (StateObject) asyncResult.AsyncState;
                Socket handler = state.workSocket;

                // Read data from the client socket
                int bytesRead = 0;
                try
                {
                    bytesRead = handler.EndReceive(asyncResult);
                }
                catch (Exception e)
                {
                    logMsg(e.ToString());
                    logMsg("Connection was forcibly closed");
                }

                if (bytesRead > 0)
                {
                    state.sb.Clear();
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    // Check for end-of-file tag. If it is not here, read more data
                    content = state.sb.ToString();

                    switch (content)
                    {
                        case "showSplash":
                            if (splashShown == false) // Show splash screen because a player is detected
                            {
                                logMsg("starting splash screen");
                                splashShown = true;
                                proc[0] = Process.Start(@"" + configPaths[6]); // Start splash screen
                            }
                            break;

                        case "killSplash":
                            if (splashShown == true) // Kill splash screen since player is not detected or the player quit
                            {
                                logMsg("killing splash screen");
                                proc[0].CloseMainWindow();
                            }
                            splashShown = false;
                            break;

                        case "startCMS":
                            if(proc[CMS] == null)
                                proc[CMS] = Process.Start(@"" + configPaths[0]); // Start CMS
                            break;

                        case "muteCMS":
                            muteApp(CMS, true); // Mute CMS
                            break;

                        case "showCMS":
                            muteApp(CMS, false);
                            muteApp(ZONE, true);
                            showApp(CMS);
                            //ShowWindow(FindWindow(configPaths[1], null), ShowWindowCommand.SW_SHOW);
                            //ShowWindow(FindWindow(configPaths[1], null), ShowWindowCommand.SW_MAXIMIZE);
                            //ShowWindow(FindWindow(configPaths[4], null), ShowWindowCommand.SW_FORCEMINIMIZE);
                            logMsg("showing CMS");
                            break;

                        case "showZone":
                            muteApp(CMS, true);
                            muteApp(ZONE, false);
                            showApp(ZONE);
                            //ShowWindow(FindWindow(configPaths[1], null), ShowWindowCommand.SW_FORCEMINIMIZE);
                            //ShowWindow(FindWindow(configPaths[1], null), ShowWindowCommand.SW_HIDE);
                            //ShowWindow(FindWindow(configPaths[4], null), ShowWindowCommand.SW_MAXIMIZE);
                            logMsg("showing ZONE");
                            break;

                        case "startApps": // Unity test
                            if (appsStarted == false)
                            {
                                AsyncSocketListener.StartApps();
                            }
                            break;

                        case "killApps": // Unity test
                            if (appsStarted == true)
                            {
                                AsyncSocketListener.KillApps();
                            }
                            break;

                        default:
                            logMsg(content);
                            break;
                    }
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                }
            }

            public static void Send(Socket handler, String data)
            {
                // Convert the string data to byte data using ASCII encoding
                byte[] byteData = Encoding.ASCII.GetBytes(data);

                // Begin sending the data the remote device
                handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }

            public static void SendCallback(IAsyncResult asyncResult)
            {
                try
                {
                    // Retrieve the socket from the state object
                    Socket handler = (Socket) asyncResult.AsyncState;

                    // Complete sending the data to the remote device
                    int bytesSent = handler.EndSend(asyncResult);

                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                }
                catch (Exception e)
                {
                    logMsg(e.ToString());
                }
            }

            public static void muteApp(int procNum, bool _muteApp)
            {
                string appClass = null; // Process Class Name
                string appDisplayName = null; // Process Display Name
                switch (procNum)
                {
                    case CMS:
                        appClass = configPaths[1];
                        appDisplayName = null;
                        break;

                    case ZONE:
                        appClass = configPaths[4];
                        appDisplayName = null;
                        break;
                }

                var hWnd = FindWindow(appClass, appDisplayName);
                if (hWnd == IntPtr.Zero)
                    return;

                GetWindowThreadProcessId(hWnd, out pID);
                if (pID == 0)
                    return;
                VolumeMixer.SetApplicationMute(pID, _muteApp);
            }

            public static void showApp(int procNum)
            {
                IntPtr handle; // Create a handle to manipulate the windows
                try
                {
                    handle = proc[procNum].MainWindowHandle;
                    if (handle != IntPtr.Zero)
                        SetWindowPos(handle, 0, 0, 0, Screen.PrimaryScreen.WorkingArea.Width,
                            Screen.PrimaryScreen.WorkingArea.Height, SWP_NOZORDER | SWP_SHOWWINDOW);

                    switch (procNum)
                    {
                        case CMS:
                            handle = proc[ZONE].MainWindowHandle;
                            ShowWindow(FindWindow(configPaths[1], null), ShowWindowCommand.SW_MAXIMIZE); // Maximize CMS
                            break;

                        case ZONE:
                            handle = proc[CMS].MainWindowHandle;
                            ShowWindow(FindWindow(configPaths[4], null), ShowWindowCommand.SW_MAXIMIZE); // Maximize Zone
                            break;
                    }

                    if (handle != IntPtr.Zero)
                        SetWindowPos(handle, 1, 0, 0, 0, 0, SWP_NOSIZE);
                }
                catch(Exception e)
                {
                    logMsg(e.ToString());
                }
                

                //ShowWindow(FindWindow(configPaths[1], null), ShowWindowCommand.SW_MAXIMIZE);
                //ShowWindow(FindWindow(configPaths[4], null), ShowWindowCommand.SW_FORCEMINIMIZE);
            }

            static void OnProcessExit(object sender, EventArgs e)
            {
                logMsg("Closing sockets..");
                state.workSocket.Shutdown(SocketShutdown.Both);
                state.workSocket.Close();
            }

            public static int Main(string[] args)
            {
                // Program Start
                AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
                AsyncSocketListener.StartListening(); // Start Aynchronous Listener
                return 0;
            }
        }

        public class VolumeMixer
        {
            public static float? GetApplicationVolume(uint pid)
            {
                ISimpleAudioVolume volume = GetVolumeObject(pid);
                if (volume == null)
                    return null;

                float level;
                volume.GetMasterVolume(out level);
                Marshal.ReleaseComObject(volume);
                return level * 100;
            }

            public static bool? GetApplicationMute(uint pid)
            {
                ISimpleAudioVolume volume = GetVolumeObject(pid);
                if (volume == null)
                    return null;

                bool mute;
                volume.GetMute(out mute);
                Marshal.ReleaseComObject(volume);
                return mute;
            }

            public static void SetApplicationVolume(uint pid, float level)
            {
                ISimpleAudioVolume volume = GetVolumeObject(pid);
                if (volume == null)
                    return;

                Guid guid = Guid.Empty;
                volume.SetMasterVolume(level / 100, ref guid);
                Marshal.ReleaseComObject(volume);
            }

            public static void SetApplicationMute(uint pid, bool mute)
            {
                ISimpleAudioVolume volume = GetVolumeObject(pid);
                if (volume == null)
                    return;

                Guid guid = Guid.Empty;
                volume.SetMute(mute, ref guid);
                Marshal.ReleaseComObject(volume);
            }

            private static ISimpleAudioVolume GetVolumeObject(uint pid)
            {
                // get the speakers (1st render + multimedia) device
                IMMDeviceEnumerator deviceEnumerator = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
                IMMDevice speakers;
                deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out speakers);

                // activate the session manager. we need the enumerator
                Guid IID_IAudioSessionManager2 = typeof(IAudioSessionManager2).GUID;
                object o;
                speakers.Activate(ref IID_IAudioSessionManager2, 0, IntPtr.Zero, out o);
                IAudioSessionManager2 mgr = (IAudioSessionManager2)o;

                // enumerate sessions for on this device
                IAudioSessionEnumerator sessionEnumerator;
                mgr.GetSessionEnumerator(out sessionEnumerator);
                uint count;
                sessionEnumerator.GetCount(out count);

                // search for an audio session with the required name
                // NOTE: we could also use the process id instead of the app name (with IAudioSessionControl2)
                ISimpleAudioVolume volumeControl = null;
                for (uint i = 0; i < count; i++)
                {
                    IAudioSessionControl2 ctl;
                    sessionEnumerator.GetSession(i, out ctl);
                    uint cpid;
                    ctl.GetProcessId(out cpid);

                    if (cpid == pid)
                    {
                        volumeControl = ctl as ISimpleAudioVolume;
                        break;
                    }
                    Marshal.ReleaseComObject(ctl);
                }
                Marshal.ReleaseComObject(sessionEnumerator);
                Marshal.ReleaseComObject(mgr);
                Marshal.ReleaseComObject(speakers);
                Marshal.ReleaseComObject(deviceEnumerator);
                return volumeControl;
            }
        }

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        internal class MMDeviceEnumerator
        {
        }

        internal enum EDataFlow
        {
            eRender,
            eCapture,
            eAll,
            EDataFlow_enum_count
        }

        internal enum ERole
        {
            eConsole,
            eMultimedia,
            eCommunications,
            ERole_enum_count
        }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMMDeviceEnumerator
        {
            uint NotImpl1();

            [PreserveSig]
            uint GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);

            // the rest is not implemented
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMMDevice
        {
            [PreserveSig]
            uint Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

            // the rest is not implemented
        }

        [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionManager2
        {
            uint NotImpl1();
            uint NotImpl2();

            [PreserveSig]
            uint GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);

            // the rest is not implemented
        }

        [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionEnumerator
        {
            [PreserveSig]
            uint GetCount(out uint SessionCount);

            [PreserveSig]
            uint GetSession(uint SessionCount, out IAudioSessionControl2 Session);
        }

        [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface ISimpleAudioVolume
        {
            [PreserveSig]
            uint SetMasterVolume(float fLevel, ref Guid EventContext);

            [PreserveSig]
            uint GetMasterVolume(out float pfLevel);

            [PreserveSig]
            uint SetMute(bool bMute, ref Guid EventContext);

            [PreserveSig]
            uint GetMute(out bool pbMute);
        }

        [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionControl2
        {
            // IAudioSessionControl
            [PreserveSig]
            uint NotImpl0();

            [PreserveSig]
            uint GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            uint SetDisplayName([MarshalAs(UnmanagedType.LPWStr)]string Value, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);

            [PreserveSig]
            uint GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            uint SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);

            [PreserveSig]
            uint GetGroupingParam(out Guid pRetVal);

            [PreserveSig]
            uint SetGroupingParam([MarshalAs(UnmanagedType.LPStruct)] Guid Override, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);

            [PreserveSig]
            uint NotImpl1();

            [PreserveSig]
            uint NotImpl2();

            // IAudioSessionControl2
            [PreserveSig]
            uint GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            uint GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            uint GetProcessId(out uint pRetVal);

            [PreserveSig]
            uint IsSystemSoundsSession();

            [PreserveSig]
            uint SetDuckingPreference(bool optOut);
        }
    }
}
