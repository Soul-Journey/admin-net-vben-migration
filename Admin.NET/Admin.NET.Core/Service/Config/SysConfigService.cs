// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using NewLife.Reflection;

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统参数配置服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 440)]
public class SysConfigService : IDynamicApiController, ITransient
{
    private static readonly SysTenantService SysTenantService = App.GetService<SysTenantService>();
    private readonly SqlSugarRepository<SysConfigValue> _sysConfigValueRep;
    private readonly SqlSugarRepository<SysConfig> _sysConfigRep;
    private readonly SqlSugarRepository<SysTenant> _sysTenantRep;
    private readonly SysCacheService _sysCacheService;
    private readonly UserManager _userManager;

    public SysConfigService(
        SqlSugarRepository<SysConfigValue> sysConfigValueRep,
        SqlSugarRepository<SysTenant> sysTenantRep,
        SqlSugarRepository<SysConfig> sysConfigRep,
        SysCacheService sysCacheService,
        UserManager userManager)
    {
        _sysConfigValueRep = sysConfigValueRep;
        _sysCacheService = sysCacheService;
        _sysConfigRep = sysConfigRep;
        _sysTenantRep = sysTenantRep;
        _userManager = userManager;
    }

    /// <summary>
    /// 获取参数配置分页列表 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取参数配置分页列表")]
    public async Task<SqlSugarPagedList<ConfigOutput>> Page(PageConfigInput input)
    {
        var queryable = await GetConfigQueryable();
        var query = queryable
            .WhereIF(!_userManager.SuperAdmin,  u => u.SysFlag == YesNoEnum.N)
            .WhereIF(!string.IsNullOrWhiteSpace(input.Name?.Trim()), u => u.Name.Contains(input.Name))
            .WhereIF(!string.IsNullOrWhiteSpace(input.Code?.Trim()), u => u.Code.Contains(input.Code))
            .WhereIF(!string.IsNullOrWhiteSpace(input.GroupCode?.Trim()), u => u.GroupCode.Equals(input.GroupCode))
            .OrderBuilder(input);
        var page = await query.ToPagedListAsync(input.Page, input.PageSize);
        return new SqlSugarPagedList<ConfigOutput>
        {
            Page = page.Page,
            PageSize = page.PageSize,
            Total = page.Total,
            TotalPages = page.TotalPages,
            HasNextPage = page.HasNextPage,
            HasPrevPage = page.HasPrevPage,
            Items = page.Items.Select(ToSafeOutput).ToList(),
        };
    }

    /// <summary>
    /// 获取参数配置列表 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取参数配置列表")]
    public async Task<List<ConfigOutput>> List(PageConfigInput input)
    {
        var queryable = await GetConfigQueryable();
        var query = queryable
            .WhereIF(!_userManager.SuperAdmin, u => u.SysFlag == YesNoEnum.N)
            .WhereIF(!string.IsNullOrWhiteSpace(input.GroupCode?.Trim()), u => u.GroupCode.Equals(input.GroupCode))
            .OrderBy(u => u.OrderNo);
        var items = await query.ToListAsync();
        return items.Select(ToSafeOutput).ToList();
    }

    /// <summary>
    /// 增加参数配置 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Add"), HttpPost]
    [DisplayName("增加参数配置")]
    [UnitOfWork]
    public async Task AddConfig(AddConfigInput input)
    {
        if (!_userManager.SuperAdmin) throw Oops.Oh(ErrorCodeEnum.D3010);

        var isExist = await _sysConfigRep.IsAnyAsync(u => u.Name == input.Name || u.Code == input.Code);
        if (isExist) throw Oops.Oh(ErrorCodeEnum.D9000);

        if (IsSensitiveCode(input.Code) && input.Value == SensitiveMask)
            throw Oops.Oh("敏感配置必须填写实际值");

        var entity = input.Adapt<SysConfig>();
        if (entity.SysFlag == YesNoEnum.N)
            entity.Value = null;
        await _sysConfigRep.InsertAsync(entity);
        if (entity.SysFlag == YesNoEnum.N)
            await UpsertTenantValue(entity.Id, input.Value);
    }

