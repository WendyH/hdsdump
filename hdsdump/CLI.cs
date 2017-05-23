using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace hdsdump {
    class CLI {
        protected static Dictionary<string, string>[] ACCEPTED = {
                new Dictionary<string,string> {
					// Single switches
                    {"h |help"     , "displays this help"},
                    {"l |debug"    , "out debug output to log file"},
                    {"p |play"     , "dump flv data to stderr for piping to another program"},
                    {"st|showtime" , "show time remaining"},
                    {"op|osproxy"  , "use system proxy (by OS)"},
                    {"fp|fproxy"   , "force proxy for downloading of fragments"},
                    {"  |postdata" , "data for the POST method for http request"},
                    {"wk|waitkey"  , "wait pressed any key at the end"},
                    {"c |continue" , "continue if possible downloading with exists file"},
                    {"z |oldmethod", "use the old method to download"},
                    {"  |quiet"    , "no output any messages"},
                    {"v |verbose"  , "show exteneded info while dumping"},
                    {"  |testalt"  , "sets all avaliable media also as alternate"},
                    
                },
                new Dictionary<string,string> {
					// Switches with parameters
                    {"a |auth"     , "authentication string for fragment requests (add '?' with parameter to end manifest url)"},
                    {"t |duration" , "stop dumping after specified time in the file (hh:mm:ss)"},
                    {"fs|filesize" , "limit size of the output file"},
                    {"m |manifest" , "path or url to manifest file for dumping stream (f4m)"},
                    {"b |urlbase"  , "base url for relative path in manifest"},
                    {"od|outdir"   , "destination folder for output file"},
                    {"o |outfile"  , "filename to use for output file"},
                    {"th|threads"  , "number of threads to download fragments"},
                    {"q |quality"  , "selected quality level (low|medium|high) or exact bitrate"},
                    {"ss|skip"     , "skip time hh:mm:ss"},
                    {"f |start"    , "start from specified fragment"},
                    {"lf|logfile"  , "file for debug output"},
                    {"ua|useragent", "user-Agent to use for emulation of browser requests"},
                    {"re|referer"  , "referer in the headers http requests"},
                    {"ck|cookies"  , "cookies in the headers http requests"},
                    {"H |headers"  , "http header"},
                    {"un|username" , "username to use for access to http server"},
                    {"ps|password" , "password to use for access to http server"},
                    {"px|proxy"    , "proxy for downloading of manifest (http://proxyAddress:proxyPort)"},
                    {"pu|proxyuser", "username for proxy server"},
                    {"pp|proxypass", "password for proxy server"},
                    {"  |adkey"    , "Akamai DRM session key as hexadecimal string"},
                    {"  |lang"     , "default language for audio if alternate audio is present"},
                    {"  |alt"      , "select alternate audio by language, codec, bitrate, streamId or label"}
                }
            };

        public Dictionary<string, string> Params = new Dictionary<string, string> { };

        private void Error(string msg) {
            if (Program.isRedirected || Program.redir2Prog != null)
                Program.Message(msg);
            else
                Program.Quit(msg);
        }

        public CLI(string[] argv) {
            // Parse params
            string doubleParam = "", doubleKey = "", arg, shrtKey, longKey;
            for (int i = 0; i < argv.Length; i++) {
                arg = argv[i];
                if (arg == "|") break; // 4windows
                bool isparam = Regex.IsMatch(arg, "^-");
                if (isparam) arg = Regex.Replace(arg, "^--?", "");
                if ((doubleParam != "") && isparam)
                    Error("<param> <c:Red>expected after '<c:White>" + argv[i - 1] + "</c>' switch <c:DarkCyan>(" + ACCEPTED[1][doubleKey] + ")</c>\n");
                else if ((doubleParam == "") && !isparam)
                    Error("'<c:Green>" + argv[i] + "</c>' <c:Red>is an invalid switch, use <c:White>-h</c> or <c:White>--help</c> to display valid switches\n");
                else if ((doubleParam == "") && isparam) {
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
                                doubleKey = pair.Key;
                                keyFound = true;
                                break;
                            }
                        }
                    if (!keyFound) {
                        Error("<c:Red>There's no <c:Green>" + argv[i] + "</c> switch, use <c:White>-h</c> or <c:White>--help</c> to display all switches\n");
                    }
                    if (Params.ContainsKey(arg)) {
                        if (arg != "headers")
                            Error("'<c:White>" + argv[i] + "</c>' <c:Red>switch cannot occur more than once\n");
                    } else 
                        Params[arg] = "[1]";

                } else if ((doubleParam != "") && !isparam) {
                    if ((doubleParam == "headers") && (Params[doubleParam] != "[1]"))
                        Params[doubleParam] += "|"+arg;
                    else
                        Params[doubleParam] = arg;
                    doubleParam = "";
                }
            }
        }

        public void EchoSetsParameters() {
            string sKey, sParameters = "", sValues = "";
            foreach (KeyValuePair<string, string> pair in Params) {
                //sKey = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(pair.Key);
                sKey = pair.Key;
                if (pair.Value == "[1]") {
                    sParameters += sKey + " ";
                } else {
                    sValues += String.Format("<c:DarkCyan>{0,-10}: <c:DarkGreen>{1}\n\r", sKey, pair.Value);
                }
            }
            if (sParameters != "") Program.Message(String.Format("<c:DarkCyan>Parameters:</c> <c:DarkGreen>{0,-10}", sParameters));
            if (sValues     != "") Program.Message(sValues + "\n\r");
        }

        public bool ChkParam(string name) {
            return Params.ContainsKey(name);
        }

        public string GetParam(string name) {
            if (Params.ContainsKey(name)) return Params[name].Trim();
            else return string.Empty;
        }

        public void DisplayHelp() {
            string shrtKey = "", longKey = "";
            if (Program.play) return;
            Program.Message("You can use <c:White>hdsdump</c> with following switches: \n\r\n\r");
            foreach (KeyValuePair<string, string> pair in ACCEPTED[0]) {
                shrtKey = pair.Key.Split('|')[0].Trim();
                longKey = pair.Key.Split('|')[1].Trim();
                if (shrtKey == "")
                    Program.Message(String.Format("     <c:White>--{0,-17} <c:DarkCyan>{1}", longKey, pair.Value));
                else
                    Program.Message(String.Format(" <c:White>-{0,-2}</c>|<c:White>--{1,-17}</c> <c:DarkCyan>{2}", shrtKey, longKey, pair.Value));
            }
            foreach (KeyValuePair<string, string> pair in ACCEPTED[1]) {
                shrtKey = pair.Key.Split('|')[0].Trim();
                longKey = pair.Key.Split('|')[1].Trim();
                if (shrtKey == "")
                    Program.Message(String.Format("     <c:White>--{0,-10}</c><param> <c:DarkCyan>{1,-7}", longKey, pair.Value));
                else
                    Program.Message(String.Format(" <c:White>-{0,-2}</c>|<c:White>--{1,-10}</c><param> <c:DarkCyan>{2,-7}", shrtKey, longKey, pair.Value));

            }
        }
    }

}
