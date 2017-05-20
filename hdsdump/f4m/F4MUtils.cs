using System.Text.RegularExpressions;

namespace hdsdump.f4m {
    public class F4MUtils {
        public struct Version {
            public int Minor;
            public int Major;
        }

        public static string GLOBAL_ELEMENT_ID = "global";

        /// <summary>
        /// Returns the version based on the default namespace of the F4M example.
		/// <p>An example of a version 1.0 namespace: "http://ns.adobe.com/f4m/1.0"</p>
        /// </summary>
        public static Version getVersion(string resource) {
            Version version = new Version();
            version.Minor = 0;
            version.Major = 0;
            Match matchVer = Regex.Match(resource, "xmlns\\s*?=\\s*?\"[^\"]+/([\\d\\.]+)\"");
            if (matchVer.Success) {
                string ver = matchVer.Groups[1].Value;
                Match m;
                m = Regex.Match(ver, "(\\d+)");
                if (m.Success) int.TryParse(m.Groups[1].Value, out version.Major);
                m = Regex.Match(ver, "\\.(\\d+)");
                if (m.Success) int.TryParse(m.Groups[1].Value, out version.Minor);
            }
            return version;
		}

        /// <summary>
        /// Returns the version based on the default namespace of the F4M example.
		/// <p>An example of a version 1.0 namespace: "http://ns.adobe.com/f4m/1.0"</p>
        /// </summary>
        public static Version getVersion(XmlNodeEx node) {
            return getVersion(node.OwnerDocument.OuterXml);
        }
    }
}
