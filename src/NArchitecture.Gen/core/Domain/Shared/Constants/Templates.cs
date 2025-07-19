using Core.CrossCuttingConcerns.Helpers;

namespace NArchitecture.Gen.Domain.Shared.Constants;

public static class Templates
{
    public static class Paths
    {
        public static string Root = @"Templates";
        public static string Crud = PlatformHelper.SecuredPathJoin(Root, "CRUD");
        public static string Command = PlatformHelper.SecuredPathJoin(Root, "Command");
        public static string Query = PlatformHelper.SecuredPathJoin(Root, "Query");
    }
}
