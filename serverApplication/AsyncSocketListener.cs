using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace serverApplication
{
    public class AsyncSocketListener
    {
        #region config variables

        const string StatisticsFileName = "Statistics.xml";
        const string ConfigeFileName = "ServerConfig.xml";

        static string VCastPath = String.Empty;
        static string VCastWindowName = String.Empty;
        static string VCastClassName = String.Empty;
        static bool VCastUseClass = false;

        static string GameFileName = String.Empty;
        static string GameWindowName = String.Empty;
        static string GameClassName = String.Empty;
        static bool GameUseClass = false;

        static string SplashScreenPath = String.Empty;
        static string SplashScreenWindowName = String.Empty;
        static string SplashScreenClassName = String.Empty;
        static bool SplashScreenUseClass = false;

        #endregion

        #region variables
        private static string[] configPaths = new string[9];
        public static uint pID;
        public static bool isclosing = false;
        public static bool cameraDisabled = false;
        const string deviceToDisable = "Kinect for Windows Camera";

        public const short
        SPLASH = 0,
        CMS = 1,
        ZONE = 2;

        public const short
        UPPER_LEFT = 0,
        UPPER_RIGHT = 1;

        public static ManualResetEvent allDone = new ManualResetEvent(false);
        public static Process[] proc = new Process[3];
        public static StateObject state;
        public static bool splashShown = false;
        public static bool appsStarted = false;
        public static string zoneName = "";
        public const string zoneClassName = "UnityWndClass";
        #endregion

        #region native methods
        public static int ActiveWindow = 0;
        // Set Window Position
        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

        // Get Current Top Window
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenThread(int dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll")]
        static extern uint SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll")]
        static extern int ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string strClassName, string strWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", EntryPoint = "PostMessageA")]
        static extern bool PostMessage(IntPtr hWnd, uint msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        static extern byte VkKeyScan(char ch);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ShowWindow(IntPtr handle, ShowWindowCommand command);

        #endregion

        #region Flags
        // Window Z Order
        public const int
        HWND_TOP = 0,
        HWND_BOTTOM = 1,
        HWND_TOPMOST = -1,
        HWND_NOTTOPMOST = -2;

        // Window Position Flags
        public const int
        SWP_NOSIZE = 0x0001,
        SWP_NOMOVE = 0x0002,
        SWP_NOZORDER = 0x0004,
        SWP_NOREDRAW = 0x0008,
        SWP_NOACTIVATE = 0x0010,
        SWP_DRAWFRAME = 0x0020,
        SWP_FRAMECHANGED = 0x0020,
        SWP_SHOWWINDOW = 0x0040,
        SWP_HIDEWINDOW = 0x0080,
        SWP_NOCOPYBITS = 0x0100,
        SWP_NOOWNERZORDER = 0x0200,
        SWP_NOREPOSITION = 0x0200,
        SWP_NOSENDCHANGING = 0x0400,
        SWP_DEFERERASE = 0x2000,
        SWP_ASYNCWINDOWPOS = 0x4000;

        // Thread Acess
        public const int
        THREAD_TERMINATE = (0x0001),
        THREAD_SUSPEND_RESUME = (0x0002),
        THREAD_GET_CONTEXT = (0x0008),
        THREAD_SET_CONTEXT = (0x0010),
        THREAD_SET_INFORMATION = (0x0020),
        THREAD_QUERY_INFORMATION = (0x0040),
        THREAD_SET_THREAD_TOKEN = (0x0080),
        THREAD_IMPERSONATE = (0x0100),
        THREAD_DIRECT_IMPERSONATION = (0x0200);

        public const uint WM_KEYDOWN = 0x100;
        #endregion

        #region enums
        public enum ShowWindowCommand : int
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
        #endregion

        public AsyncSocketListener() { }

        static void logMsg(string msg)
        {
            string timeNow = System.DateTime.Now.Hour.ToString() + ":";

            if (int.Parse(System.DateTime.Now.Minute.ToString()) < 10)
                timeNow += ("0" + System.DateTime.Now.Minute.ToString());
            else
                timeNow += System.DateTime.Now.Minute.ToString();

            timeNow += ":" + System.DateTime.Now.Second.ToString();

            Console.WriteLine(timeNow + "| " + msg);
            string dateToday = DateTime.Today.Month + "-" + DateTime.Today.Day + "-" + DateTime.Today.Year;

            checkLogDir();
        }

        public static bool readConfig()
        {
            logMsg("looking for config file . . .");
            string configPath = AppDomain.CurrentDomain.BaseDirectory.ToString() + ConfigeFileName;

            try
            {
                if (!File.Exists(configPath)) // If file does not exist
                {
                    logMsg("config file not found");
                    using (StreamWriter sw = File.CreateText(configPath))
                    {
                        logMsg("creating config template");

                        sw.WriteLine(@"<?xml version=""1.0"" encoding=""utf-8"" ?>");
                        sw.WriteLine("<root>");
                        sw.WriteLine("  <VCast>");
                        sw.WriteLine("    <Path>C:\\Program Files (x86)\\Nyxsys Philippines Inc\\EAC VCast Player\\VCast Player v1.0.exe</Path>");
                        sw.WriteLine("    <WindowName>NYXSYS-VCast 1.0</WindowName>");
                        sw.WriteLine("    <ClassName>WindowsForms10.Window.8.app.0.378734a</ClassName>");
                        sw.WriteLine("    <UseClass>True</UseClass>");
                        sw.WriteLine("  </VCast>");
                        sw.WriteLine("");
                        sw.WriteLine("  <Game>");
                        sw.WriteLine("    <FileName>CoC_Zone.exe</FileName>");
                        sw.WriteLine("    <WindowName>CoC_Zone</WindowName>");
                        sw.WriteLine("    <ClassName></ClassName>");
                        sw.WriteLine("    <UseClass>True</UseClass>");
                        sw.WriteLine("  </Game>");
                        sw.WriteLine("");
                        sw.WriteLine("  <SplashScreen>");
                        sw.WriteLine("    <FileName>kinectSplash.exe</FileName>");
                        sw.WriteLine("    <WindowName>kinectSplash</WindowName>");
                        sw.WriteLine("    <ClassName></ClassName>");
                        sw.WriteLine("    <UseClass>False</UseClass>");
                        sw.WriteLine("  </SplashScreen>");
                        sw.WriteLine("</root>");
                    }

                    logMsg("config template created");
                    readConfig();
                }
                else
                {
                    logMsg("config found");
                    ReadXML();

                    //using (StreamReader sr = File.OpenText(configPath))
                    //{
                    //    logMsg("reading config");
                    //    int lineNumber = 0;
                    //    string s = "";
                    //    while ((s = sr.ReadLine()) != null)
                    //    {
                    //        configPaths[lineNumber] = s;
                    //        lineNumber++;
                    //    }
                    //}

                    //string baseDir = AppDomain.CurrentDomain.BaseDirectory.ToString();
                    //foreach (string file in Directory.GetFiles(baseDir + @"..\"))
                    //{
                    //    if (file.ToUpper().IndexOf("ZONE") > 0)
                    //    {
                    //        configPaths[6] = file;
                    //        configPaths[7] = zoneClassName;
                    //    }
                    //}

                    //logMsg("config read");
                    //return true;
                }
            }
            catch (Exception e)
            {
                logMsg("config read error: " + e.ToString());
                return false;
            }
            return true;
        }

        public static void ReadXML()
        {
            XmlDocument config = new XmlDocument();
            config.Load(ConfigeFileName);

            foreach (XmlNode parentNode in config.DocumentElement)
            {
                if (parentNode.ChildNodes.Count > 0)
                {
                    foreach (XmlNode childNode in parentNode)
                    {
                        switch (childNode.ParentNode.Name)
                        {
                            case "VCast":
                                if (childNode.Name == "Path")
                                    VCastPath = childNode.InnerText;
                                else if (childNode.Name == "WindowName")
                                    VCastWindowName = childNode.InnerText;
                                else if (childNode.Name == "ClassName")
                                    VCastClassName = childNode.InnerText;
                                else if (childNode.Name == "UseClass")
                                    VCastUseClass = bool.Parse(childNode.InnerText);
                                break;

                            case "Game":
                                if (childNode.Name == "FileName")
                                    GameFileName = childNode.InnerText;
                                else if (childNode.Name == "WindowName")
                                    GameWindowName = childNode.InnerText;
                                else if (childNode.Name == "ClassName")
                                    GameClassName = childNode.InnerText;
                                else if (childNode.Name == "UseClass")
                                    GameUseClass = bool.Parse(childNode.InnerText);
                                break;

                            case "SplashScreen":
                                if (childNode.Name == "FileName")
                                    SplashScreenPath = childNode.InnerText;
                                else if (childNode.Name == "WindowName")
                                    SplashScreenWindowName = childNode.InnerText;
                                else if (childNode.Name == "ClassName")
                                    SplashScreenClassName = childNode.InnerText;
                                else if (childNode.Name == "UseClass")
                                    SplashScreenUseClass = bool.Parse(childNode.InnerText);
                                break;
                        }
                    }
                }
            }
        }

        public static void UserPlayed()
        {
            string[] Months = new string[12] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            string statisticsPath = AppDomain.CurrentDomain.BaseDirectory.ToString() + StatisticsFileName;
            if (File.Exists(statisticsPath) == false)
            {
                using (StreamWriter sw = File.CreateText(statisticsPath))
                {
                    sw.WriteLine(@"<?xml version=""1.0"" encoding=""utf-8"" ?>");
                    sw.WriteLine("<root>");
                    sw.WriteLine("  <LastUsed></LastUsed>");
                    sw.WriteLine("  <TotalPlays></TotalPlays>");
                    sw.WriteLine("</root>");
                }
            }

            XmlDocument statistics = new XmlDocument();
            statistics.Load(StatisticsFileName);

            bool DateTodayExists = false;

            string DateToday = Months[System.DateTime.Today.Month - 1] + "-";
            DateToday += System.DateTime.Today.Day.ToString() + "-" + System.DateTime.Today.Year.ToString();

            Console.WriteLine(DateToday);
            foreach (XmlNode node in statistics.DocumentElement)
            {
                if (node.Name == DateToday)
                {
                    DateTodayExists = true;
                }
            }

            if (DateTodayExists == false)
            {
                XmlElement DateTodayElement = statistics.CreateElement(DateToday);
                
                statistics.DocumentElement.AppendChild(DateTodayElement);
            }

            string TimePlayed = System.DateTime.Now.Hour.ToString() + ":" + System.DateTime.Now.Minute.ToString() + ":" + System.DateTime.Now.Second.ToString();
            XmlElement TimePlayedElement = statistics.CreateElement("play" + (statistics.DocumentElement[DateToday].ChildNodes.Count + 1).ToString());
            TimePlayedElement.InnerText = TimePlayed;

            statistics.DocumentElement[DateToday].AppendChild(TimePlayedElement);
            statistics.DocumentElement["LastUsed"].InnerText = DateToday + " - " + TimePlayed;

            statistics.Save(StatisticsFileName);
            SetTotalPlays(statistics);
        }

        public static void SetTotalPlays(XmlDocument currentDocument)
        {
            int TotalPlaysCount = 0;
            foreach (XmlNode PlayNode in currentDocument.DocumentElement)
            {
                if (PlayNode.Name != "LastUsed" && PlayNode.Name != "TotalPlays")
                {
                    TotalPlaysCount += PlayNode.ChildNodes.Count;
                }
            }

            currentDocument.DocumentElement["TotalPlays"].InnerText = TotalPlaysCount.ToString();
            currentDocument.Save(StatisticsFileName);
        }

        public static void checkLogDir()
        {
            if (!Directory.Exists("logs"))
            {
                try
                {
                    Directory.CreateDirectory("logs");
                }
                catch (Exception err)
                {
                    logMsg(err.ToString());
                }
            }
        }

        public static void StartApps(short application_name)
        {
            logMsg("Starting apps...");

            // Initialize Splash Screen
            proc[SPLASH] = new Process();

            switch (application_name)
            {
                case ZONE:
                    //Start Zone
                    try
                    {
                        proc[ZONE] = new Process();
                        //proc[ZONE].StartInfo.FileName = @"" + configPaths[6];
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory.ToString();
                        proc[ZONE].StartInfo.FileName = baseDir + @"..\" + GameFileName;
                        proc[ZONE].EnableRaisingEvents = true;
                        proc[ZONE].Exited += new EventHandler(ZONE_HasExited);
                        proc[ZONE].Start();
                        //Thread.Sleep(5000); // Delay CMS start
                    }
                    catch (Exception ex)
                    {
                        logMsg("Error Opening ZONE form path: " + GameFileName);
                    }

                    break;

                case CMS:
                    // Kill all other VCast instances
                    KillAllVCast();

                    // Start CMS
                    try
                    {
                        proc[CMS] = new Process();
                        proc[CMS].StartInfo.FileName = @"" + VCastPath;
                        proc[CMS].EnableRaisingEvents = true;
                        proc[CMS].Exited += new EventHandler(CMS_HasExited);
                        proc[CMS].Start();
                        CMS_Obj.Started = true;
                    }
                    catch (Exception ex)
                    {
                        logMsg("Error Opening CMS from path: " + VCastPath);
                    }

                    break;

                default:
                    logMsg("no application to start");
                    break;
            }

            IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
            ShowWindow(handle, ShowWindowCommand.SW_MINIMIZE);

            appsStarted = true;
        }


        // Kill All Running VCast and Zone before we start the program
        private static void Kill_All_Running_Process()
        {
            Process[] processlist = Process.GetProcesses();
            foreach (Process theprocess in processlist)
            {
                string ProcessName = theprocess.ProcessName.ToUpper();
                if (ProcessName.IndexOf("VCAST PLAYER V1.0") >= 0)
                    theprocess.Kill();
            }
        }

        static void KillAllVCast()
        {
            Process[] processlist = Process.GetProcesses();

            foreach (Process theprocess in processlist)
            {
                string ProcessName = theprocess.ProcessName.ToUpper();
                if (ProcessName.IndexOf("VCAST PLAYER V1.0") >= 0 || ProcessName.IndexOf("VCAST-PLAYER V1.0") >= 0)
                {
                    try
                    {
                        theprocess.Kill();
                        Console.WriteLine("Process: {0} ID: {1}", theprocess.ProcessName, theprocess.Id);
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine(err.ToString());
                    }
                }
            }
        }

        // Restart CMS in case the process exit
        private static void CMS_HasExited(object sender, System.EventArgs e)
        {
            try
            {
                proc[CMS].Exited -= new EventHandler(CMS_HasExited);
                proc[CMS].Dispose();
                proc[CMS] = null;
                proc[CMS] = new Process();
                proc[CMS].StartInfo.FileName = @"" + VCastPath;
                proc[CMS].EnableRaisingEvents = true;
                proc[CMS].Exited += new EventHandler(CMS_HasExited);
                proc[CMS].Start();
                showApp(CMS);
            }
            catch (Exception ex)
            {
                logMsg("Error RE-Opening CMS " + ex.ToString());
            }
        }

        // Restart ZONE in case the process exit
        private static void ZONE_HasExited(object sender, System.EventArgs e)
        {
            try
            {
                proc[ZONE].Exited -= new EventHandler(ZONE_HasExited);
                proc[ZONE].Dispose();
                proc[ZONE] = null;
                proc[ZONE] = new Process();
                string baseDir = AppDomain.CurrentDomain.BaseDirectory.ToString();
                proc[ZONE].StartInfo.FileName = baseDir + @"..\" + GameFileName;
                //proc[ZONE].StartInfo.FileName = @"" + GameFileName;
                proc[ZONE].EnableRaisingEvents = true;
                proc[ZONE].Exited += new EventHandler(ZONE_HasExited);
                proc[ZONE].Start();
                showApp(CMS);
            }
            catch (Exception ex)
            {
                logMsg("Error RE-Opening ZONE " + ex.ToString());
            }
        }

        public static void KillApps()
        {
            proc[CMS].CloseMainWindow(); // Kill CMS
            proc[ZONE].CloseMainWindow(); // Kill Zone
            appsStarted = false;
        }

        public static void StartListening()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

            logMsg("\r\n=======================================================");
            logMsg("HTech's Server Application Version: " + fvi.FileVersion);

            while (readConfig() == false)
            {
                // Keep reading config file
            }
            AsyncSocketListener.StartApps(ZONE);

            // Hide Taskbar
            ShowWindow(FindWindow("Shell_TrayWnd", ""), ShowWindowCommand.SW_HIDE);
            // Hide Start Orb
            ShowWindow(FindWindow("Button", "Start"), ShowWindowCommand.SW_HIDE);

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
            Socket listener = (Socket)asyncResult.AsyncState;
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
            StateObject state = (StateObject)asyncResult.AsyncState;
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
                    case "playerDetected":
                        //if (!splashShown)
                        while (!splashShown)
                        {
                            try
                            {
                                proc[SPLASH] = Process.Start(SplashScreenPath);
                                splashShown = true;
                                logMsg("showing splash");
                            }
                            catch (Exception e)
                            {
                                splashShown = false;
                                logMsg(e.ToString());
                            }
                        }
                        break;

                    case "noPlayer":
                        //if (splashShown == true) // Kill splash screen since player is not detected or the player quit
                        while (splashShown)
                        {
                            try
                            {
                                proc[SPLASH].Kill();
                                splashShown = false;
                                logMsg("killing splash");
                            }
                            catch (Exception e)
                            {
                                splashShown = true;
                                logMsg(e.ToString());
                            }
                        }
                        break;

                    case "startCMS":
                        AsyncSocketListener.StartApps(CMS);
                        break;

                    case "muteCMS":
                        muteApp(CMS, true); // Mute CMS
                        break;

                    case "showCMS":
                        if (!CMS_Obj.Started)
                        {
                            StartApps(CMS);
                            break;
                        }

                        ActiveWindow = 0;
                        muteApp(CMS, false);
                        muteApp(ZONE, true);
                        showApp(CMS);
                        logMsg("showing CMS");
                        break;

                    case "showZone":
                        muteApp(CMS, true);
                        muteApp(ZONE, false);
                        showApp(ZONE);
                        ActiveWindow = 1;
                        UserPlayed();

                        logMsg("showing ZONE");
                        break;

                    case "topSplash":
                        KeepSplashOnTop();
                        break;

                    case "startApps": // Unity test
                        if (appsStarted == false)
                        {
                            AsyncSocketListener.StartApps(ZONE);
                        }
                        break;

                    case "killApps": // Unity test
                        if (appsStarted == true)
                        {
                            AsyncSocketListener.KillApps();
                        }
                        break;

                    case "disableCamera":
                        if (cameraDisabled == false)
                        {
                            logMsg("disabling camera");
                            //try
                            //{
                            //    DeviceHelper.SetDeviceEnabled(deviceToDisable, false);
                            //    cameraDisabled = true;
                            //}
                            //catch(Exception e)
                            //{
                            //    logMsg(e.ToString());
                            //}
                        }
                        break;

                    case "enableCamera":
                        if (cameraDisabled == true)
                        {
                            logMsg("enabling camera");
                            //try
                            //{
                            //    DeviceHelper.SetDeviceEnabled(deviceToDisable, true);
                            //    cameraDisabled = false;
                            //}
                            //catch(Exception e)
                            //{
                            //    logMsg(e.ToString());
                            //}
                        }
                        break;

                    case "userPlayed":
                        UserPlayed();
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
                Socket handler = (Socket)asyncResult.AsyncState;

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
            int volume = 90;
            switch (procNum)
            {
                case CMS:
                    try
                    {
                        VolumeMixer.SetApplicationMute((uint)proc[CMS].Id, _muteApp);
                        VolumeMixer.SetApplicationVolume((uint)proc[ZONE].Id, volume);
                    }
                    catch (Exception err)
                    {
                        logMsg(err.ToString());
                    }
                    break;
                case ZONE:
                    try
                    {
                        VolumeMixer.SetApplicationMute((uint)proc[ZONE].Id, _muteApp);
                        VolumeMixer.SetApplicationVolume((uint)proc[CMS].Id, volume);
                    }
                    catch (Exception err)
                    {
                        logMsg(err.ToString());
                    }
                    break;
            }
        }

        public static void showApp(int procNum)
        {
            IntPtr handle = IntPtr.Zero; // Create a handle to manipulate the windows
            handle = proc[procNum].MainWindowHandle;

            try
            {
                switch (procNum)
                {
                    case CMS:
                        // Show CMS window
                        SetWindowPos(handle, (IntPtr)HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                        ShowWindow(handle, ShowWindowCommand.SW_SHOWMAXIMIZED);

                        handle = proc[ZONE].MainWindowHandle;
                        SetWindowPos(handle, (IntPtr)HWND_NOTTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                        SetWindowPos(handle, (IntPtr)HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

                        Thread.Sleep(250);
                        MoveMousePointerOutofBound(UPPER_LEFT);
                        break;

                    case ZONE:
                        // Show Game window
                        SetWindowPos(handle, (IntPtr)HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                        ShowWindow(handle, ShowWindowCommand.SW_SHOWMAXIMIZED);

                        handle = proc[CMS].MainWindowHandle;
                        SetWindowPos(handle, (IntPtr)HWND_NOTTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                        SetWindowPos(handle, (IntPtr)HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);

                        MoveMousePointerOutofBound(UPPER_RIGHT);
                        break;
                }

                SetWindowPos(handle, (IntPtr)HWND_NOTTOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
            }
            catch (Exception e)
            {
                logMsg(e.ToString());
            }
        }

        public static void KeepSplashOnTop()
        {
            IntPtr handle = IntPtr.Zero; // Create a handle to manipulate the windows
            try
            {
                handle = proc[SPLASH].MainWindowHandle;
                SetWindowPos(handle, (IntPtr)HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                //ShowWindow(handle, ShowWindowCommand.SW_SHOW);
            }
            catch (Exception e)
            {
                logMsg(e.ToString());
            }
        }

        // Hide the mouse pointer somewhere
        public static void MoveMousePointerOutofBound(short screenPosition)
        {
            if (screenPosition == UPPER_LEFT)
                Cursor.Position = new Point(0, 0);
            else if (screenPosition == UPPER_RIGHT)
                Cursor.Position = new Point(Screen.PrimaryScreen.WorkingArea.Width, 0);
        }
    }
}