    /// <summary>
    /// 更新参数配置 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    [DisplayName("更新参数配置")]
    [UnitOfWork]
    public async Task UpdateConfig(UpdateConfigInput input)
    {
        var current = await _sysConfigRep.GetFirstAsync(u => u.Id == input.Id) ?? throw Oops.Oh(ErrorCodeEnum.D1002);
        if (current.SysFlag == YesNoEnum.Y && !_userManager.SuperAdmin) throw Oops.Oh(ErrorCodeEnum.D3010);

        if (IsSensitiveCode(current.Code) && input.Value == SensitiveMask)
        {
            var currentValue = await GetConfigQueryable();
            input.Value = (await currentValue.FirstAsync(u => u.Id == input.Id))?.Value;
        }

        if (!_userManager.SuperAdmin)
        {
            await UpsertTenantValue(current.Id, input.Value);
            Remove(current);
            return;
        }

        var isExist = await _sysConfigRep.IsAnyAsync(u => (u.Name == input.Name || u.Code == input.Code) && u.Id != input.Id);
        if (isExist) throw Oops.Oh(ErrorCodeEnum.D9000);

        var config = input.Adapt<SysConfig>();
        if (input.SysFlag != YesNoEnum.Y)
        {
            config.Value = null;
            await UpsertTenantValue(config.Id, input.Value);
        }
        else
        {
            await _sysConfigValueRep.DeleteAsync(u => u.ConfigId == input.Id);
        }
        await _sysConfigRep.AsUpdateable(config).ExecuteCommandAsync();
        Remove(config);
    }

    /// <summary>
    /// 删除参数配置 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Delete"), HttpPost]
    [DisplayName("删除参数配置")]
    [UnitOfWork]
    public async Task DeleteConfig(DeleteConfigInput input)
    {
        if (!_userManager.SuperAdmin) throw Oops.Oh(ErrorCodeEnum.D3010);
        var config = await _sysConfigRep.GetFirstAsync(u => u.Id == input.Id);
        _ = config ?? throw Oops.Oh(ErrorCodeEnum.D1002);

        // 禁止删除系统参数
        if (config.SysFlag == YesNoEnum.Y) throw Oops.Oh(ErrorCodeEnum.D9001);

        await _sysConfigValueRep.DeleteAsync(u => u.ConfigId == config.Id);
        await _sysConfigRep.DeleteAsync(config);
        Remove(config);
    }

