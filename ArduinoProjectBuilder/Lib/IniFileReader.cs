namespace ArduinoProjectBuilder;

using System;
using System.Collections.Generic;
using System.IO;

public static class IniFileReader
{
    public static string? TryGetValue(Dictionary<string, Dictionary<string, string>> ini, string sectionName, string keyName, bool toUpper = true)
    {
        if (toUpper)
        {
            sectionName = sectionName.ToUpper();
            keyName = keyName.ToUpper();
        }
        if (ini.TryGetValue(sectionName, out var section) && section.TryGetValue(keyName, out var value))
            return value;
        return null;
    }
    
    public static Dictionary<string, Dictionary<string, string>> ReadIniFile(string filePath, bool toUpper = true)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The specified INI file does not exist.", filePath);

        var currentSection = string.Empty;
        foreach (var line in File.ReadLines(filePath))
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                continue;
            if (trimmedLine.StartsWith($"[") && trimmedLine.EndsWith($"]"))
            {
                currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                if (toUpper)
                    currentSection = currentSection.ToUpper();

                if (!result.ContainsKey(currentSection))
                    result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                var keyValuePair = trimmedLine.Split(['='], 2);
                if (keyValuePair.Length != 2) continue;
                var key = keyValuePair[0].Trim();
                if (toUpper)
                    key = key.ToUpper();

                var value = keyValuePair[1].Trim();
                if (!string.IsNullOrEmpty(currentSection))
                    result[currentSection][key] = value;
            }
        }
        return result;
    }
}
