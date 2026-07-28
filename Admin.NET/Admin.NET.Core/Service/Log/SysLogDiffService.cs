// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统差异日志服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 330)]
public class SysLogDiffService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<SysLogDiff> _sysLogDiffRep;
    private readonly UserManager _userManager;

    public SysLogDiffService(UserManager userManager, SqlSugarRepository<SysLogDiff> sysLogDiffRep)
    {
        _sysLogDiffRep = sysLogDiffRep;
        _userManager = userManager;
    }

    /// <summary>
    /// 获取差异日志分页列表 🔖
    /// </summary>
    /// <returns></returns>
    [SuppressMonitor]
    [DisplayName("获取差异日志分页列表")]
    public async Task<SqlSugarPagedList<SysLogDiff>> Page(PageLogInput input)
    {
        return await _sysLogDiffRep.AsQueryable()
            .WhereIF(_userManager.SuperAdmin && input.TenantId > 0, u => u.TenantId == input.TenantId)
            .WhereIF(!string.IsNullOrWhiteSpace(input.StartTime.ToString()), u => u.CreateTime >= input.StartTime)
            .WhereIF(!string.IsNullOrWhiteSpace(input.EndTime.ToString()), u => u.CreateTime <= input.EndTime)
            .OrderBy(u => u.CreateTime, OrderByType.Desc)
            .ToPagedListAsync(input.Page, input.PageSize);
    }

    /// <summary>
    /// 获取差异日志详情 🔖
    /// </summary>
    /// <returns></returns>
    [SuppressMonitor]
    [DisplayName("获取差异日志详情")]
    public async Task<SysLogDiff> GetDetail(long id)
    {
        var log = await _sysLogDiffRep.GetFirstAsync(u => u.Id == id) ?? throw Oops.Oh(ErrorCodeEnum.D1002);
        return LogSanitizer.Sanitize(log);
    }

    /// <summary>
    /// 清空差异日志
    /// </summary>
    [ApiDescriptionSettings(Name = "Clear"), HttpPost]
    [DisplayName("清空差异日志")]
    public async Task<int> Clear(LogInput? input = null)
    {
        if (!_userManager.SuperAdmin) throw Oops.Oh(ErrorCodeEnum.D3010);
        if (input?.TenantId > 0)
            return await _sysLogDiffRep.AsDeleteable().Where(u => u.TenantId == input.TenantId).ExecuteCommandAsync();
        return await _sysLogDiffRep.AsDeleteable().ExecuteCommandAsync();
    }
}