    /// <summary>
    /// 批量删除参数配置 🔖
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "BatchDelete"), HttpPost]
    [DisplayName("批量删除参数配置")]
    [UnitOfWork]
    public async Task BatchDeleteConfig(List<long> ids)
    {
        if (!_userManager.SuperAdmin) throw Oops.Oh(ErrorCodeEnum.D3010);
        ids = ids.Distinct().ToList();
        foreach (var id in ids)
        {
            var config = await _sysConfigRep.GetFirstAsync(u => u.Id == id);
            if (config == null) continue;

            // 禁止删除系统参数
            if (config.SysFlag == YesNoEnum.Y) continue;

            await _sysConfigRep.DeleteAsync(config);
            await _sysConfigValueRep.DeleteAsync(u => u.ConfigId == config.Id);

            Remove(config);
        }
    }

    /// <summary>
    /// 获取参数配置详情 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取参数配置详情")]
    public async Task<ConfigOutput> GetDetail([FromQuery] ConfigInput input)
    {
        var query = await GetConfigQueryable();
        query = query.WhereIF(!_userManager.SuperAdmin, u => u.SysFlag == YesNoEnum.N);
        var config = await query.FirstAsync(u => u.Id == input.Id)
            ?? throw Oops.Oh(ErrorCodeEnum.D1002);
        return ToSafeOutput(config);
    }

    /// <summary>
    /// 获取参数配置值
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    [NonAction]
    public async Task<T> GetConfigValue<T>(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return default;

        var value = _sysCacheService.Get<string>($"{CacheConst.KeyConfig}{code}");
        if (string.IsNullOrEmpty(value))
        {
            var query = await GetConfigQueryable();
            var config = await query.FirstAsync(u => u.Code == code);
            value = config?.Value;
            _sysCacheService.Set($"{CacheConst.KeyConfig}{code}", value);
        }
        if (string.IsNullOrWhiteSpace(value)) return default;
        return (T)Convert.ChangeType(value, typeof(T));
    }

    /// <summary>
    /// 获取参数配置查询器
    /// </summary>
    /// <returns></returns>
    [NonAction]
    public Task<ISugarQueryable<SysConfig>> GetConfigQueryable()
    {
        var tenantId = _userManager.TenantId;
        if (_userManager.TenantId <= 0) tenantId = SqlSugarConst.DefaultTenantId;
        return Task.FromResult(
            _sysConfigRep.CopyNew().AsQueryable()
                .LeftJoin<SysConfigValue>((u, w) => u.Id == w.ConfigId).ClearFilter()
                .Where((u, w) => w.TenantId == null || w.TenantId == tenantId)
                .Select((u, w) => new SysConfig
                {
                    Id = u.Id,
                    Name = u.Name,
                    Code = u.Code,
                    GroupCode = u.GroupCode,
                    OrderNo = u.OrderNo,
                    SysFlag = u.SysFlag,
                    Remark = u.Remark,
                    Value = w.Value ?? u.Value,
                    CreateTime = SqlFunc.IIF(u.SysFlag == YesNoEnum.Y, u.CreateTime, w.CreateTime),
                    UpdateTime = SqlFunc.IIF(u.SysFlag == YesNoEnum.Y, u.UpdateTime, w.UpdateTime),
                    CreateUserId = SqlFunc.IIF(u.SysFlag == YesNoEnum.Y, u.CreateUserId, w.CreateUserId),
                    CreateUserName = SqlFunc.IIF(u.SysFlag == YesNoEnum.Y, u.CreateUserName, w.CreateUserName),
                    UpdateUserId = SqlFunc.IIF(u.SysFlag == YesNoEnum.Y, u.UpdateUserId, w.UpdateUserId),
                    UpdateUserName = SqlFunc.IIF(u.SysFlag == YesNoEnum.Y, u.UpdateUserName, w.UpdateUserName),
                })
            );
    }

    /// <summary>
    /// 更新参数配置值
    /// </summary>
    /// <param name="code"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    [NonAction]
    public async Task UpdateConfigValue(string code, string value)
    {
        var query = await GetConfigQueryable();
        var config = await query.FirstAsync(u => u.Code == code);
        if (config == null) return;

        config.Value = value;
        await UpdateConfig(config.Adapt<UpdateConfigInput>());

        Remove(config);
    }

    /// <summary>
    /// 获取分组列表 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取分组列表")]
    public async Task<List<string>> GetGroupList()
    {
        var query = await GetConfigQueryable();
        return await query.GroupBy(u => u.GroupCode).Select(u => u.GroupCode).ToListAsync();
    }

    /// <summary>
    /// 获取 Token 过期时间
    /// </summary>
    /// <returns></returns>
    [NonAction]
    public async Task<int> GetTokenExpire()
    {
        var tokenExpireStr = await GetConfigValue<string>(ConfigConst.SysTokenExpire);
        _ = int.TryParse(tokenExpireStr, out var tokenExpire);
        return tokenExpire == 0 ? 20 : tokenExpire;
    }

    /// <summary>
    /// 获取 RefreshToken 过期时间
    /// </summary>
    /// <returns></returns>
    [NonAction]
    public async Task<int> GetRefreshTokenExpire()
    {
        var refreshTokenExpireStr = await GetConfigValue<string>(ConfigConst.SysRefreshTokenExpire);
        _ = int.TryParse(refreshTokenExpireStr, out var refreshTokenExpire);
        return refreshTokenExpire == 0 ? 40 : refreshTokenExpire;
    }

    /// <summary>
    /// 批量更新参数配置值
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "BatchUpdate"), HttpPost]
    [DisplayName("批量更新参数配置值")]
    public async Task BatchUpdateConfig(List<BatchConfigInput> input)
    {
        var query = await GetConfigQueryable();
        foreach (var config in input)
        {
            var info = await query.FirstAsync(c => c.Code == config.Code);
            if (info == null || info.SysFlag == YesNoEnum.Y) continue;

            info.Value = config.Value;
            await UpdateConfig(info.Adapt<UpdateConfigInput>());
            Remove(info);
        }
    }

    /// <summary>
    /// 获取系统信息 🔖
    /// </summary>
    /// <returns></returns>
    [SuppressMonitor]
    [AllowAnonymous]
    [DisplayName("获取系统信息")]
    public async Task<dynamic> GetSysInfo()
    {
        var tenant = await SysTenantService.GetCurrentTenant();
        tenant ??= await _sysTenantRep.GetFirstAsync(u => u.Id == SqlSugarConst.DefaultTenantId);
        _ = tenant ?? throw Oops.Oh(ErrorCodeEnum.D1002);

        var wayList = await _sysConfigRep.Change<SysUserRegWay>().AsQueryable().ClearFilter()
            .Where(u => u.TenantId == tenant.Id)
            .Select(u => new { Label = u.Name, Value = u.Id })
            .ToListAsync();

        var captcha = await GetConfigValue<bool>(ConfigConst.SysCaptcha);
        var secondVer = await GetConfigValue<bool>(ConfigConst.SysSecondVer);
        var hideTenantForLogin = await GetConfigValue<bool>(ConfigConst.SysHideTenantLogin);
        return new
        {
            tenant.Logo,
            tenant.Title,
            tenant.ViceTitle,
            tenant.ViceDesc,
            tenant.Watermark,
            tenant.Copyright,
            tenant.Icp,
            tenant.IcpUrl,
            tenant.RegWayId,
            tenant.EnableReg,
            SecondVer = secondVer ? YesNoEnum.Y : YesNoEnum.N,
            Captcha = captcha ? YesNoEnum.Y : YesNoEnum.N,
            HideTenantForLogin = hideTenantForLogin,
            WayList = wayList
        };
    }

    /// <summary>
    /// 保存系统信息 🔖
    /// </summary>
    /// <returns></returns>
    [UnitOfWork]
    [DisplayName("保存系统信息")]
    public async Task SaveSysInfo(InfoSaveInput input)
    {
        EnsureSystemAdmin();
        ValidateSystemInfo(input);
        var tenant = await SysTenantService.GetCurrentTenant() ?? throw Oops.Oh(ErrorCodeEnum.D1002);
        if (!string.IsNullOrEmpty(input.LogoBase64)) SysTenantService.SetLogoUrl(tenant, input.LogoBase64, input.LogoFileName);
        // await UpdateConfigValue(ConfigConst.SysCaptcha, (input.Captcha == YesNoEnum.Y).ToString());
        // await UpdateConfigValue(ConfigConst.SysSecondVer, (input.SecondVer == YesNoEnum.Y).ToString());

        tenant.Copy(input);
        tenant.RegWayId = input.EnableReg == YesNoEnum.Y ? input.RegWayId : null;
        await _sysConfigRep.Context.Updateable(tenant).ExecuteCommandAsync();
    }

    private void EnsureSystemAdmin()
    {
        if (!_userManager.SuperAdmin && !_userManager.SysAdmin)
            throw Oops.Oh("仅超级管理员或系统管理员可修改系统信息");
    }

    private static void ValidateSystemInfo(InfoSaveInput input)
    {
        if (!Uri.TryCreate(input.IcpUrl, UriKind.Absolute, out var icpUri)
            || (icpUri.Scheme != Uri.UriSchemeHttp && icpUri.Scheme != Uri.UriSchemeHttps))
            throw Oops.Oh("ICP 地址必须是有效的 http 或 https 地址");

        if (string.IsNullOrWhiteSpace(input.LogoBase64)) return;
        var match = Regex.Match(input.LogoBase64, @"^data:image/(?<type>png|jpeg);base64,(?<data>[A-Za-z0-9+/=]+)$", RegexOptions.IgnoreCase);
        if (!match.Success) throw Oops.Oh("系统图标仅支持 PNG、JPG 或 JPEG 格式");

        byte[] logoBytes;
        try
        {
            logoBytes = Convert.FromBase64String(match.Groups["data"].Value);
        }
        catch (FormatException)
        {
            throw Oops.Oh("系统图标内容不是有效的 Base64 数据");
        }

        if (logoBytes.Length > 2 * 1024 * 1024) throw Oops.Oh("系统图标不能超过 2MB");
        var extension = Path.GetExtension(input.LogoFileName)?.ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg")
            throw Oops.Oh("系统图标文件扩展名仅支持 .png、.jpg 或 .jpeg");
    }

    private void Remove(SysConfig config)
    {
        _sysCacheService.Remove($"{CacheConst.KeyConfig}Value:{config.Code}");
        _sysCacheService.Remove($"{CacheConst.KeyConfig}Remark:{config.Code}");
        _sysCacheService.Remove($"{CacheConst.KeyConfig}{config.GroupCode}:GroupWithCache");
        _sysCacheService.Remove($"{CacheConst.KeyConfig}{config.Code}");
    }

    private const string SensitiveMask = "******";

    private static bool IsSensitiveCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        return code.Equals(ConfigConst.SysPassword, StringComparison.OrdinalIgnoreCase)
            || code.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || code.EndsWith("_password", StringComparison.OrdinalIgnoreCase)
            || code.EndsWith("_private_key", StringComparison.OrdinalIgnoreCase);
    }

    private async Task UpsertTenantValue(long configId, string? value)
    {
        var tenantId = _userManager.TenantId > 0 ? _userManager.TenantId : SqlSugarConst.DefaultTenantId;
        var configValue = await _sysConfigValueRep.AsQueryable().ClearFilter()
            .SingleAsync(u => u.ConfigId == configId && u.TenantId == tenantId);
        if (configValue == null)
        {
            await _sysConfigValueRep.AsInsertable(new SysConfigValue
            {
                ConfigId = configId,
                TenantId = tenantId,
                Value = value,
            }).ExecuteCommandAsync();
            return;
        }

        configValue.Value = value;
        await _sysConfigValueRep.AsUpdateable(configValue)
            .UpdateColumns(u => new { u.Value })
            .ExecuteCommandAsync();
    }

    private static ConfigOutput ToSafeOutput(SysConfig config)
    {
        var sensitive = IsSensitiveCode(config.Code);
        return new ConfigOutput
        {
            Id = config.Id,
            Name = config.Name,
            Code = config.Code,
            Value = sensitive ? SensitiveMask : config.Value,
            IsSensitive = sensitive,
            SysFlag = config.SysFlag,
            GroupCode = config.GroupCode,
            OrderNo = config.OrderNo,
            Remark = config.Remark,
            CreateTime = config.CreateTime,
            UpdateTime = config.UpdateTime,
            CreateUserName = config.CreateUserName,
            UpdateUserName = config.UpdateUserName,
        };
    }
}
