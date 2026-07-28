// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.Security.Claims;
using System.Security.Cryptography;

namespace Admin.NET.Core.Service;

/// <summary>
/// 开放接口身份服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 244)]
public class SysOpenAccessService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<SysOpenAccess> _sysOpenAccessRep;
    private readonly SqlSugarRepository<SysTenant> _sysTenantRep;
    private readonly SqlSugarRepository<SysUser> _sysUserRep;
    private readonly SysCacheService _sysCacheService;
    private readonly UserManager _userManager;

    /// <summary>
    /// 开放接口身份服务构造函数
    /// </summary>
    public SysOpenAccessService(SqlSugarRepository<SysOpenAccess> sysOpenAccessRep,
        SqlSugarRepository<SysTenant> sysTenantRep,
        SqlSugarRepository<SysUser> sysUserRep,
        SysCacheService sysCacheService,
        UserManager userManager)
    {
        _sysOpenAccessRep = sysOpenAccessRep;
        _sysTenantRep = sysTenantRep;
        _sysUserRep = sysUserRep;
        _sysCacheService = sysCacheService;
        _userManager = userManager;
    }

    /// <summary>
    /// 生成签名
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("生成签名")]
    public string GenerateSignature(GenerateSignatureInput input)
    {
        EnsureSystemAdmin();
        return GenerateSignatureCore(input.AccessKey, input.AccessSecret, input.Method, input.Url, input.Timestamp, input.Nonce);
    }

    /// <summary>
    /// 获取开放接口身份分页列表 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取开放接口身份分页列表")]
    public async Task<SqlSugarPagedList<OpenAccessOutput>> Page(OpenAccessInput input)
    {
        EnsureSystemAdmin();
        return await _sysOpenAccessRep.AsQueryable()
            .LeftJoin<SysUser>((u, a) => u.BindUserId == a.Id)
            .LeftJoin<SysTenant>((u, a, b) => u.BindTenantId == b.Id)
            .LeftJoin<SysOrg>((u, a, b, c) => b.OrgId == c.Id)
            .WhereIF(!_userManager.SuperAdmin, (u, a, b, c) => u.BindTenantId == _userManager.TenantId)
            .WhereIF(!string.IsNullOrWhiteSpace(input.AccessKey?.Trim()), (u, a, b, c) => u.AccessKey.Contains(input.AccessKey))
            .Select((u, a, b, c) => new OpenAccessOutput
            {
                BindUserAccount = a.Account,
                BindTenantName = c.Name,
            }, true)
            .ToPagedListAsync(input.Page, input.PageSize);
    }

    /// <summary>
    /// 获取不包含密钥的开放接口身份分页列表
    /// </summary>
    [DisplayName("获取开放接口身份安全分页列表")]
    public async Task<SqlSugarPagedList<OpenAccessSafeOutput>> PageSafe(OpenAccessInput input)
    {
        EnsureSystemAdmin();
        return await _sysOpenAccessRep.AsQueryable()
            .LeftJoin<SysUser>((u, a) => u.BindUserId == a.Id)
            .LeftJoin<SysTenant>((u, a, b) => u.BindTenantId == b.Id)
            .LeftJoin<SysOrg>((u, a, b, c) => b.OrgId == c.Id)
            .WhereIF(!_userManager.SuperAdmin, (u, a, b, c) => u.BindTenantId == _userManager.TenantId)
            .WhereIF(!string.IsNullOrWhiteSpace(input.AccessKey?.Trim()), (u, a, b, c) => u.AccessKey.Contains(input.AccessKey.Trim()))
            .Select((u, a, b, c) => new OpenAccessSafeOutput
            {
                Id = u.Id,
                AccessKey = u.AccessKey,
                BindTenantId = u.BindTenantId,
                BindUserId = u.BindUserId,
                BindUserAccount = a.Account,
                BindTenantName = c.Name,
                CreateTime = u.CreateTime,
                UpdateTime = u.UpdateTime,
                CreateUserName = u.CreateUserName,
                UpdateUserName = u.UpdateUserName,
            })
            .OrderByDescending(u => u.Id)
            .ToPagedListAsync(input.Page, input.PageSize);
    }

    /// <summary>
    /// 增加开放接口身份 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Add"), HttpPost]
    [DisplayName("增加开放接口身份")]
    public async Task AddOpenAccess(AddOpenAccessInput input)
    {
        await EnsureBindingAsync(input.BindTenantId, input.BindUserId);
        if (await _sysOpenAccessRep.AsQueryable().AnyAsync(u => u.AccessKey == input.AccessKey && u.Id != input.Id))
            throw Oops.Oh(ErrorCodeEnum.O1000);

        var openAccess = input.Adapt<SysOpenAccess>();
        await _sysOpenAccessRep.InsertAsync(openAccess);
    }

    /// <summary>
    /// 更新开放接口身份 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    [DisplayName("更新开放接口身份")]
    public async Task UpdateOpenAccess(UpdateOpenAccessInput input)
    {
        await EnsureBindingAsync(input.BindTenantId, input.BindUserId);
        if (await _sysOpenAccessRep.AsQueryable().AnyAsync(u => u.AccessKey == input.AccessKey && u.Id != input.Id))
            throw Oops.Oh(ErrorCodeEnum.O1000);

        var existing = await GetManageableOpenAccessAsync(input.Id);
        _sysCacheService.Remove(CacheConst.KeyOpenAccess + existing.AccessKey);

        var openAccess = input.Adapt<SysOpenAccess>();
        await _sysOpenAccessRep.UpdateAsync(openAccess);
        _sysCacheService.Remove(CacheConst.KeyOpenAccess + openAccess.AccessKey);
    }

    /// <summary>
    /// 更新开放接口身份但保留原密钥
    /// </summary>
    [DisplayName("安全更新开放接口身份")]
    public async Task UpdateSafe(UpdateOpenAccessSafeInput input)
    {
        await EnsureBindingAsync(input.BindTenantId, input.BindUserId);
        if (await _sysOpenAccessRep.AsQueryable().AnyAsync(u => u.AccessKey == input.AccessKey && u.Id != input.Id))
            throw Oops.Oh(ErrorCodeEnum.O1000);

        var openAccess = await GetManageableOpenAccessAsync(input.Id);
        var oldAccessKey = openAccess.AccessKey;
        openAccess.AccessKey = input.AccessKey.Trim();
        openAccess.BindTenantId = input.BindTenantId;
        openAccess.BindUserId = input.BindUserId;
        await _sysOpenAccessRep.UpdateAsync(openAccess);

        _sysCacheService.Remove(CacheConst.KeyOpenAccess + oldAccessKey);
        _sysCacheService.Remove(CacheConst.KeyOpenAccess + openAccess.AccessKey);
    }

    /// <summary>
    /// 删除开放接口身份 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Delete"), HttpPost]
    [DisplayName("删除开放接口身份")]
    public async Task DeleteOpenAccess(DeleteOpenAccessInput input)
    {
        var openAccess = await GetManageableOpenAccessAsync(input.Id);
        _sysCacheService.Remove(CacheConst.KeyOpenAccess + openAccess.AccessKey);

        await _sysOpenAccessRep.DeleteAsync(u => u.Id == input.Id);
    }

    /// <summary>
    /// 创建密钥 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("创建密钥")]
    public async Task<string> CreateSecret()
    {
        EnsureSystemAdmin();
        return await Task.FromResult(Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..^2]);
    }

    /// <summary>
    /// 轮换开放接口密钥，返回的新密钥只应展示一次
    /// </summary>
    [DisplayName("轮换开放接口密钥")]
    public async Task<string> RotateSecret(BaseIdInput input)
    {
        var openAccess = await GetManageableOpenAccessAsync(input.Id);
        openAccess.AccessSecret = Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..^2];
        await _sysOpenAccessRep.UpdateAsync(openAccess);
        _sysCacheService.Remove(CacheConst.KeyOpenAccess + openAccess.AccessKey);
        return openAccess.AccessSecret;
    }

    /// <summary>
    /// 使用服务端保存的密钥生成签名，密钥不会返回浏览器
    /// </summary>
    [DisplayName("使用已保存密钥生成签名")]
    public async Task<string> GenerateStoredSignature(GenerateStoredSignatureInput input)
    {
        var openAccess = await GetManageableOpenAccessAsync(input.Id);
        return GenerateSignatureCore(openAccess.AccessKey, openAccess.AccessSecret, input.Method, input.Url, input.Timestamp, input.Nonce);
    }

    /// <summary>
    /// 根据 Key 获取对象
    /// </summary>
    /// <param name="accessKey"></param>
    /// <returns></returns>
    [NonAction]
    public async Task<SysOpenAccess> GetByKey(string accessKey)
    {
        return await Task.FromResult(
            _sysCacheService.GetOrAdd(CacheConst.KeyOpenAccess + accessKey, _ =>
            {
                return _sysOpenAccessRep.AsQueryable()
                    .Includes(u => u.BindUser)
                    .Includes(u => u.BindUser, p => p.SysOrg)
                    .First(u => u.AccessKey == accessKey);
            })
        );
    }

    /// <summary>
    /// Signature 身份验证事件默认实现
    /// </summary>
    [NonAction]
    public static SignatureAuthenticationEvent GetSignatureAuthenticationEventImpl()
    {
        return new SignatureAuthenticationEvent
        {
            OnGetAccessSecret = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<SysOpenAccessService>>();
                try
                {
                    var openAccessService = context.HttpContext.RequestServices.GetRequiredService<SysOpenAccessService>();
                    var openAccess = openAccessService.GetByKey(context.AccessKey).GetAwaiter().GetResult();
                    return Task.FromResult(openAccess == null ? "" : openAccess.AccessSecret);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "开放接口身份验证");
                    return Task.FromResult("");
                }
            },
            OnValidated = context =>
            {
                var openAccessService = context.HttpContext.RequestServices.GetRequiredService<SysOpenAccessService>();
                var openAccess = openAccessService.GetByKey(context.AccessKey).GetAwaiter().GetResult();
                var identity = ((ClaimsIdentity)context.Principal!.Identity!);

                identity.AddClaims(new[]
                {
                    new Claim(ClaimConst.UserId, openAccess.BindUserId + ""),
                    new Claim(ClaimConst.TenantId, openAccess.BindTenantId + ""),
                    new Claim(ClaimConst.Account, openAccess.BindUser.Account + ""),
                    new Claim(ClaimConst.RealName, openAccess.BindUser.RealName),
                    new Claim(ClaimConst.AccountType, ((int)openAccess.BindUser.AccountType).ToString()),
                    new Claim(ClaimConst.OrgId, openAccess.BindUser.OrgId + ""),
                    new Claim(ClaimConst.OrgName, openAccess.BindUser.SysOrg?.Name + ""),
                    new Claim(ClaimConst.OrgType, openAccess.BindUser.SysOrg?.Type + ""),
                });
                return Task.CompletedTask;
            }
        };
    }

    private void EnsureSystemAdmin()
    {
        if (!_userManager.SuperAdmin && !_userManager.SysAdmin)
            throw Oops.Oh("仅超级管理员或系统管理员可管理开放接口身份");
    }

    private async Task EnsureBindingAsync(long tenantId, long userId)
    {
        EnsureSystemAdmin();
        if (!_userManager.SuperAdmin && tenantId != _userManager.TenantId)
            throw Oops.Oh("系统管理员只能管理当前租户的开放接口身份");

        if (!await _sysTenantRep.AsQueryable().ClearFilter().AnyAsync(u => u.Id == tenantId && !u.IsDelete))
            throw Oops.Oh("绑定租户不存在或已删除");
        if (!await _sysUserRep.AsQueryable().ClearFilter().AnyAsync(u => u.Id == userId && u.TenantId == tenantId && !u.IsDelete))
            throw Oops.Oh("绑定用户不存在或不属于所选租户");
    }

    private async Task<SysOpenAccess> GetManageableOpenAccessAsync(long id)
    {
        EnsureSystemAdmin();
        var openAccess = await _sysOpenAccessRep.GetFirstAsync(u => u.Id == id) ?? throw Oops.Oh("开放接口身份不存在");
        if (!_userManager.SuperAdmin && openAccess.BindTenantId != _userManager.TenantId)
            throw Oops.Oh("无权管理其他租户的开放接口身份");
        return openAccess;
    }

    private static string GenerateSignatureCore(string accessKey, string accessSecret, HttpMethodEnum method, string url, long timestamp, string nonce)
    {
        var appSecretByte = Encoding.UTF8.GetBytes(accessSecret);
        var parameter = $"{method.ToString().ToUpper()}&{url}&{accessKey}&{timestamp}&{nonce}";
        using HMAC hmac = new HMACSHA256 { Key = appSecretByte };
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(parameter)));
    }
}
