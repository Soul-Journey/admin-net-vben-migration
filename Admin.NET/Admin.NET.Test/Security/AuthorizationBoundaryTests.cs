using Admin.NET.Core.Service;
using Admin.NET.Web.Core;
using Microsoft.AspNetCore.Http;
using System.Reflection;
using Xunit;

namespace Admin.NET.Test.Security;

public class AuthorizationBoundaryTests
{
    [Theory]
    [InlineData("/api/sysUser/page")]
    [InlineData("/api/sysPlugin/compileAssembly")]
    [InlineData("/api/sysPlugin/removeAssembly")]
    public void AppTokenCannotAccessProtectedManagementRoutes(string path)
    {
        Assert.False(JwtHandler.IsAppApiRouteAllowed(new PathString(path)));
    }

    [Theory]
    [InlineData("CompileAssembly")]
    [InlineData("RemoveAssembly")]
    public void PluginRuntimeHelpersAreNotPublicApiMethods(string methodName)
    {
        var method = typeof(SysPluginService).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);

        Assert.Null(method);
    }
}
