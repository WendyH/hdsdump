using System;
using System.Xml;
using System.Collections.Generic;

namespace hdsdump.f4m {

    public class XmlDocumentEx: XmlDocument {
        public override XmlElement CreateElement(string prefix, string localname, string nsURI) {
            XmlNodeEx elem = new XmlNodeEx(prefix, localname, nsURI, this);
            return elem;
        }
    }

    public class XmlNodeEx: XmlElement {

        public XmlNodeEx(string prefix, string localname, string nsURI, XmlDocument doc):
            base( prefix, localname, nsURI, doc ) {
        }

        public List<XmlNodeEx> GetChildNodesByName(string nodeName) {
            List<XmlNodeEx> nodeList = new List<XmlNodeEx>();
            foreach (XmlNode node in ChildNodes) {
                if (node.Name == nodeName) {
                    XmlNodeEx nodeEx = node as XmlNodeEx;
                    if (nodeEx != null)
                        nodeList.Add(nodeEx);
                }
            }
            return nodeList;
        }

        public XmlNode GetChildNode(string childNodeName) {
            foreach (XmlNode node in ChildNodes) {
                if (node.Name == childNodeName)
                    return node;
            }
            return null;
        }

        public byte[] GetData(string childNodeName) {
            XmlNode childNode = GetChildNode(childNodeName);
            if (childNode != null)
                return Convert.FromBase64String(childNode.InnerText.Trim());
            return null;
        }

        public byte[] GetOwnData() {
            return Convert.FromBase64String(InnerText.Trim());
        }

        public string GetText(string childNodeName) {
            XmlNode childNode = GetChildNode(childNodeName);
            if (childNode != null) {
                return childNode.InnerText.Trim();
            }
            return string.Empty;
        }

        public int GetInt(string childNodeName, int defaultValue = 0) {
            int valueInt = defaultValue;
            XmlNode childNode = GetChildNode(childNodeName);
            if (childNode != null) {
                string val = childNode.InnerText.Trim();
                int.TryParse(val, out valueInt);
            }
            return valueInt;
        }

        public float GetFloat(string childNodeName, float defaultValue = 0) {
            float   valueInt  = defaultValue;
            XmlNode childNode = GetChildNode(childNodeName);
            if (childNode != null) {
                string val = childNode.InnerText.Trim();
                float.TryParse(val, out valueInt);
            }
            return valueInt;
        }

        const string Rfc3339 = "yyyy'-'MM'-'dd'T'HH':'mm':'ss%K";

        public DateTime GetDateTime(string childNodeName) {
            XmlNode childNode = GetChildNode(childNodeName);
            DateTime result = new DateTime();
            if (childNode != null) {
                string val = childNode.InnerText.Trim();
                DateTime.TryParseExact(val, Rfc3339, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out result);
            }
            return result;
        }

        public int GetChildNodeAttributeInt(string childNodeName, string attributeName, int defaultValue = 0) {
            int resultValue = defaultValue;
            XmlNode childNode = GetChildNode(childNodeName);
            if (childNode != null) {
                string strValue = childNode.Attributes?[attributeName]?.Value;
                int.TryParse(strValue, out resultValue);
            }
            return resultValue;
        }

        public string GetAttributeStr(string name, string defaultValue = "") {
            string value = GetAttribute(name);
            if (string.IsNullOrEmpty(value))
                return defaultValue;
            return value;
        }

        public bool GetAttributeBoolean(string name) {
            return (GetAttribute(name).ToLower()=="true");
        }

        public int GetAttributeInt(string attrName, int defaultValue = 0) {
            string valueStr = GetAttribute(attrName);
            int    result   = defaultValue;
            if (!string.IsNullOrEmpty(valueStr)) {
                int.TryParse(valueStr, out result);
            }
            return result;
        }

        public float GetAttributeFloat(string attrName, float defaultValue = 0) {
            string valueStr = GetAttribute(attrName);
            float  result   = defaultValue;
            if (!string.IsNullOrEmpty(valueStr)) {
                float.TryParse(valueStr, out result);
            }
            return result;
        }

    }
}
