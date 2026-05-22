using System;
using System.Collections.Generic;
using System.IO;

class IniFile
{
    private Dictionary<string, string> data = new Dictionary<string, string>();

    public IniFile(string path)
    {
        if (!File.Exists(path)) return;

        string currentSection = "";

        foreach (var line in File.ReadAllLines(path))
        {
            string l = line.Trim();

            if (l.StartsWith(";") || l == "") continue;

            if (l.StartsWith("[") && l.EndsWith("]"))
            {
                currentSection = l.Substring(1, l.Length - 2);
                continue;
            }

            var parts = l.Split('=');
            if (parts.Length == 2)
            {
                string key = currentSection + "." + parts[0].Trim();
                string value = parts[1].Trim();

                data[key] = value;
            }
        }
    }

    public string Get(string section, string key, string defaultValue)
    {
        string fullKey = section + "." + key;
        //string tmp = data.ContainsKey(fullKey) ? data[fullKey] : defaultValue;
        return data.ContainsKey(fullKey) ? data[fullKey] : defaultValue;
    }
}