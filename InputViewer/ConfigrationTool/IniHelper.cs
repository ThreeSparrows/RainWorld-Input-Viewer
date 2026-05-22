using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ConfigrationTool
{
    internal class IniHelper
    {
        private List<string> _lines = new List<string>();
        private readonly Dictionary<string, Dictionary<string, string>> _data
            = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public void Load(string path)
        {
            _lines = new List<string>(File.ReadAllLines(path, Encoding.UTF8));
            Parse();
        }

        private void Parse()
        {
            _data.Clear();
            string sec = "";
            foreach (string line in _lines)
            {
                string t = line.Trim();
                if (t.StartsWith("[") && t.EndsWith("]"))
                {
                    sec = t.Substring(1, t.Length - 2);
                    if (!_data.ContainsKey(sec))
                        _data[sec] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                else if (!string.IsNullOrEmpty(sec) && !t.StartsWith(";") && t.Contains("="))
                {
                    int eq = t.IndexOf('=');
                    string k = t.Substring(0, eq).Trim();
                    string v = t.Substring(eq + 1).Trim();
                    _data[sec][k] = v;
                }
            }
        }

        public string Get(string section, string key, string defaultVal = "")
        {
            Dictionary<string, string> sec;
            string val;
            if (_data.TryGetValue(section, out sec) && sec.TryGetValue(key, out val))
                return val;
            return defaultVal;
        }

        public void Set(string section, string key, string value)
        {
            if (!_data.ContainsKey(section))
                _data[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _data[section][key] = value;

            string curSec = "";
            bool inSec = false;
            for (int i = 0; i < _lines.Count; i++)
            {
                string t = _lines[i].Trim();
                if (t.StartsWith("[") && t.EndsWith("]"))
                {
                    curSec = t.Substring(1, t.Length - 2);
                    inSec = string.Equals(curSec, section, StringComparison.OrdinalIgnoreCase);
                }
                else if (inSec && !t.StartsWith(";") && t.Contains("="))
                {
                    int eq = t.IndexOf('=');
                    if (string.Equals(t.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                    {
                        _lines[i] = key + "=" + value;
                        return;
                    }
                }
            }

            // キーが見つからなかった場合: セクション直後に挿入
            for (int i = 0; i < _lines.Count; i++)
            {
                string t = _lines[i].Trim();
                if (t.StartsWith("[") && t.EndsWith("]") &&
                    string.Equals(t.Substring(1, t.Length - 2), section, StringComparison.OrdinalIgnoreCase))
                {
                    _lines.Insert(i + 1, key + "=" + value);
                    return;
                }
            }

            // セクション自体が存在しない場合: 末尾に追加
            _lines.Add("[" + section + "]");
            _lines.Add(key + "=" + value);
        }

        public void Save(string path)
        {
            File.WriteAllLines(path, _lines, new UTF8Encoding(false));
        }
    }
}
