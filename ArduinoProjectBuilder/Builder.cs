using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace ArduinoProjectBuilder;

public static class Builder
{
    private const string cfgFileName = "ArduinoProjectBuilder.BuildingBlocks.json";

    public class AreaInfo
    {
        public string Name { get; set; } = "";
        public bool NewLine { get; set; }
    }

    public class Config
    {
        public List<string> Files { get; set; } = ["AppSetup.h", "AppLogic.h", "Examples.h", "Readme.md"];

        public List<string> Modules { get; set; } = ["Buttons", "LEDs", "LCD", "SDCard", "SDCard-Settings"];

        public List<AreaInfo> Areas { get; set; } =
        [
            new() { Name = "DEFINE", NewLine = true },
            new() { Name = "INIT", NewLine = false },
            new() { Name = "LOOP", NewLine = false },
            new() { Name = "EXAMPLES", NewLine = false },
            new() { Name = "RUNEXAMPLES", NewLine = true }
        ];
    }

    public static string? Run(string iniFilepath)
    {
        var ini = IniFileReader.ReadIniFile(iniFilepath);
        var sketchBookFolder = IniFileReader.TryGetValue(ini, "Main", "SketchbookFolder")!;
        var buildingBlocksFolder = IniFileReader.TryGetValue(ini, "Main", "BuildingBlocksFolder")!;
        var projectName = IniFileReader.TryGetValue(ini, "Project", "Name");
        var projectFolder = Path.Combine(sketchBookFolder!, projectName!);
        if (Directory.Exists(projectFolder))
        {
            Move2Backup(projectFolder);
            Directory.Delete(projectFolder, true);
        }
        Directory.CreateDirectory(projectFolder);
        var projectVariables = ini["Project"];
        CreateInoFile(projectFolder, projectName!, buildingBlocksFolder, projectVariables);
        var cfg = GetConfig(buildingBlocksFolder);
        foreach (var file in cfg!.Files)
            ProcessFile(sketchBookFolder, file, buildingBlocksFolder,
                projectFolder,
                cfg!.Modules,
                cfg.Areas, projectVariables, ini);
        return projectName;
    }
    
    internal static Config GetConfig(string cfgFolder, bool reCreate = false)
    {
        var filePath = Path.Combine(cfgFolder!, cfgFileName);
        if (reCreate || !File.Exists(filePath))
        {
            var cfgStr = ResourceReader.ExtractString(cfgFileName);
            File.WriteAllText(filePath, cfgStr, Encoding.UTF8);
        }
        var json = File.ReadAllText(filePath, Encoding.UTF8);
        return Serializer.Deserialize<Config>(json)!;
    }
    
