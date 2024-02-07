namespace Core.CrossCuttingConcerns.Helpers;

public static class PlatformHelper
{
    public static string SecuredPathJoin(params string[] pathItems)
    {
        string path;
        _ = Environment.OSVersion.Platform switch
        {
            PlatformID.Unix => path = string.Join("/", pathItems),
            PlatformID.MacOSX => path = string.Join("/", pathItems),
            _ => path = string.Join("\\", pathItems),
        };

        return path;
    }

    public static string GetDirectoryHeader()
    {
        string file;
        _ = Environment.OSVersion.Platform switch
        {
            PlatformID.Unix => file = "file://",
            PlatformID.MacOSX => file = "file://",
            _ => file = "",
        };

        return file;
    }
}
