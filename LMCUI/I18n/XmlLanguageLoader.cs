using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace LMCUI.I18n;

public static class XmlLanguageLoader
{
    public static Dictionary<string, string> LoadLanguageFile(string filePath)
    {
        var result = new Dictionary<string, string>();
            
        if (!File.Exists(filePath)) 
            return result;

        var doc = new XmlDocument();
        doc.Load(filePath);

        if (doc.DocumentElement != null) TraverseNodes(doc.DocumentElement, "", result);

        return result;
    }

    private static void TraverseNodes(XmlNode node, string currentPath, IDictionary<string, string> result)
    {
        var path = string.IsNullOrEmpty(currentPath) 
            ? node.Name 
            : $"{currentPath}.{node.Name}";
        
        path = path.Replace("Localization.", "");

        if (node.ChildNodes.Count == 1 && node.FirstChild.NodeType == XmlNodeType.Text)
        {
            result[path] = node.InnerText;
        }
        else
        {
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Element)
                {
                    TraverseNodes(child, path, result);
                }
            }
        }
    }
}