    private static void Move2Backup(string projectFolder)
    {
        var appFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var backupRootFolder = Path.Combine(appFolder!, ".Backup");
        var backupFolder = Path.Combine(backupRootFolder, $"{Path.GetFileNameWithoutExtension(projectFolder)}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");
        
        Directory.CreateDirectory(backupRootFolder);
        CopyDirectory(projectFolder, backupFolder);

        return;

        static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }
            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                var destDir = Path.Combine(destinationDir, Path.GetFileName(directory));
                CopyDirectory(directory, destDir);
            }
        }

    }
    
    private static void CreateInoFile(string projectFolder, string projectName, string buildingBlocksFolder, Dictionary<string, string> projectVariables)
    {
        var sourceFile = Directory.GetFiles(buildingBlocksFolder, "*.ino").Single();
        var targetFile = Path.Combine(projectFolder, $"{projectName}.ino");
        var content = File.ReadAllText(sourceFile, new UTF8Encoding(false));
        content = projectVariables.Aggregate(content, (current, variable) => ReplaceValue(current, variable.Key, variable.Value));
        File.WriteAllText(targetFile, content, new UTF8Encoding(false));
    }
    
    private static void ProcessFile(string sketchBookFolder, string filename, string inputFolder, string outFolder, IEnumerable<string> moduleNames, 
        List<AreaInfo> areas, Dictionary<string,string>? projectVariables, Dictionary<string, Dictionary<string, string>> ini)
    {
        var sourceFile = Path.Combine(inputFolder, filename);

        if (!File.Exists(sourceFile))
        {
            Console.WriteLine($"WARNING: file not found and skipped: {filename}");
            return;
        }
        var content = File.ReadAllText(sourceFile, new UTF8Encoding(false));
        content = projectVariables!.Aggregate(content, (current, variable) => ReplaceValue(current, variable.Key, variable.Value));
        content = areas.Aggregate(content, (current, areaInfo) => ReplaceArea(sketchBookFolder, current, areaInfo.Name, moduleNames, ini, inputFolder, areaInfo.NewLine));
        var targetFile = Path.Combine(outFolder, filename);
        File.WriteAllText(targetFile, content, new UTF8Encoding(false));
    }
    
    internal static string ReplaceArea(string sketchBookFolder, string content, string areaName, IEnumerable<string> moduleNames, Dictionary<string, Dictionary<string, string>> ini, string inputFolder, bool newLine)
    {
        var areaReplaceToken = $"%%{areaName.ToUpper()}%%";
        var ix = content.IndexOf(areaReplaceToken, StringComparison.InvariantCultureIgnoreCase);
        if (ix == -1) return content;
        var spaces = 0;
        while (ix > 0)
        {
            ix--;
            if (content[ix] == ' ') spaces++;
            if (content[ix] == '\n') break; }

        var sb = new StringBuilder();

        foreach (var moduleName in moduleNames)
            sb.Append(GetModuleContent(sketchBookFolder, areaName, moduleName, ini, spaces, inputFolder, newLine));

        var replaceContent = sb.ToString().Trim();

        if (string.IsNullOrEmpty(replaceContent))
        {
            // Regex, um Zeilen zu finden, die nur aus Leerzeichen und dem Token bestehen, inklusive des Zeilenumbruchs
            var pattern = $@"^\s*{Regex.Escape(areaReplaceToken)}\s*\r?\n";
            // Entfernen der Zeilen, die nur das Token enthalten
            content = Regex.Replace(content, pattern, replaceContent, RegexOptions.Multiline);
        }
        else
            content = content.Replace(areaReplaceToken, replaceContent);

        return content;
    }

    private static string GetModuleContent(string sketchBookFolder, string areaname, string moduleName,
        Dictionary<string, Dictionary<string, string>> ini, int spaces, string inputFolder, bool newLine)
    {
        if (!ini.TryGetValue(moduleName, out var section))
            return string.Empty;

        var sequence = section.Values.SingleOrDefault(v => v.StartsWith('{'))?[1..^1]?.Split(",")
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s)).ToArray();


        var variables = section.Where(v => !v.Value.StartsWith('{')).Select(pair => $"{pair.Key.ToUpper()}={FormatIniValue(pair.Value)}").ToArray();
        var projectVariables = ini["Project"].Where(v => !v.Value.StartsWith('{')).Select(pair => $"{pair.Key.ToUpper()}={FormatIniValue(pair.Value)}");
        variables = variables.Concat(projectVariables).ToArray();   

        var fname = $"{moduleName}.{areaname}.txt";
        var fpath = Path.Combine(inputFolder, fname);
        if (!File.Exists(fpath))
            return string.Empty;

        var fileContent = File.ReadAllText(fpath, Encoding.UTF8).Trim();

        if (fileContent.StartsWith("#import"))
        {
            var part = fileContent.Replace("#import", "").Trim();
            if (part.StartsWith('\\')) part = part[1..];
            var importFilePath = Path.Combine(sketchBookFolder, part);
            var importContent = File.ReadAllText(importFilePath, new UTF8Encoding(false));
            return importContent + "\n\n";
        }

        fileContent = fileContent.Replace("\r\n", "\n");
        var prefix = new string(' ', spaces);
        var result = Replace(fileContent, variables, sequence, prefix);
        if (newLine) result += "\n";
        return result;
    }

    private static string FormatIniValue(string value)
    {
        return value.Replace("\\n", "\n").Replace(@"\\", "\\");
    }

    private static string ReplaceValue(string content, string key, string value)
    {
        return content.Replace($"%%{key.ToUpper()}%%", FormatIniValue(value));
    }

    internal static string Replace(string content, IEnumerable<string>? variables, string[]? sequence, string prefix)
    {
        var result = variables != null
            ? variables.Select(variable => variable.Split('=').Select(s => s.Trim()).ToArray()).Aggregate(content, (current, ar) => 
                ReplaceValue(current, ar[0], ar[1]))
            : content;

        var sb = new StringBuilder();
        const string loopStartToken = "/*@LOOPSTART*/";
        const string loopEndToken = "/*@LOOPEND*/";

        var lines = result.Split(['\n'], StringSplitOptions.None);
        foreach (var line in lines)
        {
            if (sequence == null || sequence.Length == 0)
            {
                sb.AppendLine($"{prefix}{line}");
                continue;
            }

            var newLine = line.Replace("{COUNT}", sequence.Length.ToString());
            if (line.Contains("{INDEX}") || line.Contains("{VALUE}"))
            {
                var loopStart = line.IndexOf(loopStartToken, StringComparison.Ordinal);
                if (loopStart > -1)
                {
                    var loopEnd = line.IndexOf(loopEndToken, StringComparison.Ordinal);
                    var header = line[..loopStart];
                    var footer = line[(loopEnd + loopEndToken.Length)..];
                    var template = line.Substring(loopStart + loopStartToken.Length, loopEnd - loopStart - loopStartToken.Length);

                    var loopContent = new StringBuilder();
                    foreach (var ix in Enumerable.Range(0, sequence.Length))
                        loopContent.Append(template.Replace("{INDEX}", (ix + 1).ToString()).Replace("{VALUE}", sequence[ix]));
                    sb.AppendLine($"{prefix}{header}{loopContent.ToString()[..^1]}{footer}");
                }
                else
                {
                    foreach (var ix in Enumerable.Range(0, sequence.Length))
                    {
                        var s = newLine;
                        s = s.Replace("{INDEX}", (ix + 1).ToString());
                        s = s.Replace("{VALUE}", sequence[ix]);
                        sb.AppendLine($"{prefix}{s}");
                    }
                }
                continue;
            }
            sb.AppendLine($"{prefix}{newLine}");
        }
        return sb.ToString();
    }
}