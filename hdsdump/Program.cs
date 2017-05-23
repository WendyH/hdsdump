/* By WendyH. GNU GPL License version 3
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace hdsdump
{
    class Program
    {
        public static bool   debug        = false;
        public static string logfile      = "hdsdump.log";
        public static string manifestUrl  = "";
        public static string outDir       = "";
        public static string outFile      = "hdsdump.flv";
        public static string auth         = "";
        public static string baseUrl      = "";
        public static string quality      = "";
        public static string postData     = "";
        public static string headers      = "";
        public static string lang         = "";
        public static string alt          = "";
        public static bool   play         = false;
        public static uint   start        = 0;
        public static int    threads      = 8;
        public static uint   duration     = 0;
        public static uint   filesize     = 0;
        public static uint   fromTimestamp= 0;
        public static bool   fproxy       = false;
        public static bool   showtime     = false;
        public static bool   waitkey      = false;
        public static bool   showmlink    = false;
        public static bool   fcontinue    = false;
        public static bool   redirect2prc = false;
        public static bool   verbose      = false;
        public static bool   testalt      = false;
        
        public static System.Diagnostics.Stopwatch sw;
        public static System.Diagnostics.Process redir2Prog;

        private static byte[] sessionKey;

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
            Quit("<c:Red>" + ex.Message);
        }

        public static void Main(string[] args) {
            System.Net.ServicePointManager.UseNagleAlgorithm = true;
            System.Net.ServicePointManager.CheckCertificateRevocationList = false;
            System.Net.ServicePointManager.DefaultConnectionLimit = 20;
            System.Net.ServicePointManager.Expect100Continue = false;
            try {
                AppDomain.CurrentDomain.UnhandledException += (sender, e) => FatalExceptionObject(e.ExceptionObject);

                redirect2prc = Check4Redirect2Process(ref args);
			    CLI cli = new CLI(args);
                if (cli.ChkParam("waitkey"  )) waitkey = true;
                if (cli.ChkParam("nowaitkey")) waitkey = false;
                if (cli.ChkParam("help"     )) { cli.DisplayHelp(); Quit(""); }
                if (cli.ChkParam("filesize" )) uint.TryParse(cli.GetParam("filesize"), out filesize);
                if (cli.ChkParam("threads"  )) int .TryParse(cli.GetParam("threads" ), out threads );
                if (cli.ChkParam("start"    )) uint.TryParse(cli.GetParam("start"   ), out start   );
                if (cli.ChkParam("auth"     )) auth        = "?" + cli.GetParam("auth");
                if (cli.ChkParam("headers"  )) headers     = cli.GetParam("headers" );
                if (cli.ChkParam("urlbase"  )) baseUrl     = cli.GetParam("urlbase" );
                if (cli.ChkParam("quality"  )) quality     = cli.GetParam("quality" );
                if (cli.ChkParam("manifest" )) manifestUrl = cli.GetParam("manifest");
                if (cli.ChkParam("outdir"   )) outDir      = cli.GetParam("outdir"  );
                if (cli.ChkParam("outfile"  )) outFile     = cli.GetParam("outfile" );
                if (cli.ChkParam("logfile"  )) logfile     = cli.GetParam("logfile" );
                if (cli.ChkParam("skip"     )) fromTimestamp = GetTimestampFromString(cli.GetParam("skip"));
                if (cli.ChkParam("duration" )) duration      = GetTimestampFromString(cli.GetParam("duration"));
                if (cli.ChkParam("debug"    )) debug     = true;
                if (cli.ChkParam("play"     )) play      = true;
                if (cli.ChkParam("showtime" )) showtime  = true;
                if (cli.ChkParam("showmlink")) showmlink = true;
                if (cli.ChkParam("fproxy"   )) fproxy    = true;
                if (cli.ChkParam("continue" )) fcontinue = true;
                if (cli.ChkParam("verbose"  )) verbose   = true;
                if (cli.ChkParam("testalt"  )) testalt   = true;
                if (cli.ChkParam("postdata" )) postData  = cli.GetParam("postdata");
                if (cli.ChkParam("referer"  )) HTTP.Referer       = cli.GetParam("referer"  );
                if (cli.ChkParam("cookies"  )) HTTP.Cookies       = cli.GetParam("cookies"  );
                if (cli.ChkParam("useragent")) HTTP.Useragent     = cli.GetParam("useragent");
                if (cli.ChkParam("username" )) HTTP.Username      = cli.GetParam("username" );
                if (cli.ChkParam("password" )) HTTP.Password      = cli.GetParam("password" );
                if (cli.ChkParam("proxy"    )) HTTP.Proxy         = cli.GetParam("proxy"    );
                if (cli.ChkParam("proxyuser")) HTTP.ProxyUsername = cli.GetParam("proxyuser");
                if (cli.ChkParam("proxypass")) HTTP.ProxyPassword = cli.GetParam("proxypass");
                if (cli.ChkParam("osproxy"  )) HTTP.UseSystemProxy = true;
                if (cli.ChkParam("adkey"    )) sessionKey = AkamaiDecryptor.Unhexlify(cli.GetParam("adkey"));
                if (cli.ChkParam("lang"     )) lang = cli.GetParam("lang");
                if (cli.ChkParam("alt"      )) alt  = cli.GetParam("alt" );

                if (HTTP.Referer == "") {
                    var m = Regex.Match(manifestUrl, @"^(.*?://.*?/)");
                    if (m.Success) HTTP.Referer = m.Groups[1].Value;
                }

                if (!string.IsNullOrEmpty(headers)) {
                    foreach(string header in headers.Split(new char[] { '|', '$' })) {
                        Match m = Regex.Match(header, "(.*?):(.*)");
                        if (m.Success) {
                            string name  = m.Groups[1].Value;
                            string value = m.Groups[2].Value;
                            switch (name.ToLower()) {
                                case "referer"   : HTTP.Referer   = value; break;
                                case "useragent" : 
                                case "user-agent": HTTP.Useragent = value; break;
                                default: HTTP.Headers.Set(name, value); break;
                            }
                        }
                    }
                }

                String strVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                ShowHeader("HDSdump by WendyH v<c:White>" + strVersion);

                //if (showmlink) Program.Message("Manifest: <c:Green>" + manifest);

                if (manifestUrl == "") Program.Quit("<c:Red>Please specify the manifest. (switch '<c:White>-h</c>' or '<c:White>--help</c>' for help message)");

                if (showtime) ShowTimeElapsed("", true);

                Check4KnownLinks(ref manifestUrl);

                bool usePipe = outFile.IndexOf(@"\\.\pipe\") == 0;
                cli.Params["threads"] = threads.ToString();
                cli.Params["outfile"] = outFile;
                cli.EchoSetsParameters();

                if (!cli.ChkParam("oldmethod")) {
                    HDSDumper HdsDumper = new HDSDumper();
                    HdsDumper.FLVFile.outFile    = outDir + outFile;
                    HdsDumper.FLVFile.redir2Proc = redirect2prc;
                    HdsDumper.FLVFile.play       = play;
                    HdsDumper.FLVFile.usePipe    = usePipe;
                    HdsDumper.Downloader.maxThreads = threads;
                    HdsDumper.duration = duration;
                    HdsDumper.filesize = filesize;
                    HdsDumper.start    = start;
                    HdsDumper.auth     = auth;
                    HdsDumper.baseUrl  = baseUrl;
                    HdsDumper.quality  = quality;
                    HdsDumper.postData = postData;
                    HdsDumper.lang     = lang;
                    HdsDumper.alt      = alt;
                    HdsDumper.testalt  = testalt;
                    HdsDumper.fromTimestamp = fromTimestamp;
                    HdsDumper.sessionKey = sessionKey;

                    if (ConsolePresent) {
                        Console.CancelKeyPress += delegate {
                            HdsDumper?.FixFileMetadata();
                        };
                    }

                    try {
                        Message("Processing manifest info...");
                        HdsDumper.StartDownload(manifestUrl);
                    } catch (Exception e) {
                        Message("<c:Red>" + e.Message);
                    } finally {
                        Message("");
                        HdsDumper.FixFileMetadata();
                    }
                    if (!string.IsNullOrEmpty(HdsDumper.Status)) {
                        Quit(HdsDumper.Status);
                    } else 
                        Quit("Done.");
                }

                // ========== OLD METHOD ==========
                F4F f4f = new F4F();
                // Disable metadata if it invalidates the stream duration
                if ((f4f.fromTimestamp > 0) || (start > 0) || (duration > 0) || (filesize > 0))
                    f4f.metadata = false;

                f4f.usePipe = usePipe;
                f4f.fromTimestamp = fromTimestamp;
                f4f.play       = play;
                //f4f.threads    = threads;
                f4f.duration   = (int)duration;
                f4f.filesize   = (int)filesize;
                f4f.redir2Proc = redirect2prc;
                f4f.start      = (int)start;
                f4f.ad.sessionKey = sessionKey;
                f4f.auth     = auth;
                f4f.baseUrl  = baseUrl;
                f4f.quality  = quality;
                f4f.PostData = postData;
                f4f.Lang     = lang;
                f4f.alt      = alt;
                if (fcontinue)
                    f4f.CheckLastTSExistingFile();

                f4f.DownloadFragments(manifestUrl);
                Program.Quit("Done.");

            } catch (Exception huh) {
                FatalExceptionObject(huh);
            }

        }

        private static uint GetTimestampFromString(string time) {
            int d=0, h=0, m=0, s=0, ms = 0;
            if (time.IndexOf(':') > 0) {
                TryRegexInt(time, @".*\d+:\d+[\.,](\d+)", ref ms);
                TryRegexInt(time, @".*\d+:(\d+)"    , ref s);
                TryRegexInt(time, @".*(\d+):\d+"    , ref m);
                TryRegexInt(time, @".*(\d+):\d+:\d+", ref h);
            } else if (Regex.IsMatch(time.Trim(), @"^\d+$")) {
                s = Int32.Parse(time.Trim());
            } else {
                TryRegexInt(time, @"(\d+)s", ref s);
                TryRegexInt(time, @"(\d+)m", ref m);
                TryRegexInt(time, @"(\d+)h", ref h);
                TryRegexInt(time, @"(\d+)d", ref d);
            }
            TimeSpan ts = new TimeSpan(d, h, m, s, ms);
            return (uint)ts.TotalMilliseconds;
        }

        private static bool TryRegexInt(string text, string pattern, ref int val, RegexOptions options = RegexOptions.None) {
            var match = Regex.Match(text, pattern);
            if (match.Success)
                val = Int32.Parse(match.Groups[1].Value);
            return match.Success;
        }

        private static void Check4KnownLinks(ref string sLink) {
            if (RegExMatch(@"radio-canada.ca.*?[Mm]edia[-=](\d+)", sLink, out string id)) {
                sLink = @"http://api.radio-canada.ca/validationMedia/v1/Validation.html?connectionType=broadband&output=json&multibitrate=true&deviceType=flashhd&appCode=medianet&idMedia=" + id + "&claims=null";
                string text = HTTP.GET(sLink);
                if (RegExMatch("\"url\":\"(.*?)\"", text, out manifestUrl)) {
                    DebugLog("manifest: " + manifestUrl);
                    sLink = manifestUrl;
                }
            } else if (Regex.IsMatch(sLink, "(moon.hdkinoteatr.com|moonwalk.\\w+)/\\w+/")) {
                GetLink_Moonwalk(sLink);
            } else if (Regex.IsMatch(sLink, "/megogo.net/")) {
                GetLink_Megogo(sLink);
            } else if (Regex.IsMatch(sLink, "/rutube.ru/")) {
                GetLink_Rutube(sLink);
            } else if (Regex.IsMatch(sLink, "spbtv.online/channels/")) {
                string html = HTTP.GET(sLink);
                Match m = Regex.Match(html, "channelId\\s*:\\s*'(.*?)'");
                if (m.Success) {
                    string channelId = m.Groups[1].Value;
                    HTTP.Referer = "http://spbtv.online/vplayer/last/GrindPlayer.swf";
                    string data = HTTP.GET("http://tv3.spr.spbtv.com/v1/channels/" + channelId + "/stream?protocol=hds");
                    m = Regex.Match(data, "\"url\":\"(.*?)\"");
                    if (m.Success) {
                        manifestUrl = Regex.Unescape(m.Groups[1].Value);
                    }
                }
            }
        }

        private static void GetLink_Rutube(string sLink) {
            string sData, sID = ""; Match m;
            m = Regex.Match(sLink, "/(\\d+)(/|$|\\?)");
            if (m.Success) sID = m.Groups[1].Value;
            else Quit("No id in rutube link");
            sLink = "http://rutube.ru/play/embed/" + sID;
            HTTP.Referer = sLink;
            sData = HTTP.GET("http://rutube.ru/api/play/options/" + sID + "/?format=json&sqr4374_compat=1&no_404=true&referer="+ Uri.EscapeDataString(sLink) + "&_="+ new Random());
            m = Regex.Match(sData, "(http[^\">']+f4m[^\"}>']+)");
            if (!m.Success) Quit("No f4m source for rutube video");
            manifestUrl = m.Groups[1].Value;
        }

        private static void GetLink_Megogo(string sLink) {
            string sData, sID=""; Match m;
            HTTP.Referer = sLink;
            m = Regex.Match(sLink, "/(\\d+)-");
            if (m.Success) sID = m.Groups[1].Value;
            else Quit("No id in megogo link");
            sLink = "http://megogo.net/b/info/?&i="+sID+"&s=0&e=0&p=0&t=0&m=-1&l=ru&d=andr_iphone_ipad_winph&playerMode=html5&preview=0&h="+sLink;
            sData = HTTP.GET(sLink);
            m = Regex.Match(sData, "<src>(.*?)</");
            if (!m.Success) Quit("No src for megogo video");
            manifestUrl = m.Groups[1].Value.Replace("playlist.m3u8", "manifest.f4m");
            if (string.IsNullOrEmpty(manifestUrl)) {
                m = Regex.Match(sData, "<forbidden>(.*?)</");
                if (m.Success) Quit(m.Groups[1].Value);
            }
        }

        private static void GetLink_Moonwalk(string sLink) {
            string sHtml, sData, sPost; Match m;

            HTTP.Referer = sLink;
            sLink = sLink.Replace("moonwalk.co", "moonwalk.cc");
            sLink = sLink.Replace("moonwalk.pw", "moonwalk.cc");
            sHtml = HTTP.GET(sLink);
            if (Regex.IsMatch(sHtml, "<iframe[^>]+src=\"(http.*?)\"", RegexOptions.Singleline)) {
                sLink = sLink.Replace("moonwalk.co", "moonwalk.cc");
                sLink = sLink.Replace("moonwalk.pw", "moonwalk.cc");
                sHtml = HTTP.GET(sLink);
            }
            m = Regex.Match(sHtml, "\"csrf-token\"\\s+content=\"(.*?)\"");
            if (m.Success) HTTP.Headers.Set("X-CSRF-Token", m.Groups[1].Value);
            foreach (Match match in Regex.Matches(sHtml, "Set-Cookie:\\s*([^;]+)")) {
                HTTP.Cookies += " " + match.Groups[1].Value + ";";
            }
            string server = Regex.Match(sLink, "^(.*?//.*?)/").Groups[1].Value;
            HTTP.Headers.Set("Origin", server);
            HTTP.Headers.Set("X-Requested-With", "XMLHttpRequest");
            HTTP.Headers.Set("Pragma", "no-cache");
            m = Regex.Match(sHtml, "ajaxSetup\\([^)]+headers:(.*?)}", RegexOptions.Singleline);
            if (m.Success) {
                foreach (Match match in Regex.Matches(m.Groups[1].Value, "[\"']([\\w-_]+)[\"']\\s*?:\\s*?[\"'](\\w+)[\"']")) {
                    HTTP.Headers.Set(match.Groups[1].Value, match.Groups[2].Value);
                }
            }
            m = Regex.Match(sHtml, "/new_session.*?(\\w+)\\s*?=\\s*?\\{(.*?)\\}", RegexOptions.Singleline);
            if (!m.Success) Quit("Not found new_session parameters");
            sPost = m.Groups[2].Value.Replace("\n", "").Replace(" ", "").Replace("'", "");
            sPost = sPost.Replace(':', '=').Replace(',', '&').Replace(':', '=').Replace(':', '=').Replace("condition_detected?1=", "");
            foreach (Match match in Regex.Matches(sPost, ".=(\\w+)")) {
                m = Regex.Match(sHtml, "var\\s" + match.Groups[1].Value + "\\s*=\\s*['\"](.*?)['\"]");
                if (m.Success)
                    sPost = sPost.Replace("=" + match.Groups[1].Value, "=" + m.Groups[1].Value);
            }
            foreach (Match match in Regex.Matches(sHtml, "post_method\\.(\\w+)\\s*=\\s*(\\w+)")) {
                m = Regex.Match(sHtml, "var\\s" + match.Groups[1].Value + "\\s*=\\s*['\"](.*?)['\"]");
                if (m.Success)
                    sPost += "&" + match.Groups[1].Value + "=" + m.Groups[1].Value;
            }
            foreach (Match match in Regex.Matches(sHtml, "\\['(\\w+)'\\]\\s*=\\s*'(.*?)'")) {
                sPost += "&" + match.Groups[1].Value + "=" + match.Groups[2].Value;
            }

            sData = HTTP.POST(server + "/sessions/new_session", sPost);
            m = Regex.Match(sData, "\"manifest_f4m\"\\s*?:\\s*?\"(.*?)\"");
            if (m.Success) {
                manifestUrl = Regex.Unescape(m.Groups[1].Value);
            }
        }

        public static void Quit(string msg = "") {
            if (!play) {
                lock (interfaceLocker) {
                    if (showtime) ShowTimeElapsed("\n\r" + msg);
                    else Message(msg);
                    if (waitkey && ConsolePresent) { Console.WriteLine("Press any key to continue..."); Console.ReadKey(); }
                }
            }
            if (redirect2prc) Thread.Sleep(1000);
            Environment.Exit(0);
        }

        public static void ShowHeader(string header) {
            if (play || !ConsolePresent) return;
            string h  = Regex.Replace(header, @"(<c:\w+>|</c>)", "");
            int width = (Console.WindowWidth / 2 + h.Length / 2);
            Program.Message(String.Format("\n{0," + width.ToString() + "}\n", header));
        }

        private static object interfaceLocker = new object();

        public static void Message(string msg) {
            if (play || !ConsolePresent) return;
            lock (interfaceLocker) {
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
        }

        static ReaderWriterLock debugFileLocker = new ReaderWriterLock();
        public static void DebugLog(string msg) {
            if (!debug) return;
            try {
                debugFileLocker.AcquireWriterLock(int.MaxValue); // multithread safe
                if (verbose && ConsolePresent)
                    Message(msg);

                if (logfile == "STDERR" || logfile == "STDOUT") {
                    if (ConsolePresent) {
                        switch (logfile) {
                            case "STDERR": Console.Error.WriteLine(msg); break;
                            case "STDOUT": Console.WriteLine(msg); break;
                        }
                    }
                } else {
                    File.AppendAllText(logfile, msg + "\n");
                }

            } finally {
                debugFileLocker.ReleaseWriterLock();
            }
        }

        public static bool RegExMatch(string RegX, string wherelook, out string resultValue) {
            Match m = Regex.Match(wherelook, @RegX, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            resultValue = m.Groups[1].Value;
            return m.Groups[1].Success;
        }

        public static bool RegExMatch3(string RegX, string wherelook, out string resultValue1, out string resultValue2, out string resultValue3) {
            Match m = Regex.Match(wherelook, @RegX, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            resultValue1 = m.Groups[1].Value;
            resultValue2 = m.Groups[2].Value;
            resultValue3 = m.Groups[3].Value;
            return (m.Groups[1].Success && m.Groups[2].Success && m.Groups[3].Success);
        }

        public static void ShowTimeElapsed(string msg, bool start = false) {
            if (start) {
                sw = new System.Diagnostics.Stopwatch();
                sw.Start();
            } else {
                if (!play) Program.Message(msg + "\n\r<c:DarkCyan>Time elapsed: <c:DarkGray>" + sw.Elapsed);
            }
        }

        static bool Check4Redirect2Process(ref string[] args) {
            string par2proc = "";
            string pName    = "";
            bool   isredir  = false;
            string par;
            for (int i = 0; i < args.Length; i++) {
                par = args[i];
                if ((par == "|") && (args.Length > (i + 1))) {
                    i++;
                    isredir = true;
                    pName   = args[i];
                    continue;
                }
                if (!isredir) continue;
                par2proc += " " + args[i];
            }
            if (isredir) {
                redir2Prog = new System.Diagnostics.Process();
                redir2Prog.StartInfo.UseShellExecute = false;
                redir2Prog.StartInfo.Arguments       = par2proc;
                redir2Prog.StartInfo.FileName        = pName;
                redir2Prog.StartInfo.RedirectStandardInput = true;
                redir2Prog.Start();
            }
            return isredir;
        }

    }

}
