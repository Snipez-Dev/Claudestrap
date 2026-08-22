using System;
using System.IO;
using System.Xml.Linq;

namespace Claudestrap
{
    public static class RobloxSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox",
            "GlobalBasicSettings_13.xml"
        );

        public static bool IsUncapped()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return false;

                var doc = XDocument.Load(SettingsPath);
                var fpsElement = FindFPSElement(doc);

                return fpsElement != null && int.TryParse(fpsElement.Value, out int fps) && fps > 240;
            }
            catch
            {
                return false;
            }
        }

        public static void SetUncapped(bool uncap)
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    var newDoc = new XDocument(
                        new XElement("robloxSettings",
                            new XElement("int", new XAttribute("name", "FramerateCap"), uncap ? "9999" : "-1")
                        )
                    );
                    Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                    newDoc.Save(SettingsPath);
                    return;
                }

                var doc = XDocument.Load(SettingsPath);
                var fpsElement = FindFPSElement(doc);

                if (fpsElement != null)
                    fpsElement.Value = uncap ? "9999" : "-1";
                else
                    doc.Root?.Add(new XElement("int", new XAttribute("name", "FramerateCap"), uncap ? "9999" : "-1"));

                doc.Save(SettingsPath);
            }
            catch
            {
            }
        }

        private static XElement? FindFPSElement(XDocument doc)
        {
            foreach (var intElement in doc.Descendants("int"))
            {
                var nameAttr = intElement.Attribute("name");
                if (nameAttr != null && nameAttr.Value == "FramerateCap")
                    return intElement;
            }
            return null;
        }

        public static bool IsChatVisible()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return true;

                var doc = XDocument.Load(SettingsPath);
                var chatElement = FindChatElement(doc);

                if (chatElement == null) return true;

                return chatElement.Value.ToLower() == "true";
            }
            catch
            {
                return true;
            }
        }

        public static void SetChatVisible(bool visible)
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    var newDoc = new XDocument(
                        new XElement("robloxSettings",
                            new XElement("bool", new XAttribute("name", "ChatVisible"), visible ? "true" : "false")
                        )
                    );
                    Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                    newDoc.Save(SettingsPath);
                    return;
                }

                var doc = XDocument.Load(SettingsPath);
                var chatElement = FindChatElement(doc);

                if (chatElement != null)
                    chatElement.Value = visible ? "true" : "false";
                else
                    doc.Root?.Add(new XElement("bool", new XAttribute("name", "ChatVisible"), visible ? "true" : "false"));

                doc.Save(SettingsPath);
            }
            catch
            {
            }
        }

        private static XElement? FindChatElement(XDocument doc)
        {
            foreach (var boolElement in doc.Descendants("bool"))
            {
                var nameAttr = boolElement.Attribute("name");
                if (nameAttr != null && nameAttr.Value == "ChatVisible")
                    return boolElement;
            }
            return null;
        }

        public static bool IsDarkMode()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return false;

                var doc = XDocument.Load(SettingsPath);
                var themeElement = FindThemeElement(doc);

                return themeElement != null && themeElement.Value.Equals("Dark", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static void SetDarkMode(bool dark)
        {
            try
            {
                string value = dark ? "Dark" : "Light";

                if (!File.Exists(SettingsPath))
                {
                    var newDoc = new XDocument(
                        new XElement("robloxSettings",
                            new XElement("string", new XAttribute("name", "InterfaceStyle"), value)
                        )
                    );
                    Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                    newDoc.Save(SettingsPath);
                    return;
                }

                var doc = XDocument.Load(SettingsPath);
                var themeElement = FindThemeElement(doc);

                if (themeElement != null)
                    themeElement.Value = value;
                else
                    doc.Root?.Add(new XElement("string", new XAttribute("name", "InterfaceStyle"), value));

                doc.Save(SettingsPath);
            }
            catch
            {
            }
        }

        private static XElement? FindThemeElement(XDocument doc)
        {
            foreach (var stringElement in doc.Descendants("string"))
            {
                var nameAttr = stringElement.Attribute("name");
                if (nameAttr != null && nameAttr.Value == "InterfaceStyle")
                    return stringElement;
            }
            return null;
        }
    }
}
