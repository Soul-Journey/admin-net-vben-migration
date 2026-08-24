// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统动态插件服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 245)]
public class SysPluginService : IDynamicApiController, ITransient
{
    private readonly IDynamicApiRuntimeChangeProvider _provider;
    private readonly SqlSugarRepository<SysPlugin> _sysPluginRep;
    private readonly UserManager _userManager;

    public SysPluginService(IDynamicApiRuntimeChangeProvider provider,
        SqlSugarRepository<SysPlugin> sysPluginRep,
        UserManager userManager)
    {
        _provider = provider;
        _userManager = userManager;
        _sysPluginRep = sysPluginRep;
    }

    /// <summary>
    /// 获取动态插件列表 🧩
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取动态插件列表")]
    public async Task<SqlSugarPagedList<SysPlugin>> Page(PagePluginInput input)
    {
        EnsureSuperAdmin();
        return await _sysPluginRep.AsQueryable()
            .WhereIF(_userManager.SuperAdmin && input.TenantId > 0, u => u.TenantId == input.TenantId)
            .WhereIF(!string.IsNullOrWhiteSpace(input.Name), u => u.Name.Contains(input.Name))
            .OrderBy(u => new { u.OrderNo, u.Id })
            .ToPagedListAsync(input.Page, input.PageSize);
    }

    /// <summary>
    /// 增加动态插件 🧩
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Add"), HttpPost]
    [DisplayName("增加动态插件")]
    public async Task AddPlugin(AddPluginInput input)
    {
        EnsureSuperAdmin();
        var isExist = await _sysPluginRep.IsAnyAsync(u => u.Name == input.Name || u.AssemblyName == input.AssemblyName);
        if (isExist) throw Oops.Oh(ErrorCodeEnum.D1900);

        // 添加动态程序集/接口
        input.AssemblyName = LoadAssembly(input.CsharpCode, input.AssemblyName);

        await _sysPluginRep.InsertAsync(input.Adapt<SysPlugin>());
    }

    /// <summary>
    /// 更新动态插件 🧩
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    [DisplayName("更新动态插件")]
    public async Task UpdatePlugin(UpdatePluginInput input)
    {
        EnsureSuperAdmin();
        var plugin = await _sysPluginRep.GetByIdAsync(input.Id);
        if (plugin == null) throw Oops.Oh("动态插件不存在");

        var isExist = await _sysPluginRep.IsAnyAsync(u => u.Name == input.Name && u.Id != input.Id);
        if (isExist) throw Oops.Oh(ErrorCodeEnum.D1900);

        // Compile before unloading the active assembly so invalid source cannot
        // take the existing plugin offline.
        var replacementAssembly = CompileAssembly(input.CsharpCode, plugin.AssemblyName);
        RemoveAssembly(plugin.AssemblyName);
        _provider.AddAssembliesWithNotifyChanges(replacementAssembly);
        input.AssemblyName = replacementAssembly.GetName().Name;

        await _sysPluginRep.AsUpdateable(input.Adapt<SysPlugin>()).IgnoreColumns(true).ExecuteCommandAsync();
    }

    /// <summary>
    /// 删除动态插件 🧩
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Delete"), HttpPost]
    [DisplayName("删除动态插件")]
    public async Task DeletePlugin(DeletePluginInput input)
    {
        EnsureSuperAdmin();
        var plugin = await _sysPluginRep.GetByIdAsync(input.Id);
        if (plugin == null) return;

        // 移除动态程序集/接口
        RemoveAssembly(plugin.AssemblyName);

        await _sysPluginRep.DeleteAsync(u => u.Id == input.Id);
    }

    /// <summary>
    /// 编译动态程序集
    /// </summary>
    /// <param name="csharpCode"></param>
    /// <param name="assemblyName">程序集名称</param>
    /// <returns></returns>
    private static System.Reflection.Assembly CompileAssembly(string csharpCode, string assemblyName = default)
    {
        return App.CompileCSharpClassCode(csharpCode, assemblyName);
    }

    private string LoadAssembly(string csharpCode, string assemblyName = default)
    {
        var dynamicAssembly = CompileAssembly(csharpCode, assemblyName);
        _provider.AddAssembliesWithNotifyChanges(dynamicAssembly);
        return dynamicAssembly.GetName().Name;
    }

    /// <summary>
    /// 移除动态程序集/接口 🧩
    /// </summary>
    private void RemoveAssembly(string assemblyName)
    {
        _provider.RemoveAssembliesWithNotifyChanges(assemblyName);
    }

    private void EnsureSuperAdmin()
    {
        if (!_userManager.SuperAdmin)
            throw Oops.Oh("动态插件仅允许超级管理员维护");
    }
}
