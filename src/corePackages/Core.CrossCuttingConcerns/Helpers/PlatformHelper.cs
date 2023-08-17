using System;
namespace Core.CrossCuttingConcerns.Helpers
{
    public static class PlatformHelper
    {
        public static string SecuredPathJoin(params string[] pathItems)
        {
            string path;
            _ = Environment.OSVersion.Platform switch
            {
                PlatformID.Unix => path = String.Join("/", pathItems),
                PlatformID.MacOSX => path = String.Join("/", pathItems),
                _ => path = String.Join("\\", pathItems),
            };

            return path;
        }

        public static string GetDirectorHeader()
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
}

