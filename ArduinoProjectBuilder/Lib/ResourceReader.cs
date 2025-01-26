using System.Globalization;
using System.Reflection;

namespace ArduinoProjectBuilder;

public static class ResourceReader
{
    #region public methods

    public static Stream? ExtractStream(string resourceName, Assembly? asm = null)
    {
        return FindManifestResourceStream(asm, resourceName);
    }

    public static string? ExtractString(string resourceName, Assembly? asm = null)
    {
        using var stream = FindManifestResourceStream(asm, resourceName);
        if (stream == null) return string.Empty;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    #endregion

    #region private methods

    private static Stream? FindManifestResourceStream(Assembly? asm, string resourceName)
    {
        return asm != null
            ? asm.ExtractManifestResourceStream(resourceName)
            : AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.GetType().FullName != "System.Reflection.Emit.InternalAssemblyBuilder")
                .Select(assembly => assembly.ExtractManifestResourceStream(resourceName))
                .FirstOrDefault(stream => stream != null);
    }

    private static Stream? ExtractManifestResourceStream(this Assembly asm, string resourceName)
    {
        var spec = asm.GetManifestResourceNames().FirstOrDefault(s => s.EndsWith(resourceName, ignoreCase: true, culture: CultureInfo.InvariantCulture));
        return string.IsNullOrEmpty(spec) ? null : asm.GetManifestResourceStream(spec);
    }


    #endregion
}