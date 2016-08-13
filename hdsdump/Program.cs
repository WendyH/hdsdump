/* By WendyH. GNU GPL License version 3
 * Based on https://raw.github.com/K-S-V/Scripts/master/AdobeHDS.php
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace hdsdump
{
    static class Constants
    {
        public const int AUDIO                = 0x08;
        public const int VIDEO                = 0x09;
        public const int SCRIPT_DATA          = 0x12;
        public const int FRAME_TYPE_INFO      = 0x05;
        public const int CODEC_ID_AVC         = 0x07;
        public const int CODEC_ID_AAC         = 0x0A;
        public const int AVC_SEQUENCE_HEADER  = 0x00;
        public const int AAC_SEQUENCE_HEADER  = 0x00;
        public const int AVC_NALU             = 0x01;
        public const int AVC_SEQUENCE_END     = 0x02;
        public const int FRAMEFIX_STEP        = 0x28;
        public const int STOP_PROCESSING      = 0x02;
        public const int INVALID_TIMESTAMP    = -1;
        public const int TIMECODE_DURATION    = 8;
    }

    class Program
    {
        public static bool   debug        = false;
        public static string logfile      = "hdsdump.log";
        public static string manifest     = "";
        public static string outDir       = "";
        public static string outFile      = "hdsdump.flv";
        public static string skip         = "";
        public static bool   play         = false;
        public static int    start        = 0;
        public static int    threads      = 1;
        public static int    duration     = 0;
        public static int    filesize     = 0;
        public static bool   fproxy       = false;
        public static bool   showtime     = false;
        public static bool   waitkey      = false;
        public static bool   showmlink    = false;
        public static bool   fcontinue    = false;
        public static bool   redirect2prc = false;
        public static System.Diagnostics.Stopwatch sw;
        public static System.Diagnostics.Process redir2Prog;

        private static bool? _consolePresent;
        public static bool ConsolePresent {
            get {
                if (_consolePresent == null) {
                    _consolePresent = true;
                    try { int window_height = Console.WindowHeight; } catch { _consolePresent = false; }
                }
                return _consolePresent.Value;
            }
        }

        public static void FatalExceptionObject(object exceptionObject) {
            //if (!debug) return;
            string msg = exceptionObject.ToString() + "\r\n";
            Exception ex = exceptionObject as Exception;
            if (ex != null) {
                msg = ex.ToString() + "\r\n";
            }

            string tempFile = Path.Combine(Path.GetTempPath(), "hdsdump.log");
            long   length   = 0;

            var    st       = new System.Diagnostics.StackTrace(ex, true);
            var    frame    = st.GetFrame(st.FrameCount - 1);
            int    line     = frame.GetFileLineNumber();

            try {
                if (File.Exists(tempFile))
                    length = new FileInfo(tempFile).Length;
                if (length > 1024 * 1024 * 2)
                    File.Delete(tempFile);
                File.AppendAllText(tempFile, $"{DateTime.Now.ToString(CultureInfo.CurrentCulture)} Line: {line} {msg}\r\n");
            } catch {
                // ingnore
            }
        }

        public static void Main(string[] args)
		{
            try {
                AppDomain.CurrentDomain.UnhandledException += (sender, e) => FatalExceptionObject(e.ExceptionObject);

                redirect2prc = Check4Redirect2Process(ref args);
			    CLI cli = new CLI(args);
                if (cli.chkParam("waitkey"  )) waitkey = true;
                if (cli.chkParam("nowaitkey")) waitkey = false;
                if (cli.chkParam("help"     )) { cli.displayHelp(); Quit(""); }

                F4F f4f = new F4F();
                if (cli.chkParam("duration" )) Int32.TryParse(cli.getParam("duration"), out duration);
                if (cli.chkParam("filesize" )) Int32.TryParse(cli.getParam("filesize"), out filesize);
                if (cli.chkParam("threads"  )) Int32.TryParse(cli.getParam("threads" ), out threads );
                if (cli.chkParam("start"    )) Int32.TryParse(cli.getParam("start"   ), out start   );
                if (cli.chkParam("auth"     )) f4f.auth    = "?" + cli.getParam("auth");
                if (cli.chkParam("urlbase"  )) f4f.baseUrl = cli.getParam("urlbase" );
                if (cli.chkParam("quality"  )) f4f.quality = cli.getParam("quality" );
                if (cli.chkParam("manifest" )) manifest    = cli.getParam("manifest");
                if (cli.chkParam("outdir"   )) outDir      = cli.getParam("outdir"  );
                if (cli.chkParam("outfile"  )) outFile     = cli.getParam("outfile" );
                if (cli.chkParam("skip"     )) skip        = cli.getParam("skip"    );
                if (cli.chkParam("logfile"  )) logfile     = cli.getParam("logfile" );
                if (cli.chkParam("debug"    )) debug     = true;
                if (cli.chkParam("play"     )) play      = true;
                if (cli.chkParam("showtime" )) showtime  = true;
                if (cli.chkParam("showmlink")) showmlink = true;
                if (cli.chkParam("fproxy"   )) fproxy    = true;
                if (cli.chkParam("continue" )) fcontinue = true;
                if (cli.chkParam("post"     )) HTTP.POST = true;
                if (cli.chkParam("referer"  )) HTTP.Referer       = cli.getParam("referer"  );
                if (cli.chkParam("cookies"  )) HTTP.Cookies       = cli.getParam("cookies"  );
                if (cli.chkParam("useragent")) HTTP.Useragent     = cli.getParam("useragent");
                if (cli.chkParam("username" )) HTTP.Username      = cli.getParam("username" );
                if (cli.chkParam("password" )) HTTP.Password      = cli.getParam("password" );
                if (cli.chkParam("proxy"    )) HTTP.Proxy         = cli.getParam("proxy"    );
                if (cli.chkParam("proxyuser")) HTTP.ProxyUsername = cli.getParam("proxyuser");
                if (cli.chkParam("proxypass")) HTTP.ProxyPassword = cli.getParam("proxypass");

                if (HTTP.Referer == "") RegExMatch(@"^(.*?://.*?/)", manifest, out HTTP.Referer);

                String strVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                ShowHeader("HDSdump by WendyH (K-S-V AdobeHDS based) v <c:White>" + strVersion);

                //if (showmlink) Program.Message("Manifest: <c:Green>" + manifest);

                if (manifest == "") Program.Quit("<c:Red>Please specify the manifest. (switch '<c:White>-h</c>' or '<c:White>--help</c>' for help message)");

                if (showtime) ShowTimeElapsed("", true);

                if (Check4KnownLinks(manifest)) Program.Quit("Done.");

                if (outFile.IndexOf(@"\\.\pipe\") == 0) f4f.usePipe = true;
                if (skip != "")
                {
                    string sH, sM, sS;
                    if (RegExMatch3(@"(\d{2}):(\d{2}):(\d{2})", skip, out sH, out sM, out sS))
                        f4f.fromTimestamp = (((Int32.Parse(sH) * 60 * 60) + (Int32.Parse(sM) * 60) + Int32.Parse(sS)) * 1000);
                }
                else
                {
                    if (fcontinue) f4f.CheckLastTSExistingFile();
                }
                f4f.play       = play;
                f4f.threads    = threads;
                f4f.duration   = duration;
                f4f.filesize   = filesize;
                f4f.redir2Proc = redirect2prc;
                f4f.start      = start;

                // Disable metadata if it invalidates the stream duration
                if ((f4f.fromTimestamp > 0) || (start > 0) || (duration > 0) || (filesize > 0))
                    f4f.metadata = false;

                cli.EchoSetsParameters();

			    f4f.DownloadFragments(manifest);
                Program.Quit("Done.");

            } catch (Exception huh) {
                FatalExceptionObject(huh);
            }

        }

        public static bool Check4KnownLinks(string sLink) {
            string sVideoId = "";
            if (RegExMatch(@"megogo.net/../view/(\d+)", sLink, out sVideoId))
            {
                HTTP req = new HTTP();
                sLink = "http://megogo.net/b/info/?s=0&e=0&p=0&t=&m=-1&i=" + sVideoId;
                req.get(sLink);
                if (!RegExMatch("<src>(.*?)</src>", req.responseText, out manifest)) 
                    Program.Quit("Cannot get the manifest from link megogo.net.");
            } 
            else if (sLink.IndexOf("now.ru/item/") > 0)
            {
                HTTP req = new HTTP();
                req.get(sLink);
                if (RegExMatch("\"smil\"\\s*?:\\s*?\"(.*?)\"", req.responseText, out sLink))
                {
                    sLink = sLink.Replace(@"\/", "/");
                    req.get(sLink);
                    if (!RegExMatch("<httpds[^>]+src=\"(.*?)\"", req.responseText, out manifest))
                        Program.Quit("<c:Red>Cannot get the manifest from link <c:White>now.ru</c>.");
                }

            }
            return false;
        }
        
        public static void Quit(string msg = "")
        {
            if (!play)
            {
                //if (msg != "") waitkey = true;
                if (showtime) ShowTimeElapsed("\n\r" + msg);
                else Message(msg);
                if (waitkey && ConsolePresent) { Console.WriteLine("Press any key to continue..."); Console.ReadKey(); }
            }
            if (redirect2prc) Thread.Sleep(1000);
            Environment.Exit(0);
        }

        public static void ShowHeader(string header)
        {
            if (play || !ConsolePresent) return;
            string h  = Regex.Replace(header, @"(<c:\w+>|</c>)", "");
            int width = (Console.WindowWidth / 2 + h.Length / 2);
            Program.Message(String.Format("\n{0," + width.ToString() + "}\n", header));
        }

        public static void Message(string msg)
        {
            if (play || !ConsolePresent) return;
            Console.ForegroundColor = ConsoleColor.Gray;
            List<ConsoleColor> colorsStack = new List<ConsoleColor>();
            string[] chars = msg.Split('<'); string spChar = "";
            foreach (string s in chars) {
                string sText = s;
                Match m = Regex.Match(s, @"^c:(\w+)>");
                if (m.Success) {
                    sText = s.Replace(m.Groups[0].Value, "");
                    try {
                        colorsStack.Add(Console.ForegroundColor);
                        Console.ForegroundColor = (ConsoleColor)Enum.Parse(typeof(ConsoleColor), m.Groups[1].Value);
                    } catch { };

                } else if (Regex.IsMatch(s, @"^/c>")) {
                    if (colorsStack.Count > 0) {
                        Console.ForegroundColor = colorsStack[colorsStack.Count - 1];
                        colorsStack.RemoveAt(colorsStack.Count - 1);
                    } else
                        Console.ResetColor();
                    sText = s.Substring(3);

                } else sText = spChar + sText;
                Console.Write(sText);
                spChar = "<";
            }
            if (!msg.EndsWith("\r")) Console.Write("\n\r");
            Console.ResetColor();
        }

        public static void DebugLog(string msg)
        {
            if (!Program.debug || !ConsolePresent) return;
            if      (Program.logfile == "STDERR") Console.Error.WriteLine(msg);
            else if (Program.logfile == "STDOUT") Console.WriteLine(msg);
            else File.AppendAllText(Program.logfile, msg + "\n");
        }

        public static bool RegExMatch(string RegX, string wherelook, out string resultValue)
        {
            Match m = Regex.Match(wherelook, @RegX, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            resultValue = m.Groups[1].Value;
            return m.Groups[1].Success;
        }

        public static bool RegExMatch3(string RegX, string wherelook, out string resultValue1, out string resultValue2, out string resultValue3)
        {
            Match m = Regex.Match(wherelook, @RegX, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            resultValue1 = m.Groups[1].Value;
            resultValue2 = m.Groups[2].Value;
            resultValue3 = m.Groups[3].Value;
            return (m.Groups[1].Success && m.Groups[2].Success && m.Groups[3].Success);
        }

        public static void ShowTimeElapsed(string msg, bool start = false)
        {
            if (start)
            {
                sw = new System.Diagnostics.Stopwatch();
                sw.Start();
            }
            else
            {
                if (!play) Program.Message(msg + "\n\r<c:DarkCyan>Time elapsed: <c:DarkGray>" + sw.Elapsed);
            }
        }

        static bool Check4Redirect2Process(ref string[] args)
        {
            string par2proc = "";
            string pName    = "";
            bool   isredir  = false;
            string par;
            for (int i = 0; i < args.Length; i++)
            {
                par = args[i];
                if ((par == "|") && (args.Length > (i + 1)))
                {
                    i++;
                    isredir = true;
                    pName   = args[i];
                    continue;
                }
                if (!isredir) continue;
                par2proc += " " + args[i];
            }
            if (isredir)
            {
                redir2Prog = new System.Diagnostics.Process();
                redir2Prog.StartInfo.UseShellExecute = false;
                redir2Prog.StartInfo.Arguments       = par2proc;
                redir2Prog.StartInfo.FileName        = pName;
                redir2Prog.StartInfo.RedirectStandardInput = true;
                redir2Prog.Start();
            }
            return isredir;
        }

        public static string StripHtml(string source) {
            string output;

            //get rid of HTML tags
            output = Regex.Replace(source, "<[^>]*>", string.Empty);

            //get rid of multiple blank lines
            output = Regex.Replace(output, @"^\s*$\n", string.Empty, RegexOptions.Multiline);

            return output;
        }
    }

    class CLI
    {
        protected static Dictionary<string, string>[] ACCEPTED = {
                new Dictionary<string,string> {
					// Single switches
                    {"h |help"     , "displays this help"},
                    {"l |debug"    , "out debug output to log file"},
                    {"p |play"     , "dump flv data to stderr for piping to another program"},
                    {"st|showtime" , "show time remaining"},
                    {"fp|fproxy"   , "force proxy for downloading of fragments"},
                    {"  |post"     , "use POST method for http request"},
                    {"wk|waitkey"  , "wait pressed any key at the end"},
                    {"sl|showmlink", "display manifest link at the start (deprecated)"},
                    {"c |continue" , "continue if possible downloading with exists file"},
//                    {"nw|nowaitkey", "no wait key press when not done exit"}
                },
                new Dictionary<string,string> {
					// Switches with parameters
                    {"a |auth"     , "authentication string for fragment requests (add '?' with parameter to end manifest url)"},
                    {"s |duration" , "stop live recording after specified number of seconds"},
                    {"fs|filesize" , "split output file in chunks of specified size (MB)"},
                    {"m |manifest" , "manifest or playlist file for downloading of fragments"},
                    {"b |urlbase"  , "base url for relative path in playlist"},
                    {"od|outdir"   , "destination folder for output file"},
                    {"o |outfile"  , "filename to use for output file"},
                    {"th|threads"  , "number of threads to download fragments"},
                    {"q |quality"  , "selected quality level (low|medium|high) or exact bitrate"},
                    {"f |start"    , "start from specified fragment"},
                    {"t |skip"     , "skip time hh:mm:ss"},
                    {"lf|logfile"  , "file for debug output"},
                    {"ua|useragent", "User-Agent to use for emulation of browser requests"},
                    {"re|referer"  , "Referer in the headers http requests"},
                    {"ck|cookies"  , "Cookies in the headers http requests"},
                    {"un|username" , "username to use for access to http server"},
                    {"ps|password" , "password to use for access to http server"},
                    {"px|proxy"    , "proxy for downloading of manifest (http://proxyAddress:proxyPort)"},
                    {"pu|proxyuser", "username for proxy server"},
                    {"pp|proxypass", "password for proxy server"}
                }
            };

        private Dictionary<string, string> Params = new Dictionary<string, string> { };

        public CLI(string[] argv)
        {
            // Parse params
            string doubleParam = "", doubleKey="", arg, shrtKey, longKey;
            for (int i = 0; i < argv.Length; i++)
            {
                arg = argv[i];
                if (arg == "|") break; // 4windows
                bool isparam = Regex.IsMatch(arg, "^-");
                if (isparam) arg = Regex.Replace(arg, "^--?", "");
                if ((doubleParam != "") && isparam)
                    Program.Quit("<param> <c:Red>expected after '<c:White>" + argv[i-1] + "</c>' switch <c:DarkCyan>(" + ACCEPTED[1][doubleKey] + ")</c>\n");
                else if ((doubleParam == "") && !isparam)
                    Program.Quit("'<c:Green>" + argv[i] + "</c>' <c:Red>is an invalid switch, use <c:White>-h</c> or <c:White>--help</c> to display valid switches\n");
                else if ((doubleParam == "") && isparam)
                {
                    bool keyFound = false;
                    foreach (KeyValuePair<string, string> pair in ACCEPTED[0]) {
                        shrtKey = pair.Key.Split('|')[0].Trim();
                        longKey = pair.Key.Split('|')[1].Trim();
                        if (arg == shrtKey) arg = longKey;
                        if (arg == longKey) { keyFound = true; break; }
                    }
                    if (!keyFound) foreach (KeyValuePair<string, string> pair in ACCEPTED[1]) {
                            shrtKey = pair.Key.Split('|')[0].Trim();
                            longKey = pair.Key.Split('|')[1].Trim();
                            if (arg == shrtKey) arg = longKey;
                            if (arg == longKey) {
                                doubleParam = arg;
                                doubleKey   = pair.Key;
                                keyFound = true; 
                                break; 
                            }
                    }
                    if (!keyFound)
                        Program.Quit("<c:Red>There's no <c:Green>" + argv[i] + "</c> switch, use <c:White>-h</c> or <c:White>--help</c> to display all switches\n");
                    if (this.Params.ContainsKey(arg))
                        Program.Quit("'<c:White>" + argv[i] + "</c>' <c:Red>switch cannot occur more than once\n");
                    Params[arg] = "[1]";

                }
                else if ((doubleParam != "") && !isparam)
                {
                    Params[doubleParam] = arg;
                    doubleParam = "";
                }
            }
        }

        public void EchoSetsParameters() {
            string sKey, sParameters="", sValues="";
            foreach (KeyValuePair<string, string> pair in this.Params) {
                //sKey = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(pair.Key);
                sKey = pair.Key;
                if (pair.Value == "[1]") {
                    sParameters += sKey+" ";
                } else {
                    sValues += String.Format("<c:DarkCyan>{0,-10}: <c:DarkGreen>{1}\n\r", sKey, pair.Value);
                }
            }
            if (sParameters != "") Program.Message(String.Format("<c:DarkCyan>Parameters:</c> <c:DarkGreen>{0,-10}", sParameters));
            if (sValues     != "") Program.Message(sValues+"\n\r");
        }

        public bool chkParam(string name)
        {
            return this.Params.ContainsKey(name);
        }

        public string getParam(string name)
        {
            if (this.Params.ContainsKey(name)) return Params[name].Trim();
            else return "";
        }

        public void displayHelp()
        {
            string shrtKey = "", longKey = "";
            if (Program.play) return;
            Program.Message("You can use <c:White>hdsdump</c> with following switches: \n\r\n\r");
            foreach (KeyValuePair<string, string> pair in ACCEPTED[0]) 
            {
                shrtKey = pair.Key.Split('|')[0].Trim();
                longKey = pair.Key.Split('|')[1].Trim();
                if (shrtKey=="")
                    Program.Message(String.Format("     <c:White>--{0,-17} <c:DarkCyan>{1}", longKey, pair.Value));
                else
                    Program.Message(String.Format(" <c:White>-{0,-2}</c>|<c:White>--{1,-17}</c> <c:DarkCyan>{2}", shrtKey, longKey, pair.Value));
            }
            foreach (KeyValuePair<string, string> pair in ACCEPTED[1])
            {
                shrtKey = pair.Key.Split('|')[0].Trim();
                longKey = pair.Key.Split('|')[1].Trim();
                if (shrtKey=="")
                    Program.Message(String.Format("     <c:White>--{0,-10}</c><param> <c:DarkCyan>{1,-7}", longKey, pair.Value));
                else
                    Program.Message(String.Format(" <c:White>-{0,-2}</c>|<c:White>--{1,-10}</c><param> <c:DarkCyan>{2,-7}", shrtKey, longKey, pair.Value));

            }
        }
    }

    static class UriHacks
    {
        // System.UriSyntaxFlags is internal, so let's duplicate the flag privately
        private const int UnEscapeDotsAndSlashes = 0x2000000;

        public static void LeaveDotsAndSlashesEscaped(Uri uri)
        {
            if (uri == null) throw new ArgumentNullException("uri");
            FieldInfo fieldInfo = uri.GetType().GetField("m_Syntax", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo != null) {
                object uriParser = fieldInfo.GetValue(uri);
                fieldInfo = typeof(UriParser).GetField("m_Flags", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fieldInfo != null) {
                    object uriSyntaxFlags = fieldInfo.GetValue(uriParser);
                    // Clear the flag that we don't want
                    uriSyntaxFlags = (int)uriSyntaxFlags & ~UnEscapeDotsAndSlashes;
                    fieldInfo.SetValue(uriParser, uriSyntaxFlags);
                }
            }
        }
    }

    class HTTP
    {
        public string Url    = "";
        public string Status = "";
        public byte[] ResponseData;
        public static string Useragent     = "Mozilla/5.0 (Windows NT 5.1; rv:20.0) Gecko/20100101 Firefox/20.0";
        public static string Referer       = "";
        public static string Cookies       = "";
        public static string Proxy         = "";
        public static string ProxyUsername = "";
        public static string ProxyPassword = "";
        public static string Username      = "";
        public static string Password      = "";
        public static bool   POST          = false;
        public bool notUseProxy  = false;
        private int bufferLenght = 1048576;
        public WebHeaderCollection Headers = new WebHeaderCollection();

		public string responseText
		{
			get { return ASCIIEncoding.ASCII.GetString(this.ResponseData); }
		}		

		public HTTP(bool lnotUseProxy = false)
        {
            this.notUseProxy = lnotUseProxy;
        }

        public int get(string sUrl)
        {
            int RetCode = 0;
            this.Url          = sUrl;
            this.ResponseData = new byte[0];  
            if (!sUrl.StartsWith("http"))
            {   // if not http url - try load as file
                if (File.Exists(sUrl)) {
                    this.ResponseData = File.ReadAllBytes(sUrl);
                    RetCode = 200;
                } else {
                    this.Status = "File not found.";
                    RetCode = 404;
                }
                return RetCode;
            }
            HttpWebRequest request = this.CreateRequest();
            string postData = "";
            if (POST)
            {
                int questPos = sUrl.IndexOf('?');
                if (questPos > 0)
                {
                    this.Url = sUrl.Substring(0, questPos);
                    postData = sUrl.Substring(questPos + 1);
                }
            }
            string s = Cookies.Trim();
            if ((s!="") && (s.Substring(s.Length - 1, 1) != ";")) Cookies = s + "; ";
            if (POST)
            {
                StreamWriter sw = new StreamWriter(request.GetRequestStream());
                sw.Write(postData);
                sw.Close();
            }
            HttpWebResponse response = HttpWebResponseExt.GetResponseNoException(request);
            this.Status = response.StatusDescription;
            RetCode = (int)response.StatusCode;
            if (response.Headers.Get("Set-cookie")!=null) {
                Program.RegExMatch("^(.*?);", response.Headers.Get("Set-cookie"), out s);
                Cookies += s + "; ";
            }
            Stream dataStream = response.GetResponseStream();
            if (response.ContentEncoding.ToLower().Contains("gzip"))
                dataStream = new GZipStream(dataStream, CompressionMode.Decompress);
            else if (response.ContentEncoding.ToLower().Contains("deflate"))
                dataStream = new DeflateStream(dataStream, CompressionMode.Decompress);

            byte[] buffer = new byte[bufferLenght];
            using (MemoryStream ms = new MemoryStream())
            {
                int readBytes;
                while ((readBytes = dataStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, readBytes);
                }
                this.ResponseData = ms.ToArray();
            }
            dataStream.Close();
            response.Close();
            return RetCode;
        }

        private HttpWebRequest CreateRequest()
        {
            Uri myUri = new Uri(this.Url);
            UriHacks.LeaveDotsAndSlashesEscaped(myUri);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(myUri);
            if (Useragent != "") request.UserAgent   = Useragent;
            if (Referer   != "") request.Referer     = Referer;
            if (Username  != "") request.Credentials = new NetworkCredential(Username, Password);
            if ((Proxy != "") && !this.notUseProxy)
            {
                if (!Proxy.StartsWith("http")) Proxy = "http://" + Proxy;
                WebProxy myProxy = new WebProxy();
                myProxy.Address  = new Uri(Proxy);
                if (ProxyUsername != "") 
                    myProxy.Credentials = new NetworkCredential(ProxyUsername, ProxyPassword);
                request.Proxy = myProxy;
            }
            if (POST) request.Method = "POST";
            else      request.Method = "GET";
            request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            request.Headers.Add("Accept-Language: en-us,en;q=0.5");
            request.Headers.Add("Accept-Encoding: gzip,deflate");
            request.Headers.Add("Accept-Charset: ISO-8859-1,utf-8;q=0.7,*;q=0.7");
            request.KeepAlive = true;
            request.Headers.Add("Keep-Alive: 900");
            foreach (string key in this.Headers.AllKeys)
            {
                request.Headers.Set(key , this.Headers[key]);
            }
            if (Cookies != "") request.Headers.Add("Cookie: " + Cookies);
            request.ContentType = "application/x-www-form-urlencoded";
            return request;
        }
    }

    class F4F: IDisposable
    {
        public bool           usePipe       = false;
        public FileStream     pipeStream = null;
        public BinaryWriter   pipeWriter = null;
        public Microsoft.Win32.SafeHandles.SafeFileHandle pipeHandle = null;

        public int      manifesttype  = 0; // 0 - hds, 1 - xml playlist, 2 - m3u playlist, 3 - json manifest with template
        public string fragUrlTemplate = "<FRAGURL>Seg<SEGNUM>-Frag<FRAGNUM>";
        public string   auth          = "";
        public long     fromTimestamp = -1;
        public string   bootstrapUrl  = "";
        public string   baseUrl       = "";
        public int      duration      = 0;
        public int      fileCount     = 1;
        public int      start         = 0;
        public string   format        = " {0,-8}{1,-16}{2,-16}{3,-8}";
        public bool     live          = false;
        public bool     metadata      = true; 
        public int      threads       = 1;
        public bool     play          = false;
        public bool     redir2Proc    = false;
        public string   quality       = "high";
        public int      segNum        = 1;
        public int      fragNum       = 0;
        public int      fragCount     = 0;
        public int      fragsPerSeg   = 0;
        public int      lastFrag      = 0;
        public int      discontinuity = 0;
        public string   fragUrl       = "";
        public bool     audio             = false;
        public long     baseTS            = 0;
        public long     negTS             = 0;
        public int      filesize          = 0;
        public int      fixWindow         = 1000;
        public bool     video             = false;
        public int      prevTagSize       = 4;
        public int      tagHeaderLen      = 11;
        public long     prevAudioTS       = -1;
        public long     prevVideoTS       = -1;
        public long     currentTS         = 0;
        public long     pAudioTagLen      = 0;
        public long     pVideoTagLen      = 0;
        public long     pAudioTagPos      = 0;
        public long     pVideoTagPos      = 0;
        public bool     prevAVC_Header    = false;
        public bool     prevAAC_Header    = false;
        public bool     AVC_HeaderWritten = false;
        public bool     AAC_HeaderWritten = false;
        private bool    FLVHeaderWritten  = false;
        private bool    FLVContinue       = false;
        private int  threadsRun        = 0;
        private int  fragmentsComplete = 0;
        private int  currentDuration   = 0;
        private long currentFilesize   = 0;

        private Fragment2Dwnld[] Fragments2Download;
        private XmlNamespaceManager nsMgr;
        private Dictionary<string, Media> media = new Dictionary<string, Media>();
        private List<string> _serverEntryTable  = new List<string>();
        private List<string> _qualityEntryTable = new List<string>();
        private List<string> _qualitySegmentUrlModifiers = new List<string>();
        private List<Segment>  segTable  = new List<Segment>();
        private List<Fragment> fragTable = new List<Fragment>();
        private int segStart  = -1;
        private int fragStart = -1;
        private Media selectedMedia;
        private struct Segment
        {
            public int firstSegment;
            public int fragmentsPerSegment;
        }
        private struct Fragment
        {
            public int   firstFragment;
            public long  firstFragmentTimestamp;
            public int   fragmentDuration;
            public int   discontinuityIndicator;
        }
        private struct Manifest
        {
            public string     bitrate;
            public string     url;
            public XmlElement xml;
        }
        private struct Media
        {
            public string baseUrl;
            public string url;
            public string bootstrapUrl;
            public byte[] bootstrap;
            public byte[] metadata;
        }
        private struct Fragment2Dwnld
        {
            public string url;
            public byte[] data;
            public bool running;
            public bool ready;
        }

        public F4F() // constructor
        {
            this.InitDecoder();
        }

        #region IDisposable Support
        private bool disposedValue = false; // Для определения избыточных вызовов

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    pipeWriter.Close();
                    pipeStream.Close();
                    if (!pipeHandle.IsClosed)
                        pipeHandle.Close();
                }
                pipeStream = null;
                pipeWriter = null;
                pipeHandle = null;

                disposedValue = true;
            }
        }

        ~F4F() {
            Dispose(false);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private void InitDecoder()
        {
            if (this.FLVContinue)
                this.baseTS        = 0;
            else
                this.baseTS        = Constants.INVALID_TIMESTAMP;
            this.audio = false;
            this.negTS             = Constants.INVALID_TIMESTAMP;
            this.video             = false;
            this.prevTagSize       = 4;
            this.tagHeaderLen      = 11;
            this.prevAudioTS       = Constants.INVALID_TIMESTAMP;
            this.prevVideoTS       = Constants.INVALID_TIMESTAMP;
            this.pAudioTagLen      = 0;
            this.pVideoTagLen      = 0;
            this.pAudioTagPos      = 0;
            this.pVideoTagPos      = 0;
            this.prevAVC_Header    = false;
            this.prevAAC_Header    = false;
            this.AVC_HeaderWritten = false;
            this.AAC_HeaderWritten = false;
        }

        public static string NormalizePath(string path)
        {
            string[]     inSegs  = Regex.Split(path, @"(?<!\/)\/(?!\/)");
            List<string> outSegs = new List<string>();
            foreach (string seg in inSegs)
            {
                if (seg == "" || seg == ".")
                    continue;
                if (seg == "..")
                    outSegs.RemoveAt(outSegs.Count-1);
                else
                    outSegs.Add(seg);
            }
            string outPath = string.Join("/", outSegs.ToArray());
            if (path.StartsWith("/")) outPath  = "/" + outPath;
            if (path.EndsWith  ("/")) outPath += "/";
            return outPath;
        }

        private static string DecodeFrom64(string encodedData)
        {
            byte[] encodedDataAsBytes = System.Convert.FromBase64String(encodedData);
            string returnValue;
            returnValue = System.Text.Encoding.ASCII.GetString(encodedDataAsBytes);
            return returnValue;
        }

        private static byte  ReadByte (ref byte[] bytesData, long pos)
        {
            return bytesData[pos];
        }

        private static uint ReadInt24(ref byte[] bytesData, long pos)
        {
            uint iValLo = (uint)(bytesData[pos + 2] + (bytesData[pos + 1] * 256));
            uint iValHi = (uint)(bytesData[pos + 0]);
            uint iVal = iValLo + (iValHi * 65536);
            return iVal;
        }

        private static uint ReadInt32(ref byte[] bytesData, long pos)
        {
            uint iValLo = (uint)(bytesData[pos + 3] + (bytesData[pos + 2] * 256));
            uint iValHi = (uint)(bytesData[pos + 1] + (bytesData[pos + 0] * 256));
            uint iVal = iValLo + (iValHi * 65536);
            return iVal;
        }

        private static long ReadInt64(ref byte[] bytesData, long pos)
        {
            uint iValLo = ReadInt32(ref bytesData, pos + 4);
            uint iValHi = ReadInt32(ref bytesData, pos + 0);
            long iVal = iValLo + (iValHi * 4294967296);
            return iVal;
        }

        private static string GetString(XmlNode xmlObject)
        {
            return xmlObject.InnerText.Trim();
        }

        private static bool isHttpUrl(string url)
        {
            bool boolValue = (url.Length>4) && (url.ToLower().Substring(0, 4) == "http");
            return boolValue;
        }

        private static bool isRtmpUrl(string url)
        {
            return Regex.IsMatch(url, @"^rtm(p|pe|pt|pte|ps|pts|fp):", RegexOptions.IgnoreCase);
        }

        private static void ReadBoxHeader(ref byte[] bytesData, ref long pos, ref string boxType, ref long boxSize)
        {
            boxSize = ReadInt32(ref bytesData, pos);
            boxType = ReadStringBytes(ref bytesData, pos + 4, 4);
            if (boxSize == 1)
            {
                boxSize = ReadInt64(ref bytesData, pos + 8) - 16;
                pos += 16;
            }
            else
            {
                boxSize -= 8;
                pos += 8;
            }
        }

        private static string ReadStringBytes(ref byte[] bytesData, long pos, long len)
        {
            string resultValue = "";
            for (int i = 0; i < len; i++)
            {
                resultValue += (char)bytesData[pos + i];
            }
            return resultValue;
        }

        private static string ReadString(ref byte[] bytesData, ref long pos)
        {
            string resultValue = "";
            int bytesCount = bytesData.Length;
            while ((pos < bytesCount) && (bytesData[pos]!=0))
            {
                resultValue += (char)bytesData[pos++];
            }
            pos++;
            return resultValue;
        }

        private static void WriteByte(ref byte[] bytesData, long pos, byte byteValue)
        {
			bytesData[pos] = byteValue;
        }

        private static void WriteInt24(ref byte[] bytesData, long pos, long intValue)
        {
            bytesData[pos + 0] = (byte)((intValue & 0xFF0000) >> 16);
			bytesData[pos + 1] = (byte)((intValue & 0xFF00) >> 8);
			bytesData[pos + 2] = (byte)( intValue & 0xFF);
        }

        private static void WriteInt32(ref byte[] bytesData, long pos, long intValue)
        {
			bytesData[pos + 0] = (byte)((intValue & 0xFF000000) >> 24);
			bytesData[pos + 1] = (byte)((intValue & 0xFF0000) >> 16);
			bytesData[pos + 2] = (byte)((intValue & 0xFF00) >> 8);
			bytesData[pos + 3] = (byte)( intValue & 0xFF);
        }

        private static void WriteBoxSize(ref byte[] bytesData, long pos, string type, long size)
        {
            string realtype = Encoding.ASCII.GetString(bytesData, (int)pos - 4, 4);
            if (realtype == type) {
                WriteInt32(ref bytesData, pos - 8, size);
            } else {
                WriteInt32(ref bytesData, pos - 8, 0);
                WriteInt32(ref bytesData, pos - 4, size);
            }
        }

        private static void ByteBlockCopy(ref byte[] bytesData1, long pos1, ref byte[] bytesData2, long pos2, long len)
        {
            int len1 = bytesData1.Length;
            int len2 = bytesData2.Length;
            for (int i = 0; i < len; i++)
            {
                if ((pos1 >= len1) || (pos2 >= len2)) break;
                bytesData1[pos1++] = bytesData2[pos2++];
            }
        }

        private static string GetNodeProperty(XmlNode node, string propertyName, string defaultvalue = "")
        {
            bool     found = false;
            string   value = defaultvalue;
            string[] names = propertyName.Split('|');
            for (int i = 0; i < names.Length; i++)
            {
                propertyName = names[i].ToLower();
                // Scpecial 4 caseless check of name
                for (int n=0; n<node.Attributes.Count; n++)
                {
                    if (node.Attributes[n].Name.ToLower() == propertyName)
                    {

                        value = node.Attributes[n].Value;
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }
            return value;
        }

        private string ExtractBaseUrl(string dataUrl)
        {
            string baseUrl = dataUrl;
            if (this.baseUrl != "") 
                baseUrl = this.baseUrl;
            else {
                if (baseUrl.IndexOf("?") > 0) 
                    baseUrl = baseUrl.Substring(0, baseUrl.IndexOf("?"));
                int i = baseUrl.LastIndexOf("/");
                if (i >= 0) baseUrl = baseUrl.Substring(0, baseUrl.LastIndexOf("/"));
                else        baseUrl = "";
            }
            return baseUrl;
        }

        private void WriteFlvTimestamp(ref byte[] frag, long fragPos, long packetTS)
        {
            WriteInt24(ref frag, fragPos + 4,        (packetTS & 0x00FFFFFF));
            WriteByte (ref frag, fragPos + 7, (byte)((packetTS & 0xFF000000) >> 24));
        }

        private int FindFragmentInTabe(int needle)
        {
            return this.fragTable.FindIndex(m => { return m.firstFragment==needle; });
        }

        private void CheckRequestRerutnCode(int statusCode, string statusMsg)
        {
            switch (statusCode) 
            {
                case 403:
                    Program.Quit("<c:Red>ACCESS DENIED! Unable to download manifest. (Request status: <c:Magenta>" + statusMsg + "</c>)");
                    break;

                case 404:
                    Program.Quit("<c:Red>Manifest file not found! (Request status: <c:Magenta>" + statusMsg + "</c>)");
                    break;

                default:
                    if (statusCode != 200)
                        Program.Quit("<c:Red>Unable to download manifest (Request status: <c:Magenta>" + statusMsg + "</c>)");
                    break;
            }            
        }

        private static bool AttrExist(XmlNode node, string name)
        {
            if (node == null) return false;
            return (GetNodeProperty(node, name, "<no>") != "<no>");
        }

        private XmlElement GetManifest(ref string manifestUrl)
        {
            string sDomain = "";
            HTTP cc = new HTTP();
            int statusCode = cc.get(manifestUrl);
            CheckRequestRerutnCode(statusCode, cc.Status);

            if (Program.RegExMatch(@"<r>\s*?<to>(.*?)</to>", cc.responseText, out sDomain))
            {
                if (Program.RegExMatch(@"^.*?://.*?/.*?/(.*)", manifestUrl, out manifestUrl))
                {
                    manifestUrl = sDomain + manifestUrl;
                    statusCode = cc.get(manifestUrl);
                    CheckRequestRerutnCode(statusCode, cc.Status);
                }
            }

            string xmlText = cc.responseText;
            if (xmlText.IndexOf("</") < 0)
                Program.Quit("<c:Red>Error loading manifest: <c:Green>" + manifestUrl);
            XmlDocument xmldoc = new XmlDocument();
            try {
                xmldoc.LoadXml(xmlText);
            } catch {
                if (Regex.IsMatch(xmlText, @"<html.*?<body", RegexOptions.Singleline)) {
                    Program.Quit("<c:Red>Error loading manifest. Url redirected to html page. Check the manifest url.");
                } else {
                    Program.Quit("<c:Red>Error loading manifest. It's no valid xml file.");
                }
            }
            nsMgr = new XmlNamespaceManager(xmldoc.NameTable);
            nsMgr.AddNamespace("ns", xmldoc.DocumentElement.NamespaceURI);
            return xmldoc.DocumentElement;
        }

		// Get manifest and parse - extract medias info and select quality
        private void ParseManifest(string manifestUrl)
        {
			#pragma warning disable 0219
            string baseUrl = "", defaultQuality = ""; int i = 0;
            
            Program.Message("Processing manifest info....");
            XmlElement xml = this.GetManifest(ref manifestUrl);            

            XmlNode node = xml.SelectSingleNode("/ns:manifest/ns:baseURL", nsMgr);
            if (node  != null) baseUrl = node.InnerText.Trim();
            if (baseUrl == "") baseUrl = ExtractBaseUrl(manifestUrl);

            if ((baseUrl == "") && !isHttpUrl(manifestUrl))
                Program.Quit("<c:Red>Not found <c:Magenta>baseURL</c> value in manifest or in parameter <c:White>--urlbase</c>.");

            XmlNodeList nodes = xml.SelectNodes("/ns:manifest/ns:media[@*]", nsMgr);
            Dictionary<string, Manifest> manifests = new Dictionary<string, Manifest>();
            int  countBitrate       = 0;
            bool readChildManifests = false;
            if (nodes.Count > 0) readChildManifests = AttrExist(nodes[0], "href");
            if (readChildManifests)
            {
                foreach (XmlNode ManifestNode in nodes)
                {
                    if (!AttrExist(ManifestNode, "bitrate")) countBitrate++;
                    Manifest manifest = new Manifest();
                    manifest.bitrate = GetNodeProperty(ManifestNode, "bitrate", countBitrate.ToString());
                    manifest.url     = NormalizePath(baseUrl + "/" + GetNodeProperty(ManifestNode, "href"));
                    manifest.xml     = this.GetManifest(ref manifest.url);
                    manifests[manifest.bitrate] = manifest;
                }
            }
            else
            {
                Manifest manifest = new Manifest();
                manifest.bitrate = "0";
                manifest.url     = manifestUrl;
                manifest.xml     = xml;
                manifests[manifest.bitrate] = manifest;
                defaultQuality = manifest.bitrate;
            }
            countBitrate = 0;
            foreach (KeyValuePair<string, Manifest> pair in manifests)
            {
                Manifest manifest = pair.Value; string sBitrate = "";

                // Extract baseUrl from manifest url
                node = manifest.xml.SelectSingleNode("/ns:manifest/ns:baseURL", nsMgr);
                if (node != null) baseUrl = node.InnerText.Trim();
                else              baseUrl = ExtractBaseUrl(manifest.url);
                                
                XmlNodeList MediaNodes = manifest.xml.SelectNodes("/ns:manifest/ns:media", nsMgr);
                foreach (XmlNode stream in MediaNodes) {

                    if (AttrExist(stream, "bitrate"))
                        sBitrate = GetNodeProperty(stream, "bitrate");
                    else if (Int32.Parse(manifest.bitrate)>0)
                        sBitrate = manifest.bitrate;
                    else
                        sBitrate = (countBitrate++).ToString();
                    
                    while (this.media.ContainsKey(sBitrate)) sBitrate = (Int32.Parse(sBitrate)+1).ToString();

                    Media mediaEntry = new Media();
                    mediaEntry.baseUrl = baseUrl;
                    mediaEntry.url     = GetNodeProperty(stream, "url");

                    if (isRtmpUrl(mediaEntry.baseUrl) || isRtmpUrl(mediaEntry.url))
                        Program.Quit("<c:Red>Provided manifest is not a valid HDS manifest. (Media url is <c:Magenta>rtmp</c>?)");

                    if (AttrExist(stream, "bootstrapInfoId"))
                        node = manifest.xml.SelectSingleNode("/ns:manifest/ns:bootstrapInfo[@id='" + GetNodeProperty(stream, "bootstrapInfoId") + "']", nsMgr);
                    else
                        node = manifest.xml.SelectSingleNode("/ns:manifest/ns:bootstrapInfo", nsMgr);
                    if (node != null) 
                    {
                        if (AttrExist(node, "url")) 
                        {
                            mediaEntry.bootstrapUrl = NormalizePath(mediaEntry.baseUrl + "/" + GetNodeProperty(node, "url"));
                            HTTP cc = new HTTP();
                            if (cc.get(mediaEntry.bootstrapUrl) != 200)
                                Program.Quit("<c:Red>Failed to download bootstrap info. (Request status: <c:Magenta>" + cc.Status + "</c>)\n\r<c:DarkCyan>bootstrapUrl: <c:DarkRed>" + mediaEntry.bootstrapUrl);
                            mediaEntry.bootstrap = cc.ResponseData;
                        } 
                        else
                            mediaEntry.bootstrap = System.Convert.FromBase64String(node.InnerText.Trim());
                    }

                    node = manifest.xml.SelectSingleNode("/ns:manifest/ns:media[@url='" + mediaEntry.url + "']/ns:metadata", nsMgr);
                    if (node != null)
                        mediaEntry.metadata = System.Convert.FromBase64String(node.InnerText.Trim());
                    else
                        mediaEntry.metadata = null;
                    this.media[sBitrate] = mediaEntry;
                }
            }

            // Available qualities
            if (this.media.Count < 1) 
                Program.Quit("<c:Red>No media entry found");

            Program.DebugLog("Manifest Entries:\n");
            Program.DebugLog(String.Format(" {0,-8}{1}", "Bitrate", "URL"));
            string sBitrates = " ";
            foreach (KeyValuePair<string, Media> pair in this.media)
            {
                sBitrates += pair.Key + " ";
                Program.DebugLog(String.Format(" {0,-8}{1}", pair.Key, pair.Value.url));
            }

            Program.DebugLog("");
            // Sort quality keys - from high to low
            string[] keys = new string[this.media.Keys.Count];
            this.media.Keys.CopyTo(keys, 0);
            Array.Sort(keys, delegate(string b, string a)
                {
                    int x = 0;
                    int y = 0;
                    if (Int32.TryParse(a, out x) && Int32.TryParse(b, out y)) return x - y;
                    else return a.CompareTo(b);
                });
            string sQuality = defaultQuality;
            // Quality selection
            if (this.media.ContainsKey(this.quality))
                sQuality = this.quality;
            else
            {
                this.quality = this.quality.ToLower();
                switch (this.quality)
                {
                    case "low":
                        this.quality = keys[keys.Length-1]; // last
                        break;
                    case "medium":
                        this.quality = keys[keys.Length/2];
                        break;
                    default:
                        this.quality = keys[0]; // first
                        break;
                }
                int iQuality = Convert.ToInt32(this.quality);
                while (iQuality>=0)
                {
                    if (this.media.ContainsKey(iQuality.ToString()))
                        break;
                    iQuality--;
                }
                sQuality = iQuality.ToString();
            }
            this.selectedMedia = this.media[sQuality];
            int n = sBitrates.IndexOf(sQuality);
            sBitrates = sBitrates.Replace(" " + sQuality + " ", " <c:Cyan>" + sQuality + "</c> ");
            Program.Message("Quality Selection:");
            Program.Message("Available:"+sBitrates);
            Program.Message("Selected : <c:Cyan>" + sQuality.PadLeft(n + sQuality.Length-1));
            this.baseUrl = this.selectedMedia.baseUrl;
            if (!String.IsNullOrEmpty(this.selectedMedia.bootstrapUrl))
            {
                this.bootstrapUrl = this.selectedMedia.bootstrapUrl;
                this.UpdateBootstrapInfo(this.bootstrapUrl);
            }
            else
            {
                long   pos     = 0;
                long   boxSize = 0;
                string boxType = "";
                ReadBoxHeader(ref this.selectedMedia.bootstrap, ref pos, ref boxType, ref boxSize);
                if (boxType == "abst")
                    this.ParseBootstrapBox(ref this.selectedMedia.bootstrap, pos);
                else
                    Program.Quit("<c:Red>Failed to parse bootstrap info.");
            }

            if (this.fragsPerSeg == 0) this.fragsPerSeg = this.fragCount;

            if (this.live) 
            {
                this.threads = 1;
                this.fromTimestamp = -1;
                Program.Message("<c:Magenta>[Live stream]");
            }
			#pragma warning restore 0219
        }

        private void UpdateBootstrapInfo(string bootstrapUrl)
        {
            int     fragNum = this.fragCount;
            int     retries = 0;
            HTTP cc = new HTTP();
            cc.Headers.Add("Cache-Control: no-cache");
            cc.Headers.Add("Pragma: no-cache");
            while ((fragNum == this.fragCount) && (retries < 30))
            {
                long    bootstrapPos = 0;
                long    boxSize      = 0;
                string  boxType      = "";
                Program.DebugLog("Updating bootstrap info, Available fragments: " + this.fragCount.ToString());
                if (cc.get(bootstrapUrl) != 200)
                    Program.Quit("<c:Red>Failed to refresh bootstrap info");
                ReadBoxHeader(ref cc.ResponseData, ref bootstrapPos, ref boxType, ref boxSize);
                if (boxType == "abst")
                    this.ParseBootstrapBox(ref cc.ResponseData, bootstrapPos);
                else
                    Program.Quit("<c:Red>Failed to parse bootstrap info");

                Program.DebugLog("Update complete, Available fragments: " + this.fragCount.ToString());
                if (fragNum == this.fragCount)
                {
                    retries++;
                    Program.Message(String.Format("{0,-80}\r", "<c:DarkCyan>Updating bootstrap info, Retries: " + retries.ToString()));
                    Thread.Sleep(2000); // 2 sec
                }
            }
        }

        private void ParseBootstrapBox(ref byte[] bootstrapInfo, long pos)
        {
			#pragma warning disable 0219
			byte   version          = ReadByte (ref bootstrapInfo, pos);
			int    flags            = (int)ReadInt24(ref bootstrapInfo, pos + 1);
            int    bootstrapVersion = (int)ReadInt32(ref bootstrapInfo, pos + 4);
			byte   Byte             = ReadByte (ref bootstrapInfo, pos + 8);
            int    profile          =   (Byte & 0xC0) >> 6;
            int    update           =   (Byte & 0x10) >> 4;
            if (((Byte & 0x20) >> 5) > 0)
            {
                this.live           = true;
                this.metadata       = false;
            }
            if (update==0) 
            {
                this.segTable .Clear();
                this.fragTable.Clear();
            }
            int    timescale           = (int)ReadInt32(ref bootstrapInfo, pos + 9);
            Int64  currentMediaTime    = ReadInt64(ref bootstrapInfo, 13);
            Int64  smpteTimeCodeOffset = ReadInt64(ref bootstrapInfo, 21);
            pos += 29;
            string movieIdentifier  = ReadString(ref bootstrapInfo, ref pos);
            byte serverEntryCount   = ReadByte(ref bootstrapInfo, pos++);
            for (int i = 0; i < serverEntryCount; i++)
                _serverEntryTable.Add(ReadString(ref bootstrapInfo, ref pos));
            byte qualityEntryCount  = ReadByte(ref bootstrapInfo, pos++);
            for (int i = 0; i < qualityEntryCount; i++)
                _qualityEntryTable.Add(ReadString(ref bootstrapInfo, ref pos));
            string drmData          = ReadString(ref bootstrapInfo, ref pos);
            string metadata         = ReadString(ref bootstrapInfo, ref pos);
            byte segRunTableCount   = ReadByte  (ref bootstrapInfo, pos++);

            long   boxSize = 0;
            string boxType = "";
            Program.DebugLog("Segment Tables:");
            for (int i = 0; i < segRunTableCount; i++)
            {
                Program.DebugLog(String.Format("\nTable {0}:", i + 1));
                ReadBoxHeader(ref bootstrapInfo, ref pos, ref boxType, ref boxSize);
                if (boxType == "asrt")
                    ParseAsrtBox(ref bootstrapInfo, pos);
                pos += boxSize;
            }
            byte fragRunTableCount = ReadByte(ref bootstrapInfo, pos++);
            Program.DebugLog("Fragment Tables:");
            for (int i = 0; i < fragRunTableCount; i++)
            {
                Program.DebugLog(String.Format("\nTable {0}:", i + 1));
                ReadBoxHeader(ref bootstrapInfo, ref pos, ref boxType, ref boxSize);
                if (boxType == "afrt")
                    ParseAfrtBox(ref bootstrapInfo, pos);
                pos += (int)boxSize;
            }
            ParseSegAndFragTable();
			#pragma warning restore 0219
        }

        private void ParseAsrtBox(ref byte[] asrt, long pos)
        {
			#pragma warning disable 0219
            byte version           = ReadByte (ref asrt, pos);
            int  flags             = (int)ReadInt24(ref asrt, pos + 1);
            int  qualityEntryCount = ReadByte (ref asrt, pos + 4);
			#pragma warning restore 0219
            this.segTable.Clear();
            pos += 5;
            for (int i = 0; i < qualityEntryCount; i++)
            {
                this._qualitySegmentUrlModifiers.Add(ReadString(ref asrt, ref pos));
            }
            int segCount           = (int)ReadInt32(ref asrt, pos);
            pos += 4;
            Program.DebugLog(String.Format("{0}:\n\n {1,-8}{2,-10}", "Segment Entries", "Number", "Fragments"));
            for (int i = 0; i < segCount; i++)
            {
                int firstSegment = (int)ReadInt32(ref asrt, pos);
                Segment segEntry = new Segment();
                segEntry.firstSegment        = firstSegment;
                segEntry.fragmentsPerSegment = (int)ReadInt32(ref asrt, pos + 4);
                if ((segEntry.fragmentsPerSegment & 0x80000000) > 0)
                    segEntry.fragmentsPerSegment = 0;
                pos += 8;
                this.segTable.Add(segEntry);
                Program.DebugLog(String.Format(" {0,-8}{1,-10}", segEntry.firstSegment, segEntry.fragmentsPerSegment));
            }
            Program.DebugLog("");
        }

        private void ParseAfrtBox(ref byte[] afrt, long pos)
        {
            this.fragTable.Clear();
			#pragma warning disable 0219
            int version           = ReadByte (ref afrt, pos);
            int flags             = (int)ReadInt24(ref afrt, pos + 1);
            int timescale         = (int)ReadInt32(ref afrt, pos + 4);
            int qualityEntryCount = ReadByte (ref afrt, pos + 8);
			#pragma warning restore 0219
            pos += 9;
            for (int i = 0; i < qualityEntryCount; i++)
            {
                this._qualitySegmentUrlModifiers.Add(ReadString(ref afrt, ref pos));
            }
            int fragEntries = (int)ReadInt32(ref afrt, pos);
            pos += 4;
            Program.DebugLog(String.Format(" {0,-8}{1,-16}{2,-16}{3,-16}", "Number", "Timestamp", "Duration", "Discontinuity"));
            for (int i = 0; i < fragEntries; i++)
            {
                int firstFragment = (int)ReadInt32(ref afrt, pos);
                Fragment fragEntry = new Fragment();
                fragEntry.firstFragment          = firstFragment;
                fragEntry.firstFragmentTimestamp = (long)ReadInt64(ref afrt, pos +  4);
                fragEntry.fragmentDuration       = (int )ReadInt32(ref afrt, pos + 12);
                fragEntry.discontinuityIndicator = 0;
                pos += 16;
                if (fragEntry.fragmentDuration == 0)
                    fragEntry.discontinuityIndicator = ReadByte(ref afrt, pos++);
                this.fragTable.Add(fragEntry);
                Program.DebugLog(String.Format(" {0,-8}{1,-16}{2,-16}{3,-16}", fragEntry.firstFragment, fragEntry.firstFragmentTimestamp, fragEntry.fragmentDuration, fragEntry.discontinuityIndicator));
                if ((this.fromTimestamp > 0) && (fragEntry.firstFragmentTimestamp > 0) && (fragEntry.firstFragmentTimestamp < this.fromTimestamp))
                    this.start = fragEntry.firstFragment+1;
                    //this.start = i+1;
            }
            Program.DebugLog("");
        }

        private void ParseSegAndFragTable()
        {
            if ((this.segTable.Count == 0) || (this.fragTable.Count==0)) return;
            Segment  firstSegment  = this.segTable[0];
            Segment  lastSegment   = this.segTable[this.segTable.Count-1];
            Fragment firstFragment = this.fragTable[0];
            Fragment lastFragment  = this.fragTable[this.fragTable.Count-1];
          
            // Check if live stream is still live
            if ((lastFragment.fragmentDuration == 0) && (lastFragment.discontinuityIndicator == 0))
            {
              this.live = false;
              if (this.fragTable.Count > 0)
                  this.fragTable.RemoveAt(this.fragTable.Count-1);
              if (this.fragTable.Count > 0)
                  lastFragment = this.fragTable[this.fragTable.Count - 1];
            }
          
            // Count total fragments by adding all entries in compactly coded segment table
            bool    invalidFragCount = false;
            Segment prev             = this.segTable[0];
            this.fragCount = prev.fragmentsPerSegment;
            for (int i = 0; i < this.segTable.Count; i++)
            {
                Segment current = this.segTable[i];
                this.fragCount += (current.firstSegment - prev.firstSegment - 1) * prev.fragmentsPerSegment;
                this.fragCount += current.fragmentsPerSegment;
                prev = current;
            }
            if ((this.fragCount & 0x80000000)==0)
                this.fragCount += firstFragment.firstFragment - 1;
            if ((this.fragCount & 0x80000000)!=0)
            {
                this.fragCount   = 0;
                invalidFragCount = true;
            }
            if (this.fragCount < lastFragment.firstFragment)
                this.fragCount = lastFragment.firstFragment;
            Program.DebugLog("fragCount: " + this.fragCount.ToString());

            // Determine starting segment and fragment
            if (this.segStart < 0)
            {
                if (this.live)
                    this.segStart = lastSegment.firstSegment;
                else
                    this.segStart = firstSegment.firstSegment;
                if (this.segStart < 1)
                    this.segStart = 1;
            }
            if (this.fragStart < 0)
            {
                if (this.live && !invalidFragCount)
                    this.fragStart = this.fragCount - 2;
                else
                    this.fragStart = firstFragment.firstFragment - 1;
                if (this.fragStart < 0)
                    this.fragStart = 0;
            }
            Program.DebugLog("segStart : " + this.segStart.ToString());
            Program.DebugLog("fragStart: " + this.fragStart.ToString());
        }

        private void StartNewThread2DownloadFragment()
        {
            if (this.fragNum < 1) return;
            this.threadsRun++;
            for (int i=this.fragNum-1; i < this.fragCount; i++)
            {
                if ((this.fragmentsComplete - this.fragNum) > 5) break;
                if (!this.Fragments2Download[i].running)
                {
                    this.Fragments2Download[i].running = true;
                    HTTP cc = new HTTP(!Program.fproxy);
                    if (cc.get(this.Fragments2Download[i].url) != 200)
                    {
                        this.Fragments2Download[i].running = false;
                        this.Fragments2Download[i].ready   = false;
                        Program.DebugLog("Error download fragment " + (i + 1) + " in thread. Status: " + cc.Status);
                    }
                    else
                    {
                        this.Fragments2Download[i].data = cc.ResponseData;
                        this.Fragments2Download[i].ready = true;
                        this.fragmentsComplete++;
                    }
                    break;
                };
            }
            this.threadsRun--;
        }

        private void ThreadDownload()
        {
            while (this.fragmentsComplete < this.fragCount)
            {
                if ((this.fragCount - this.fragmentsComplete) < this.threads) this.threads = this.fragCount - this.fragmentsComplete;
                if (this.threadsRun < this.threads)
                {
                    Thread t = new Thread(StartNewThread2DownloadFragment);
                    t.IsBackground = true;
                    t.Start();
                }
                Thread.Sleep(300);
            }
        }

        public string GetFragmentUrl(int segNum, int fragNum)
        {
            string fragUrl = this.fragUrlTemplate;
            fragUrl = fragUrl.Replace("<FRAGURL>", this.fragUrl);
            fragUrl = fragUrl.Replace("<SEGNUM>",  segNum.ToString());
            fragUrl = fragUrl.Replace("<FRAGNUM>", fragNum.ToString());
            return fragUrl + this.auth;
        }

        public int GetSegmentFromFragment(int fragN)
        {
            if ((this.segTable.Count == 0) || (this.fragTable.Count==0)) return 1;
            Segment  firstSegment  = this.segTable[0];
            Segment  lastSegment   = this.segTable[this.segTable.Count-1];
            Fragment firstFragment = this.fragTable[0];
            Fragment lastFragment  = this.fragTable[this.fragTable.Count-1];

            if (this.segTable.Count == 1)
                return firstSegment.firstSegment;
            else
            {
                Segment seg, prev  = firstSegment;
                int     end, start = firstFragment.firstFragment;
                for (int i = firstSegment.firstSegment; i <= lastSegment.firstSegment; i++)
                {
                    if (this.segTable.Count >= (i-1))
                        seg = this.segTable[i];
                    else
                        seg = prev;
                    end = start + seg.fragmentsPerSegment;
                    if ((fragN >= start) && (fragN < end))
                        return i;
                    prev  = seg;
                    start = end;
                }
            }
            return lastSegment.firstSegment;
        }

        public void CheckLastTSExistingFile() {
            string sFile = Program.outDir + Program.outFile;
            if (!File.Exists(sFile)) return;
            int b1, b2, b3, b4;
            using (FileStream fs = new FileStream(sFile, FileMode.Open)) {
                if (fs.Length > 600) {
                    fs.Position = fs.Length - 4;
                    b1 = fs.ReadByte();
                    b2 = fs.ReadByte();
                    b3 = fs.ReadByte();
                    b4 = fs.ReadByte();
                    int blockLength = b2 * 256 * 256 + b3 * 256 + b4;
                    if (fs.Length - blockLength > 600) {
                        fs.Position = fs.Length - blockLength;
                        b1 = fs.ReadByte();
                        b2 = fs.ReadByte();
                        b3 = fs.ReadByte();
                        this.fromTimestamp = b1 * 256 * 256 + b2 * 256 + b3;
                        this.FLVHeaderWritten = true;
                        this.FLVContinue      = true;
                        Program.DebugLog("Continue downloading with exiting file from timestamp: " + this.fromTimestamp.ToString());
                    }
                }
            }
        }

		public void DownloadFragments(string manifestUrl)
        {
            HTTP cc = new HTTP(!Program.fproxy);
            this.ParseManifest(manifestUrl);

            this.segNum         = this.segStart;
            this.fragNum        = this.fragStart;
            if (this.start > 0)
            {
                this.segNum     = this.GetSegmentFromFragment(start);
                this.fragNum    = this.start - 1;
                this.segStart   = this.segNum;
                this.fragStart  = this.fragNum;
            }
            string remaining    = "";
            string sDuration    = "";
            int downloaded      = 0;
            this.filesize       = 0;
            bool usedThreads    = (this.threads > 1)  && !this.live;
            int retCode;
            byte[] fragmentData = new byte[0];
            this.lastFrag = this.fragNum;
            if (this.fragNum >= this.fragCount)
                Program.Quit("<c:Red>No fragment available for downloading");

            if (isHttpUrl(this.selectedMedia.url))
                this.fragUrl = this.selectedMedia.url;
            else
                this.fragUrl = NormalizePath(this.baseUrl + "/" + this.selectedMedia.url);

            this.fragmentsComplete = this.fragNum;
            Program.DebugLog("Downloading Fragments:");
            this.InitDecoder();
            DateTime startTime = DateTime.Now;
            if (usedThreads)
            {
                this.Fragments2Download = new Fragment2Dwnld[this.fragCount];
                int curSegNum, curFragNum;
                for (int i = 0; i < this.fragCount; i++) {
                    curFragNum = i + 1;
                    curSegNum  = this.GetSegmentFromFragment(curFragNum);
                    this.Fragments2Download[i].url     = GetFragmentUrl(curSegNum, i + 1);
                    this.Fragments2Download[i].ready   = (curFragNum < this.fragNum); // if start > 0 skip 
                    this.Fragments2Download[i].running = false;
                }
                Thread MainThread = new Thread(ThreadDownload);
                MainThread.IsBackground = true;
                MainThread.Start();
            }
            // --------------- MAIN LOOP DOWNLOADING FRAGMENTS ----------------
            int fragsToDownload = this.fragCount - this.fragNum;
            while (this.fragNum < this.fragCount)
            {
                this.fragNum++;
                this.segNum = this.GetSegmentFromFragment(this.fragNum);

                //if (this.duration > 0) 
                int ts = (int)Math.Round((double)this.currentTS / 1000);
                sDuration = string.Format("<c:DarkCyan>Current timestamp: </c>{0:00}:{1:00}:{2:00} ", ts / 3600, (ts / 60) % 60, ts % 60);

                if (Program.showtime && !this.live)
                {
                    TimeSpan timeRemaining = TimeSpan.FromTicks(DateTime.Now.Subtract(startTime).Ticks * (fragsToDownload - (downloaded + 1)) / (downloaded + 1));
                    remaining = String.Format("<c:DarkCyan>Time remaining: </c>{0:00}<c:Cyan>:</c>{1:00}<c:Cyan>:</c>{2:00}", timeRemaining.Hours, timeRemaining.Minutes, timeRemaining.Seconds);
                }
                Program.Message(String.Format("{0,-46} {1}{2}\r", "Downloading <c:White>" + fragNum + "</c>/" + this.fragCount + " fragments", sDuration, remaining));
                int fragIndex = FindFragmentInTabe(this.fragNum);
                if (fragIndex >= 0)
                    this.discontinuity = this.fragTable[fragIndex].discontinuityIndicator;
                else {
                    // search closest
                    for (int i = 0; i < this.fragTable.Count; i++) {
                        if (this.fragTable[i].firstFragment < this.fragNum) continue;
                        this.discontinuity = this.fragTable[i].discontinuityIndicator;
                        break;
                    }
                }
                if (this.discontinuity != 0) {
                    Program.DebugLog("Skipping fragment " + this.fragNum.ToString() + " due to discontinuity, Type: " + this.discontinuity.ToString());
                    continue;
                }

                if (usedThreads)
                {
                    // use threads
                    DateTime DataTimeOut = DateTime.Now.AddSeconds(200);
                    while (!this.Fragments2Download[this.fragNum - 1].ready)
                    {
                        System.Threading.Thread.Sleep(2000);
                        if (DateTime.Now > DataTimeOut) break;
                    }
                    if (!this.Fragments2Download[this.fragNum - 1].ready)
                    {
                        Program.Quit("<c:Red>Timeout downloading fragment " + this.fragNum + " ".PadLeft(38));
                    }
                    fragmentData = this.Fragments2Download[this.fragNum - 1].data;
                    Program.DebugLog("threads fragment loaded: " + this.Fragments2Download[this.fragNum - 1].url);
                }
                else
                {
                    Program.DebugLog("Fragment Url: " + GetFragmentUrl(this.segNum, this.fragNum));
                    retCode = cc.get(GetFragmentUrl(this.segNum, this.fragNum));
                    if (retCode != 200) {
                        if ((retCode == 403) && !String.IsNullOrEmpty(HTTP.Proxy) && !Program.fproxy)
                        {
                            string msg = "<c:Red>Access denied for downloading fragment <c:White>" + this.fragNum.ToString() + "</c>. (Request status: <c:Magenta>" + cc.Status + "</c>)";
                            msg += "\nTry switch <c:Green>--fproxy</c>.";
                            Program.Quit(msg);
                        }
                        else
                            Program.Quit("<c:Red>Failed to download fragment <c:White>" + this.fragNum.ToString() + "</c>. (Request status: <c:Magenta>" + cc.Status + "</c>)");
                    } else
                        fragmentData = cc.ResponseData;
                }

                WriteFragment(ref fragmentData, this.fragNum);

                /* Resync with latest available fragment when we are left behind due to slow *
                 * connection and short live window on streaming server. make sure to reset  *
                 * the last written fragment.                                                */
                if (this.live && (this.fragNum >= this.fragCount))
                {
                    Program.DebugLog("Trying to resync with latest available fragment");
                    this.UpdateBootstrapInfo(this.bootstrapUrl);
                    //this.fragNum  = this.fragCount - 1;
                    this.lastFrag = this.fragNum;
                }
                downloaded++;
                Program.DebugLog("Downloaded: serment=" + this.segNum + " fragment=" + this.fragNum + "/" + this.fragCount + " lenght: " + fragmentData.Length);
                fragmentData = null;
                if (usedThreads) this.Fragments2Download[this.fragNum - 1].data = null;
                if ((this.duration > 0) && (this.currentDuration >= this.duration)) break;
                if ((this.filesize > 0) && (this.currentFilesize >= this.filesize)) break;

            }
            sDuration = string.Format("\n<c:DarkCyan>Downloaded duration: </c>{0:00}:{1:00}:{2:00} ", this.currentDuration / 3600, (this.currentDuration / 60) % 60, this.currentDuration % 60);
            Program.Message(sDuration);
            Program.DebugLog("\nAll fragments downloaded successfully.");
        }

        private static byte[] ConvertHexStringToByteArray(string hexString)
        {
            byte[] HexAsBytes = new byte[hexString.Length / 2];
            for (int index = 0; index < HexAsBytes.Length; index++)
            {
                string byteValue = hexString.Substring(index * 2, 2);
                HexAsBytes[index] = byte.Parse(byteValue, System.Globalization.NumberStyles.HexNumber);
            }
            return HexAsBytes; 
        }

        private void Write2File(string outFile, ref byte[] data, FileMode fileMode = FileMode.Append, long pos = 0, long datalen = 0)
        {
            if ((datalen == 0) || (datalen > (data.Length - pos))) datalen = data.Length - pos;
            try {
                if (this.play) {
                    Stream stdout = (this.redir2Proc) ? Program.redir2Prog.StandardInput.BaseStream : Console.OpenStandardOutput();
                    stdout.Write(data, (int)pos, (int)datalen);
                    stdout.Flush();
                } else {
                    if (this.pipeWriter == null) {
                        if (usePipe) {
                            if (this.pipeStream != null) this.pipeStream.Close();
                            if (this.pipeHandle != null) this.pipeHandle.Close();
                            this.pipeHandle = NativeMethods.CreateFile(outFile, NativeMethods.GENERIC_WRITE, 0, IntPtr.Zero, NativeMethods.OPEN_EXISTING, NativeMethods.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                            if (this.pipeHandle.IsInvalid) Program.Quit("<c:Red>Cannot create pipe for writting.");
                            this.pipeStream = new FileStream(this.pipeHandle, FileAccess.Write, 4096, true);
                            this.pipeWriter = new BinaryWriter(this.pipeStream);
                        } else {
                            if (this.pipeStream != null) this.pipeStream.Close();
                            if (this.pipeHandle != null) this.pipeHandle.Close();
                            this.pipeStream = new FileStream(outFile, fileMode);
                            this.pipeWriter = new BinaryWriter(this.pipeStream);
                        }
                    }
                    this.pipeWriter.Write(data, (int)pos, (int)datalen);
                    this.pipeWriter.Flush();
                }
                this.currentFilesize += datalen;
            } catch (Exception e) {
                if (Program.ConsolePresent) {
                    Program.DebugLog("Error while writing to file! Message: " + e.Message);
                    Program.DebugLog("Exception: " + e.ToString());
                    Program.Quit("<c:Red>Error while writing to file! <c:DarkCyan>Message: <c:Magenta>" + e.Message);
                }
            }
        }

        private void WriteFlvHeader(string outFile, bool audio = true, bool video = true)
        {
 //           if (usePipe) return;
            this.filesize = 0;
            byte[] flvHeader = ConvertHexStringToByteArray("464c5601050000000900000000");
            if (!video || !audio)
                if (audio & !video)
                    flvHeader[4] = 0x04;
                else if (video & !audio)
                    flvHeader[4] = 0x01;

            this.Write2File(outFile, ref flvHeader, FileMode.Create);
            if (this.metadata) this.WriteMetadata(outFile);

            this.FLVHeaderWritten = true;
        }

        private void WriteMetadata(string outFile)
        {
            if ((this.selectedMedia.metadata!=null) && (this.selectedMedia.metadata.Length>0))
            {
                int mediaMetadataSize = this.selectedMedia.metadata.Length;
                byte[] metadata  = new byte[this.tagHeaderLen + mediaMetadataSize + 4];
                WriteByte (ref metadata, 0, Constants.SCRIPT_DATA);
                WriteInt24(ref metadata, 1, mediaMetadataSize);
                WriteInt24(ref metadata, 4, 0);
                WriteInt32(ref metadata, 7, 0);
                ByteBlockCopy(ref metadata, this.tagHeaderLen, ref this.selectedMedia.metadata, 0, mediaMetadataSize);
                WriteByte (ref metadata, this.tagHeaderLen + mediaMetadataSize - 1, 0x09);
                WriteInt32(ref metadata, this.tagHeaderLen + mediaMetadataSize, this.tagHeaderLen + mediaMetadataSize);
                this.Write2File(outFile, ref metadata);
            }
        }

        private void WriteFragment(ref byte[] data, int fragNum)
        {
            if (data == null) return;
            if (!this.FLVHeaderWritten) {
                this.InitDecoder();
                DecodeFragment(ref data, true);
                WriteFlvHeader(Program.outDir + Program.outFile, this.audio, this.video);
                if (this.metadata) WriteMetadata(Program.outDir + Program.outFile);
                this.InitDecoder();
            }
            DecodeFragment(ref data);
        }

        bool VerifyFragment(ref byte[] frag)
        {
          string boxType  = "";
          long   boxSize  = 0;
          long   fragPos  = 0;

          /* Some moronic servers add wrong boxSize in header causing fragment verification *
           * to fail so we have to fix the boxSize before processing the fragment.          */
          while (fragPos < frag.Length)
          {
              ReadBoxHeader(ref frag, ref fragPos, ref boxType, ref boxSize);
              if (boxType == "mdat")
              {
                  if ((fragPos + boxSize) > frag.Length)
                  {
                      boxSize = frag.Length - fragPos;
                      WriteBoxSize(ref frag, fragPos, boxType, boxSize);
                  }
                  return true;
              }
              fragPos += boxSize;
          }
          return false;
        }

        private void DecodeFragment(ref byte[] frag, bool notWrite=false)
        {
            if (frag == null) return;
            string outFile   = Program.outDir + Program.outFile;
            string boxType   = "";
            long   boxSize   = 0;
            long   fragLen   = frag.Length;
            long   fragPos   = 0;
            long   packetTS  = 0;
            long   lastTS    = 0;
            long   fixedTS   = 0;
            int    AAC_PacketType = 0;
            int    AVC_PacketType = 0;

            if (!VerifyFragment(ref frag))
            {
                Program.Message("<c:Red>Skipping failed fragment " + fragNum + " ".PadLeft(48));
                return;
            };
            
            while (fragPos < fragLen)
            {
                ReadBoxHeader(ref frag, ref fragPos, ref boxType, ref boxSize);
                if (boxType == "mdat") break;
                fragPos += boxSize;
            }
            Program.DebugLog(String.Format("Fragment {0}:\n", fragNum));
            Program.DebugLog(String.Format(this.format + "{4,-16}", "Type", "CurrentTS", "PreviousTS", "Size", "Position"));
            while (fragPos < fragLen)
            {
                int packetType =      ReadByte (ref frag, fragPos    );
                int packetSize = (int)ReadInt24(ref frag, fragPos + 1);
                packetTS       =      ReadInt24(ref frag, fragPos + 4);
                packetTS       =      (uint)packetTS | (uint)(ReadByte(ref frag, fragPos + 7) << 24);

                if ((packetTS & 0x80000000) == 0) packetTS &= 0x7FFFFFFF;
                long totalTagLen = this.tagHeaderLen + packetSize + this.prevTagSize;

                // Try to fix the odd timestamps and make them zero based
                this.currentTS = packetTS;
                lastTS = this.prevVideoTS >= this.prevAudioTS ? this.prevVideoTS : this.prevAudioTS;
                fixedTS   = lastTS + Constants.FRAMEFIX_STEP;
                if ((this.baseTS == Constants.INVALID_TIMESTAMP) && ((packetType == Constants.AUDIO) || (packetType == Constants.VIDEO)))
                    this.baseTS = packetTS;
                if ((this.baseTS > 1000) && (packetTS >= this.baseTS)) 
                    packetTS -= this.baseTS;

                if (lastTS != Constants.INVALID_TIMESTAMP) 
                {
                    long timeShift = packetTS - lastTS;
                    if (timeShift > this.fixWindow) 
                    {
                        Program.DebugLog(String.Format("Timestamp gap detected: PacketTS={0} LastTS={1} Timeshift={2}", packetTS, lastTS, timeShift));
                        this.baseTS += timeShift - Constants.FRAMEFIX_STEP;
                        packetTS = fixedTS;
                    } 
                    else 
                    {
                        lastTS = packetType == Constants.VIDEO ? this.prevVideoTS : this.prevAudioTS;
                        if (packetTS < (lastTS - this.fixWindow))
                        {
                            if ((this.negTS != Constants.INVALID_TIMESTAMP) && ((packetTS + this.negTS) < (lastTS - this.fixWindow)))
                                this.negTS = Constants.INVALID_TIMESTAMP;
                            if (this.negTS == Constants.INVALID_TIMESTAMP)
                            {
                                this.negTS = (int)(fixedTS - packetTS);
                                Program.DebugLog(String.Format("Negative timestamp detected: PacketTS={0} LastTS={1} NegativeTS={2}", packetTS, lastTS, this.negTS));
                                packetTS = fixedTS;
                            }
                            else
                            {
                                if ((packetTS + this.negTS) <= (lastTS + this.fixWindow)) 
                                    packetTS += this.negTS;
                                else 
                                {
                                  this.negTS = (int)(fixedTS - packetTS);
                                  Program.DebugLog(String.Format("Negative timestamp override: PacketTS={0} LastTS={1} NegativeTS={2}", packetTS, lastTS, this.negTS));
                                  packetTS = fixedTS;
                                }
                            }
                        }
                    }
                }
                if (packetTS != this.currentTS) WriteFlvTimestamp(ref frag, fragPos, packetTS);

                switch (packetType)
                {
                    case Constants.AUDIO:
                        if (packetTS > this.prevAudioTS - this.fixWindow)
                        {
                            int FrameInfo = ReadByte(ref frag, fragPos + this.tagHeaderLen);
                            int CodecID = (FrameInfo & 0xF0) >> 4;
                            if (CodecID == Constants.CODEC_ID_AAC)
                            {
                                AAC_PacketType = ReadByte(ref frag, fragPos + this.tagHeaderLen + 1);
                                if (AAC_PacketType == Constants.AAC_SEQUENCE_HEADER) 
                                {
                                    if (this.AAC_HeaderWritten)
                                    {
                                        Program.DebugLog("Skipping AAC sequence header");
                                        Program.DebugLog(String.Format(this.format, "AUDIO", packetTS, this.prevAudioTS, packetSize));
                                        break;
                                    }
                                    else
                                    {
                                        Program.DebugLog("Writing AAC sequence header");
                                        this.AAC_HeaderWritten = true;
                                    }
                                } 
                                else if (!this.AAC_HeaderWritten) 
                                {
                                    Program.DebugLog("Discarding audio packet received before AAC sequence header");
                                    Program.DebugLog(String.Format(this.format, "AUDIO", packetTS, this.prevAudioTS, packetSize));
                                    break;
                                }
                            }
                            if (packetSize > 0)
                            {
                                // Check for packets with non-monotonic audio timestamps and fix them
                                if (!((CodecID == Constants.CODEC_ID_AAC) && ((AAC_PacketType == Constants.AAC_SEQUENCE_HEADER) || this.prevAAC_Header)))
                                {
                                    if ((this.prevAudioTS != Constants.INVALID_TIMESTAMP) && (packetTS <= this.prevAudioTS))
                                    {
                                        Program.DebugLog("Fixing audio timestamp");
                                        Program.DebugLog(String.Format(this.format, "AUDIO", packetTS, this.prevAudioTS, packetSize));
                                        packetTS += (Constants.FRAMEFIX_STEP / 5) + (this.prevAudioTS - packetTS);
                                        this.WriteFlvTimestamp(ref frag, fragPos, packetTS);
                                    }
                                }
                                if ((CodecID == Constants.CODEC_ID_AAC) && (AAC_PacketType == Constants.AAC_SEQUENCE_HEADER))
                                    this.prevAAC_Header = true;
                                else
                                    this.prevAAC_Header = false;

                                if (!notWrite) if ((this.currentTS > this.fromTimestamp) || !this.FLVContinue)
                                        this.Write2File(outFile, ref frag, FileMode.Append, fragPos, totalTagLen);
                                    
                                Program.DebugLog(String.Format(this.format, "AUDIO", packetTS, this.prevAudioTS, packetSize));
                                this.prevAudioTS = packetTS;
                                pAudioTagLen = totalTagLen;
                            }
                            else
                            {
                                Program.DebugLog("Skipping small sized audio packet");
                                Program.DebugLog(String.Format(this.format, "AUDIO", packetTS, this.prevAudioTS, packetSize));
                            }
                        }
                        else
                        {
                            Program.DebugLog("Skipping audio packet in fragment fragNum");
                            Program.DebugLog(String.Format(this.format, "AUDIO", packetTS, this.prevAudioTS, packetSize));
                        }
                        if (!this.audio) this.audio = true;
                        break;
                    
                    case Constants.VIDEO:
                        if (packetTS > this.prevVideoTS - this.fixWindow)
                        {
                            int FrameInfo = ReadByte(ref frag, fragPos + this.tagHeaderLen);
                            int FrameType = (FrameInfo & 0xF0) >> 4;
                            int CodecID   =  FrameInfo & 0x0F;
                            if (FrameType == Constants.FRAME_TYPE_INFO)
                            {
                                Program.DebugLog("Skipping video info frame");
                                Program.DebugLog(String.Format(this.format, "VIDEO", packetTS, this.prevVideoTS, packetSize));
                                break;
                            }
                            if (CodecID == Constants.CODEC_ID_AVC)
                            {
                                AVC_PacketType = ReadByte(ref frag, fragPos + this.tagHeaderLen + 1);
                                if (AVC_PacketType == Constants.AVC_SEQUENCE_HEADER)
                                {
                                    if (this.AVC_HeaderWritten)
                                    {
                                        Program.DebugLog("Skipping AVC sequence header");
                                        Program.DebugLog(String.Format(this.format, "VIDEO", packetTS, this.prevVideoTS, packetSize));
                                        break;
                                    }
                                    else
                                    {
                                        Program.DebugLog("Writing AVC sequence header");
                                        this.AVC_HeaderWritten = true;
                                    }
                                }
                                else if (!this.AVC_HeaderWritten)
                                {
                                    Program.DebugLog("Discarding video packet received before AVC sequence header");
                                    Program.DebugLog(String.Format(this.format, "VIDEO", packetTS, this.prevVideoTS, packetSize));
                                    break;
                                }
                            }
                            if (packetSize > 0)
                            {
                                if (Program.debug)
                                {
                                    long pts = packetTS;
                                    if ((CodecID == Constants.CODEC_ID_AVC) && (AVC_PacketType == Constants.AVC_NALU))
                                    {
                                        long cts = ReadInt24(ref frag, fragPos + this.tagHeaderLen + 2);
                                        cts = (cts + 0xff800000) ^ 0xff800000;
                                        pts = packetTS + cts;
                                        if (cts != 0) Program.DebugLog(String.Format("DTS: {0} CTS: {1} PTS: {2}", packetTS, cts, pts));
                                    }
                                }

                                // Check for packets with non-monotonic video timestamps and fix them
                                if (!((CodecID == Constants.CODEC_ID_AVC) && ((AVC_PacketType == Constants.AVC_SEQUENCE_HEADER) || (AVC_PacketType == Constants.AVC_SEQUENCE_END) || this.prevAVC_Header)))
                                {
                                    if ((this.prevVideoTS != Constants.INVALID_TIMESTAMP) && (packetTS <= this.prevVideoTS))
                                    {
                                        Program.DebugLog("Fixing video timestamp");
                                        Program.DebugLog(String.Format(this.format, "VIDEO", packetTS, this.prevVideoTS, packetSize));
                                        packetTS += (Constants.FRAMEFIX_STEP / 5) + (this.prevVideoTS - packetTS);
                                        this.WriteFlvTimestamp(ref frag, fragPos, packetTS);
                                    }
                                }
                                if ((CodecID == Constants.CODEC_ID_AVC) && (AVC_PacketType == Constants.AVC_SEQUENCE_HEADER))
                                    this.prevAVC_Header = true;
                                else
                                    this.prevAVC_Header = false;

                                if (!notWrite) if ((this.currentTS > this.fromTimestamp) || !this.FLVContinue)
                                        this.Write2File(outFile, ref frag, FileMode.Append, fragPos, totalTagLen);

                                Program.DebugLog(String.Format(this.format, "VIDEO", packetTS, this.prevVideoTS, packetSize));
                                this.prevVideoTS = packetTS;
                                pVideoTagLen = totalTagLen;
                            }
                            else
                            {
                                Program.DebugLog("Skipping small sized video packet");
                                Program.DebugLog(String.Format(this.format, "VIDEO", packetTS, this.prevVideoTS, packetSize));
                            }
                        }
                        else
                        {
                            Program.DebugLog("Skipping video packet in fragment fragNum");
                            Program.DebugLog(String.Format(this.format, "VIDEO", packetTS, this.prevVideoTS, packetSize));
                        }
                        if (!this.video) this.video = true;
                        break;
                    
                    case Constants.SCRIPT_DATA:
                        break;
                    
                    default:
                        if ((packetType == 10) || (packetType == 11))
                            Program.Quit("<c:Red>This stream is encrypted with <c:Magenta>Akamai DRM</c>. Decryption of such streams isn't currently possible with this program. Not yet.");
                        else if ((packetType == 40) || (packetType == 41))
                            Program.Quit("<c:Red>This stream is encrypted with <c:Magenta>FlashAccess DRM</c>. Decryption of such streams isn't currently possible with this program. Not yet.");
                        else
                            Program.Quit("<c:Red>Unknown packet type <c:Magenta>" + packetType + "</c> encountered! Encrypted fragments can't be recovered. I'm so sorry.");
                        break;
                }
                fragPos += totalTagLen;
            }
            this.currentDuration = (int)Math.Round((double)packetTS / 1000);
        }
    }

    public static class HttpWebResponseExt {
        public static HttpWebResponse GetResponseNoException(HttpWebRequest req) {
            try {
                return (HttpWebResponse)req.GetResponse();
            } catch (WebException we) {
                Program.DebugLog("Error downloading the link: " + req.RequestUri + "\r\nException: " + we.Message);
                var resp = we.Response as HttpWebResponse;
                if (resp == null)
                    Program.Quit("<c:Red>" + we.Message + " (Request status: <c:Magenta>" + we.Status + "</c>)");
                    //throw;
                return resp;
            }
        }
    }

}
