// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统租户管理服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 390)]
public class SysTenantService : IDynamicApiController, ITransient
{
    private static readonly SysMenuService SysMenuService = App.GetService<SysMenuService>();
    private readonly SqlSugarRepository<SysUserExtOrg> _sysUserExtOrgRep;
    private readonly SqlSugarRepository<SysTenantMenu> _sysTenantMenuRep;
    private readonly SqlSugarRepository<SysRoleMenu> _sysRoleMenuRep;
    private readonly SqlSugarRepository<SysUserRole> _userRoleRep;
    private readonly SqlSugarRepository<SysTenant> _sysTenantRep;
    private readonly SqlSugarRepository<SysRole> _sysRoleRep;
    private readonly SqlSugarRepository<SysUser> _sysUserRep;
    private readonly SqlSugarRepository<SysOrg> _sysOrgRep;
    private readonly SqlSugarRepository<SysPos> _sysPosRep;
    private readonly SysRoleMenuService _sysRoleMenuService;
    private readonly SysConfigService _sysConfigService;
    private readonly SysCacheService _sysCacheService;
    private readonly UploadOptions _uploadOptions;

    public SysTenantService(
        SqlSugarRepository<SysUserExtOrg> sysUserExtOrgRep,
        SqlSugarRepository<SysTenantMenu> sysTenantMenuRep,
        SqlSugarRepository<SysRoleMenu> sysRoleMenuRep,
        SqlSugarRepository<SysUserRole> userRoleRep,
        SqlSugarRepository<SysTenant> sysTenantRep,
        SqlSugarRepository<SysUser> sysUserRep,
        SqlSugarRepository<SysRole> sysRoleRep,
        SqlSugarRepository<SysOrg> sysOrgRep,
        SqlSugarRepository<SysPos> sysPosRep,
        IOptions<UploadOptions> uploadOptions,
        SysRoleMenuService sysRoleMenuService,
        SysConfigService sysConfigService,
        SysCacheService sysCacheService)
    {
        _sysTenantRep = sysTenantRep;
        _sysOrgRep = sysOrgRep;
        _sysRoleRep = sysRoleRep;
        _sysPosRep = sysPosRep;
        _sysUserRep = sysUserRep;
        _userRoleRep = userRoleRep;
        _sysRoleMenuRep = sysRoleMenuRep;
        _sysCacheService = sysCacheService;
        _uploadOptions = uploadOptions.Value;
        _sysConfigService = sysConfigService;
        _sysTenantMenuRep = sysTenantMenuRep;
        _sysUserExtOrgRep = sysUserExtOrgRep;
        _sysRoleMenuService = sysRoleMenuService;
    }

    /// <summary>
    /// 获取租户分页列表 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取租户分页列表")]
    public async Task<SqlSugarPagedList<TenantOutput>> Page(PageTenantInput input)
    {
        return await _sysTenantRep.AsQueryable()
            .LeftJoin<SysUser>((u, a) => u.UserId == a.Id)
            .LeftJoin<SysOrg>((u, a, b) => u.OrgId == b.Id)
            .WhereIF(!string.IsNullOrWhiteSpace(input.Phone), (u, a) => a.Phone.Contains(input.Phone.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.Name), (u, a, b) => b.Name.Contains(input.Name.Trim()))
            .OrderBy(u => new { u.OrderNo, u.Id })
            .Select((u, a, b) => new TenantOutput
            {
                Id = u.Id,
                OrgId = b.Id,
                Name = b.Name,
                UserId = a.Id,
                AdminAccount = a.Account,
                Phone = a.Phone,
                Host = u.Host,
                Email = a.Email,
                TenantType = u.TenantType,
                DbType = u.DbType,
                Connection = u.Connection,
                ConfigId = u.ConfigId,
                OrderNo = u.OrderNo,
                Remark = u.Remark,
                Status = u.Status,
                CreateTime = u.CreateTime,
                CreateUserName = u.CreateUserName,
                UpdateTime = u.UpdateTime,
                UpdateUserName = u.UpdateUserName,
            }, true)
            .ToPagedListAsync(input.Page, input.PageSize);
    }

    /// <summary>
    /// 获取租户列表
    /// </summary>
    /// <returns></returns>
    [AllowAnonymous]
    [DisplayName("获取租户列表"), HttpGet]
    public async Task<dynamic> GetList()
    {
        return await _sysTenantRep.AsQueryable()
           .LeftJoin<SysOrg>((u, a) => u.OrgId == a.Id).ClearFilter()
           .Where(u => u.Status == StatusEnum.Enable && u.IsDelete == false)
           .Select((u, a) => new
           {
               Label = $"{u.Title}-{a.Name}",
               Host = u.Host.ToLower(),
               Value = u.Id,
           }).ToListAsync();
    }

    /// <summary>
    /// 获取当前租户
    /// </summary>
    /// <returns></returns>
    [NonAction]
    public async Task<SysTenant> GetCurrentTenant()
    {
        var tenantId = long.Parse(App.User?.FindFirst(ClaimConst.TenantId)?.Value ?? "0");
        var host = App.HttpContext.Request.Host.Host.ToLower();
        return await _sysTenantRep.AsQueryable()
            .WhereIF(tenantId > 0, u => u.Id == tenantId)
            .WhereIF(!(tenantId > 0), u => SqlFunc.ToLower(u.Host).Equals(host))
            .FirstAsync();
    }

    /// <summary>
    /// 获取库隔离的租户列表
    /// </summary>
    /// <returns></returns>
    [NonAction]
    public async Task<List<SysTenant>> GetTenantDbList()
    {
        return await _sysTenantRep.GetListAsync(u => u.TenantType == TenantTypeEnum.Db && u.Status == StatusEnum.Enable);
    }

    /// <summary>
    /// 增加租户 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [UnitOfWork]
    [ApiDescriptionSettings(Name = "Add"), HttpPost]
    [DisplayName("增加租户")]
    public async Task AddTenant(AddTenantInput input)
    {
        var isExist = await _sysOrgRep.IsAnyAsync(u => u.Name == input.Name);
        if (isExist) throw Oops.Oh(ErrorCodeEnum.D1300);

        input.Host = input.Host?.ToLower();
        isExist = await _sysTenantRep.IsAnyAsync(u => !string.IsNullOrWhiteSpace(u.Host) && u.Host == input.Host);
        if (isExist) throw Oops.Oh(ErrorCodeEnum.D1303);

        isExist = await _sysUserRep.AsQueryable().ClearFilter().AnyAsync(u => u.Account == input.AdminAccount);
        if (isExist) throw Oops.Oh(ErrorCodeEnum.D1301);

        // 从库配置判断
        if (!string.IsNullOrWhiteSpace(input.SlaveConnections) && !JSON.IsValid(input.SlaveConnections)) throw Oops.Oh(ErrorCodeEnum.D1302);

        switch (input.TenantType)
        {
            // Id隔离时设置与主库一致
            case TenantTypeEnum.Id:
                var config = _sysTenantRep.AsSugarClient().CurrentConnectionConfig;
                input.DbType = config.DbType;
                input.Connection = config.ConnectionString;
                break;

            case TenantTypeEnum.Db:
                if (string.IsNullOrWhiteSpace(input.Connection))
                    throw Oops.Oh(ErrorCodeEnum.Z1004);
                break;

            default:
                throw Oops.Oh(ErrorCodeEnum.D3004);
        }
        if (input.EnableReg == YesNoEnum.N) input.RegWayId = null;
        var tenant = input.Adapt<TenantOutput>();

        // 设置logo
        tenant.Logo = null;
        SetLogoUrl(tenant, input.LogoBase64, input.LogoFileName);

        // 取最大`id + 100` 为新租户`id`，方便数据维护
        tenant.Id = await _sysTenantRep.AsQueryable().MaxAsync(u => u.Id) + 100;

        await _sysTenantRep.InsertAsync(tenant);
        await InitNewTenant(tenant);

        await CacheTenant();
    }

    /// <summary>
    /// 设置logo
    /// </summary>
    /// <param name="tenant"></param>
    /// <param name="logoBase64"></param>
    /// <param name="logoFileName"></param>
    [NonAction]
    public void SetLogoUrl(SysTenant tenant, string logoBase64, string logoFileName)
    {
        var match = Regex.Match(
            logoBase64,
            @"^data:image/(?<type>png|jpeg);base64,(?<data>[A-Za-z0-9+/=]+)$",
            RegexOptions.IgnoreCase);
        if (!match.Success) throw Oops.Oh("租户图标仅支持 PNG、JPG 或 JPEG 格式");

        byte[] binData;
        try
        {
            binData = Convert.FromBase64String(match.Groups["data"].Value);
        }
        catch (FormatException)
        {
            throw Oops.Oh("租户图标内容不是有效的 Base64 数据");
        }
        if (binData.Length > 2 * 1024 * 1024) throw Oops.Oh("租户图标不能超过 2MB");

        // 根据文件名取扩展名
        var ext = Path.GetExtension(logoFileName)?.ToLowerInvariant();
        if (ext is not ".png" and not ".jpg" and not ".jpeg")
            throw Oops.Oh("租户图标文件扩展名仅支持 .png、.jpg 或 .jpeg");

        // 本地图标保存路径
        var invalidFileNameChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar })
            .ToHashSet();
        var safeViceTitle = new string((tenant.ViceTitle ?? "tenant")
            .Select(u => invalidFileNameChars.Contains(u) ? '_' : u)
            .ToArray())
            .Trim();
        if (string.IsNullOrWhiteSpace(safeViceTitle)) safeViceTitle = "tenant";
        var fileName = $"{safeViceTitle}-logo{ext}".ToLowerInvariant();
        var path = GetTenantLogoUploadRoot();
        var absoluteFilePath = Path.Combine(path, fileName);

        // 只清理上传目录内的旧租户图标，禁止删除 wwwroot/images 等公共资源。
        DeleteManagedTenantLogo(tenant.Logo);

        // 创建文件夹
        var absoluteFileDir = Path.GetDirectoryName(absoluteFilePath);
        if (!Directory.Exists(absoluteFileDir)) Directory.CreateDirectory(absoluteFileDir);

        // 保存图标文件
        File.WriteAllBytes(absoluteFilePath, binData);

        // 保存图标配置
        tenant.Logo = $"/upload/{fileName}";
    }

    private string GetTenantLogoUploadRoot()
    {
        var path = _uploadOptions.Path.Replace("/{yyyy}/{MM}/{dd}", "");
        path = path.StartsWith("/") || Regex.IsMatch(path, "^[A-Z|a-z]:")
            ? path
            : Path.Combine(App.WebHostEnvironment.WebRootPath, path);
        return Path.GetFullPath(path);
    }

    private void DeleteManagedTenantLogo(string? logo)
    {
        if (string.IsNullOrWhiteSpace(logo)) return;

        var uploadRoot = GetTenantLogoUploadRoot()
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var absoluteFilePath = Path.GetFullPath(Path.Combine(
            App.WebHostEnvironment.WebRootPath,
            logo.TrimStart('/', '\\')));
        var managedUploadPrefix = uploadRoot + Path.DirectorySeparatorChar;
        if (absoluteFilePath.StartsWith(managedUploadPrefix, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(absoluteFilePath))
            File.Delete(absoluteFilePath);
    }

    /// <summary>
    /// 设置租户状态 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("设置租户状态")]
    public async Task<int> SetStatus(TenantInput input)
    {
        var tenant = await _sysTenantRep.GetFirstAsync(u => u.Id == input.Id);
        if (tenant == null || tenant.ConfigId == SqlSugarConst.MainConfigId) throw Oops.Oh(ErrorCodeEnum.Z1001);

        if (!Enum.IsDefined(typeof(StatusEnum), input.Status)) throw Oops.Oh(ErrorCodeEnum.D3005);

        tenant.Status = input.Status;
        return await _sysTenantRep.AsUpdateable(tenant).UpdateColumns(u => new { u.Status }).ExecuteCommandAsync();
    }

    /// <summary>
    /// 新增租户初始化
    /// </summary>
    /// <param name="tenant"></param>
    private async Task InitNewTenant(TenantOutput tenant)
    {
        var tenantId = tenant.Id;
        var tenantName = tenant.Name;

        // 初始化机构
        var newOrg = new SysOrg { TenantId = tenantId, Pid = 0, Name = tenantName, Code = tenantName, Remark = tenantName, };
        await _sysOrgRep.InsertAsync(newOrg);

        // 初始化默认角色
        var newRole = new SysRole { TenantId = tenantId, Name = CommonConst.DefaultBaseRoleName, Code = CommonConst.DefaultBaseRoleCode, DataScope = DataScopeEnum.Self, Remark = "此角色为系统自动创建角色" };
        var baseRole = await _sysRoleRep.InsertReturnEntityAsync(newRole);
        var baseRoleMenuIdList = GetBaseRoleMenuIdList().ToList();
        await _sysRoleMenuService.GrantRoleMenu(new RoleMenuInput { Id = baseRole.Id, MenuIdList = baseRoleMenuIdList.Select(u => u.MenuId).ToList() });

        // 初始化职位
        var newPos = new SysPos { TenantId = tenantId, Name = "管理员-" + tenantName, Code = tenantName, Remark = tenantName };
        await _sysPosRep.InsertAsync(newPos);

        // 初始化租户管理员账号
        var password = await _sysConfigService.GetConfigValue<string>(ConfigConst.SysPassword);
        var newUser = new SysUser
        {
            TenantId = tenantId,
            Account = tenant.AdminAccount,
            Password = CryptogramUtil.Encrypt(password),
            NickName = "系统管理员",
            Email = tenant.Email,
            Phone = tenant.Phone,
            AccountType = AccountTypeEnum.SysAdmin,
            OrgId = newOrg.Id,
            PosId = newPos.Id,
            Birthday = DateTime.Parse("2000-01-01"),
            RealName = "系统管理员",
            Remark = "系统管理员" + tenantName,
        };
        await _sysUserRep.InsertAsync(newUser);

        // 关联租户组织机构和管理员用户
        await _sysTenantRep.UpdateAsync(u => new SysTenant { UserId = newUser.Id, OrgId = newOrg.Id }, u => u.Id == tenantId);

        // 默认租户管理员角色菜单集合
        var menuList = GetTenantDefaultMenuList().ToList();
        await GrantMenu(new TenantMenuInput { Id = tenantId, MenuIdList = menuList.Select(u => u.MenuId).ToList() });
    }

    /// <summary>
    /// 获取租户默认菜单
    /// </summary>
    /// <param name="ignoreHome">如果某租户需要定制主页，可以忽略</param>
    /// <returns></returns>
    [NonAction]
    public IEnumerable<SysTenantMenu> GetTenantDefaultMenuList(bool ignoreHome = false)
    {
        var menuList = new List<SysMenu>();
        var allMenuList = new SysMenuSeedData().HasData().ToList();

        var dashboardMenu = allMenuList.First(u => u.Type == MenuTypeEnum.Dir && u.Title == "工作台");
        menuList.AddRange(allMenuList.ToChildList(u => u.Id, u => u.Pid, dashboardMenu.Id));
        if (ignoreHome) menuList = menuList.Where(u => !(u.Type == MenuTypeEnum.Menu && u.Name == "home")).ToList();

        var systemMenu = allMenuList.First(u => u.Type == MenuTypeEnum.Dir && u.Title == "系统管理");
        menuList.Add(systemMenu);
        menuList.AddRange(allMenuList.ToChildList(u => u.Id, u => u.Pid, u => u.Pid == systemMenu.Id && new[] { "账号管理", "角色管理", "机构管理", "职位管理", "个人中心", "通知公告" }.Contains(u.Title)));

        var platformMenu = allMenuList.First(u => u.Type == MenuTypeEnum.Dir && u.Title == "平台管理");
        menuList.Add(platformMenu);
        menuList.AddRange(allMenuList.ToChildList(u => u.Id, u => u.Pid, u => u.Pid == platformMenu.Id && new[] { "菜单管理", "字典管理", "模板管理", "系统配置" }.Contains(u.Title)));
        var dictMenu = menuList.First(u => u.Type == MenuTypeEnum.Menu && u.Title == "字典管理");
        menuList = menuList.Where(u => u.Pid != dictMenu.Id || !new[] { "编辑", "删除" }.Contains(u.Title)).ToList();

        var logMenu = allMenuList.First(u => u.Type == MenuTypeEnum.Dir && u.Title == "日志管理");
        menuList.Add(logMenu);
        menuList.AddRange(allMenuList.ToChildList(u => u.Id, u => u.Pid, u => u.Pid == logMenu.Id && new[] { "访问日志", "操作日志" }.Contains(u.Title)));
        var logMenuIds = menuList.Where(u => u.Type == MenuTypeEnum.Menu && new[] { "访问日志", "操作日志" }.Contains(u.Title)).Select(u => u.Id).ToList();
        menuList = menuList.Where(u => !logMenuIds.Contains(u.Pid) || !new[] { "清空" }.Contains(u.Title)).ToList();

        var flow = _sysTenantRep.Context.Queryable<SysMenu>().First(u => u.Type == MenuTypeEnum.Menu && u.Title == "审批流程");
        menuList.Add(allMenuList.First(u => u.Type == MenuTypeEnum.Dir && u.Title == "帮助文档"));
        menuList.Add(allMenuList.First(u => u.Type == MenuTypeEnum.Menu && u.Title == "关于项目"));
        if (flow != null) menuList.Add(flow);

        return menuList.Select(u => new SysTenantMenu
        {
            Id = CommonUtil.GetFixedHashCode("" + SqlSugarConst.DefaultTenantId + u.Id, 1300000000000),
            TenantId = SqlSugarConst.DefaultTenantId,
            MenuId = u.Id
        });
    }

    /// <summary>
    /// 获取租户默认菜单
    /// </summary>
    /// <returns></returns>
    [NonAction]
    public IEnumerable<SysTenantMenu> GetBaseRoleMenuIdList()
    {
        var menuList = new List<SysMenu>();
        var allMenuList = new SysMenuSeedData().HasData().ToList();

        var dashboardMenu = allMenuList.First(u => u.Type == MenuTypeEnum.Dir && u.Title == "工作台");
        menuList.AddRange(allMenuList.ToChildList(u => u.Id, u => u.Pid, dashboardMenu.Id));

        var systemMenu = allMenuList.First(u => u.Type == MenuTypeEnum.Dir && u.Title == "系统管理");
        menuList.Add(systemMenu);
        menuList.AddRange(allMenuList.ToChildList(u => u.Id, u => u.Pid, u => u.Pid == systemMenu.Id && new[] { "机构管理", "个人中心" }.Contains(u.Title)));
        menuList = menuList.Where(u => !new[] { "增加", "编辑", "删除" }.Contains(u.Title)).ToList();

        return menuList.Select(u => new SysTenantMenu
        {
            Id = CommonUtil.GetFixedHashCode("" + SqlSugarConst.DefaultTenantId + u.Id, 1300000000000),
            TenantId = SqlSugarConst.DefaultTenantId,
            MenuId = u.Id
        });
    }

    /// <summary>
    /// 删除租户 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [UnitOfWork]
    [ApiDescriptionSettings(Name = "Delete"), HttpPost]
    [DisplayName("删除租户")]
    public async Task DeleteTenant(DeleteTenantInput input)
    {
        // 禁止删除默认租户
        if (input.Id.ToString() == SqlSugarConst.MainConfigId) throw Oops.Oh(ErrorCodeEnum.D1023);

        var tenant = await _sysTenantRep.AsQueryable().ClearFilter()
            .FirstAsync(u => u.Id == input.Id && !u.IsDelete) ?? throw Oops.Oh(ErrorCodeEnum.D1002);

        // 若账号为开放接口绑定租户则禁止删除
        var isOpenAccessTenant = await _sysTenantRep.ChangeRepository<SqlSugarRepository<SysOpenAccess>>().IsAnyAsync(u => u.BindTenantId == input.Id);
        if (isOpenAccessTenant) throw Oops.Oh(ErrorCodeEnum.D1031);

        // 先取得关联主键，再删除关系表，避免删除主表后无法精确定位关系数据。
        var userIds = await _sysUserRep.AsQueryable().ClearFilter()
            .Where(u => u.TenantId == input.Id)
            .Select(u => u.Id)
            .ToListAsync();
        var roleIds = await _sysRoleRep.AsQueryable().ClearFilter()
            .Where(u => u.TenantId == input.Id)
            .Select(u => u.Id)
            .ToListAsync();

        await _sysTenantMenuRep.AsDeleteable().Where(u => u.TenantId == input.Id).ExecuteCommandAsync();

        if (userIds.Count > 0)
        {
            await _sysTenantRep.Context.Deleteable<SysUserMenu>()
                .Where(u => userIds.Contains(u.UserId))
                .ExecuteCommandAsync();
            await _userRoleRep.AsDeleteable()
                .Where(u => userIds.Contains(u.UserId))
                .ExecuteCommandAsync();
            await _sysUserExtOrgRep.AsDeleteable()
                .Where(u => userIds.Contains(u.UserId))
                .ExecuteCommandAsync();
        }

        if (roleIds.Count > 0)
        {
            await _sysRoleMenuRep.AsDeleteable()
                .Where(u => roleIds.Contains(u.RoleId))
                .ExecuteCommandAsync();
            await _sysTenantRep.Context.Deleteable<SysRoleOrg>()
                .Where(u => roleIds.Contains(u.RoleId))
                .ExecuteCommandAsync();
            await _userRoleRep.AsDeleteable()
                .Where(u => roleIds.Contains(u.RoleId))
                .ExecuteCommandAsync();
        }

        await _sysUserRep.AsDeleteable().Where(u => u.TenantId == input.Id).ExecuteCommandAsync();
        await _sysRoleRep.AsDeleteable().Where(u => u.TenantId == input.Id).ExecuteCommandAsync();
        await _sysOrgRep.AsDeleteable().Where(u => u.TenantId == input.Id).ExecuteCommandAsync();
        await _sysPosRep.AsDeleteable().Where(u => u.TenantId == input.Id).ExecuteCommandAsync();
        await _sysTenantRep.DeleteAsync(u => u.Id == input.Id);

        DeleteManagedTenantLogo(tenant.Logo);
        await CacheTenant(input.Id);
    }

    /// <summary>
    /// 更新租户 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    [DisplayName("更新租户")]
    public async Task UpdateTenant(UpdateTenantInput input)
    {
        var isExist = await _sysOrgRep.IsAnyAsync(u => u.Name == input.Name && u.Id != input.OrgId);
        if (isExist) throw Oops.Oh(ErrorCodeEnum.D1300);

        input.Host = input.Host?.ToLower();
        isExist = await _sysTenantRep.IsAnyAsync(u => !string.IsNullOrWhiteSpace(u.Host) && u.Host == input.Host && u.Id != input.Id);
        if (isExist) throw Oops.Oh(ErrorCodeEnum.D1303);

        isExist = await _sysUserRep.AsQueryable().ClearFilter().AnyAsync(u => u.Account == input.AdminAccount && u.Id != input.UserId);
        if (isExist) throw Oops.Oh(ErrorCodeEnum.D1301);

        // Id隔离时设置与主库一致
        switch (input.TenantType)
        {
            case TenantTypeEnum.Id:
                var config = _sysTenantRep.AsSugarClient().CurrentConnectionConfig;
                input.DbType = config.DbType;
                input.Connection = config.ConnectionString;
                break;

            case TenantTypeEnum.Db:
                if (string.IsNullOrWhiteSpace(input.Connection))
                    throw Oops.Oh(ErrorCodeEnum.Z1004);
                break;

            default:
                throw Oops.Oh(ErrorCodeEnum.D3004);
        }
        // 从库配置判断
        if (!string.IsNullOrWhiteSpace(input.SlaveConnections) && !JSON.IsValid(input.SlaveConnections))
            throw Oops.Oh(ErrorCodeEnum.D1302);

        // 设置logo
        var tenant = input.Adapt<SysTenant>();
        if (!string.IsNullOrWhiteSpace(input.LogoBase64)) SetLogoUrl(tenant, input.LogoBase64, input.LogoFileName);

        // 更新租户信息
        await _sysTenantRep.AsUpdateable(tenant).IgnoreColumns(true).ExecuteCommandAsync();

        // 更新系统机构
        await _sysOrgRep.UpdateAsync(u => new SysOrg() { Name = input.Name }, u => u.Id == input.OrgId);

        // 更新系统用户
        await _sysUserRep.UpdateAsync(u => new SysUser() { Account = input.AdminAccount, Phone = input.Phone, Email = input.Email }, u => u.Id == input.UserId);

        await CacheTenant(input.Id);
    }

    /// <summary>
    /// 授权租户菜单 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [UnitOfWork]
    [DisplayName("授权租户菜单")]
    public async Task GrantMenu(TenantMenuInput input)
    {
        _ = await _sysTenantRep.AsQueryable().ClearFilter()
            .FirstAsync(u => u.Id == input.Id && !u.IsDelete) ?? throw Oops.Oh(ErrorCodeEnum.D1002);

        input.MenuIdList = (input.MenuIdList ?? new List<long>())
            .Where(u => u > 0)
            .Distinct()
            .ToList();

        // 获取需要授权的菜单列表
        var menuList = await _sysTenantRep.Context.Queryable<SysMenu>()
            .Where(u => input.MenuIdList.Contains(u.Id))
            .Distinct()
            .ToListAsync();
        if (menuList.Select(u => u.Id).Distinct().Count() != input.MenuIdList.Count)
            throw Oops.Oh(ErrorCodeEnum.D1002);

        // 检查是否存在重复菜单
        if (menuList.Where(u => u.Type != MenuTypeEnum.Btn).GroupBy(u => new { u.Pid, u.Title }).Any(u => u.Count() > 1) ||
            menuList.Where(u => u.Type == MenuTypeEnum.Btn).GroupBy(u => u.Permission).Any(u => u.Count() > 1))
            throw Oops.Oh(ErrorCodeEnum.D1304);

        // 检查路由是否重复
        if (menuList.Where(u => !string.IsNullOrWhiteSpace(u.Name)).GroupBy(u => u.Name).Any(u => u.Count() > 1))
            throw Oops.Oh(ErrorCodeEnum.D4009);

        // 删除旧记录
        await _sysTenantMenuRep.AsDeleteable().Where(u => u.TenantId == input.Id).ExecuteCommandAsync();

        // 追加父级菜单
        var allIdList = await _sysTenantRep.Context.Queryable<SysMenu>().Select(u => new { u.Id, u.Pid }).ToListAsync();
        var pIdList = allIdList.ToChildList(u => u.Pid, u => u.Id, u => input.MenuIdList.Contains(u.Id)).Select(u => u.Pid).Distinct().ToList();
        input.MenuIdList = input.MenuIdList.Concat(pIdList).Distinct().Where(u => u != 0).ToList();

        // 保存租户菜单
        var sysTenantMenuList = input.MenuIdList.Select(menuId => new SysTenantMenu { TenantId = input.Id, MenuId = menuId }).ToList();
        await _sysTenantMenuRep.InsertRangeAsync(sysTenantMenuList);

        // 清除菜单权限缓存
        SysMenuService.DeleteMenuCache();
    }

    /// <summary>
    /// 获取租户菜单Id集合 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取租户菜单Id集合")]
    public async Task<List<long>> GetTenantMenuList([FromQuery] BaseIdInput input)
    {
        var menuIds = await _sysTenantMenuRep.AsQueryable().Where(u => u.TenantId == input.Id).Select(u => u.MenuId).Distinct().ToListAsync();
        return await SysMenuService.ExcludeParentMenuOfFullySelected(menuIds);
    }

    /// <summary>
    /// 重置租户管理员密码 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("重置租户管理员密码")]
    public async Task<string> ResetPwd(TenantUserInput input)
    {
        var password = await _sysConfigService.GetConfigValue<string>(ConfigConst.SysPassword);
        var encryptPassword = CryptogramUtil.Encrypt(password);
        await _sysUserRep.UpdateAsync(u => new SysUser { Password = encryptPassword }, u => u.Id == input.UserId);
        return password;
    }

    /// <summary>
    /// 切换租户 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [UnitOfWork]
    [DisplayName("切换租户")]
    public async Task<LoginOutput> ChangeTenant(BaseIdInput input)
    {
        var userId = (App.HttpContext?.User.FindFirst(ClaimConst.UserId)?.Value)?.ToLong();
        _ = await _sysTenantRep.GetFirstAsync(u => u.Id == input.Id) ?? throw Oops.Oh(ErrorCodeEnum.D1002);
        var user = await _sysUserRep.GetFirstAsync(u => u.Id == userId) ?? throw Oops.Oh(ErrorCodeEnum.D1002);
        user.TenantId = input.Id;

        return await GetAccessTokenInNotSingleLogin(user);
    }

    /// <summary>
    /// 进入租管端 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("进入租管端")]
    public async Task<LoginOutput> GoTenant(BaseIdInput input)
    {
        var tenant = await _sysTenantRep.GetFirstAsync(u => u.Id == input.Id) ?? throw Oops.Oh(ErrorCodeEnum.D1002);
        var user = await _sysUserRep.GetFirstAsync(u => u.Id == tenant.UserId) ?? throw Oops.Oh(ErrorCodeEnum.D1002);
        return await GetAccessTokenInNotSingleLogin(user);
    }

    /// <summary>
    /// 同步授权菜单(用于版本更新后，同步授权数据) 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [UnitOfWork]
    [DisplayName("同步授权菜单")]
    public async Task SyncGrantMenu(BaseIdInput input)
    {
        _ = await _sysTenantRep.AsQueryable().ClearFilter()
            .FirstAsync(u => u.Id == input.Id && !u.IsDelete) ?? throw Oops.Oh(ErrorCodeEnum.D1002);

        var currentMenuIds = await _sysTenantMenuRep.AsQueryable()
            .Where(u => u.TenantId == input.Id)
            .Select(u => u.MenuId)
            .Distinct()
            .ToListAsync();
        var roleMenuIds = await _sysRoleRep.AsQueryable().ClearFilter()
            .Where(u => u.TenantId == input.Id)
            .InnerJoin<SysRoleMenu>((u, rm) => u.Id == rm.RoleId)
            .Select((u, rm) => rm.MenuId)
            .Distinct()
            .ToListAsync();
        var menuIdList = currentMenuIds.Concat(roleMenuIds).Distinct().ToList();

        // 默认租户应同步当前数据库的全部菜单，包括插件动态增加的菜单。
        if (input.Id == SqlSugarConst.DefaultTenantId)
        {
            var allMenuIds = await _sysTenantRep.Context.Queryable<SysMenu>()
                .Select(u => u.Id)
                .ToListAsync();
            menuIdList = menuIdList.Concat(allMenuIds).Distinct().ToList();
        }

        // 兼容旧版本的 sys_admin 角色：先保留其菜单，再移除旧角色模型。
        var adminRole = await _sysRoleRep.AsQueryable().ClearFilter().FirstAsync(u => u.TenantId == input.Id && u.Code == "sys_admin");
        if (adminRole != null)
        {
            await _sysRoleRep.Context.Deleteable<SysUserRole>().Where(u => u.RoleId == adminRole.Id).ExecuteCommandAsync();
            await App.GetService<SysRoleService>().DeleteRole(new DeleteRoleInput { Id = adminRole.Id });
        }
        await GrantMenu(new TenantMenuInput { Id = input.Id, MenuIdList = menuIdList });
    }

    /// <summary>
    /// 在非单用户登录模式下获取登录令牌
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    [NonAction]
    public async Task<LoginOutput> GetAccessTokenInNotSingleLogin(SysUser user)
    {
        // 使用非单用户模式登录
        var singleLogin = _sysCacheService.Get<bool>($"{CacheConst.KeyConfig}{ConfigConst.SysSingleLogin}");
        try
        {
            _sysCacheService.Set($"{CacheConst.KeyConfig}{ConfigConst.SysSingleLogin}", false);
            return await App.GetService<SysAuthService>().CreateToken(user);
        }
        finally
        {
            // 恢复单用户登录参数
            if (singleLogin) _sysCacheService.Set($"{CacheConst.KeyConfig}{ConfigConst.SysSingleLogin}", true);
        }
    }

    /// <summary>
    /// 缓存所有租户
    /// </summary>
    /// <param name="tenantId"></param>
    /// <returns></returns>
    [NonAction]
    public async Task CacheTenant(long tenantId = 0)
    {
        // 移除 ISqlSugarClient 中的库连接并排除默认主库
        if (tenantId > 0 && tenantId.ToString() != SqlSugarConst.MainConfigId)
            _sysTenantRep.AsTenant().RemoveConnection(tenantId);

        var tenantList = await _sysTenantRep.GetListAsync();

        // 对租户库连接进行SM2加密
        foreach (var tenant in tenantList.Where(tenant => !string.IsNullOrWhiteSpace(tenant.Connection)))
            tenant.Connection = CryptogramUtil.SM2Encrypt(tenant.Connection);

        _sysCacheService.Set(CacheConst.KeyTenant, tenantList);
    }

    /// <summary>
    /// 创建租户数据库 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "CreateDb"), HttpPost]
    [DisplayName("创建租户数据库")]
    public async Task CreateDb(TenantInput input)
    {
        var tenant = await _sysTenantRep.GetSingleAsync(u => u.Id == input.Id);
        if (tenant == null) return;

        if (tenant.DbType == SqlSugar.DbType.Oracle)
            throw Oops.Oh(ErrorCodeEnum.Z1002);

        if (string.IsNullOrWhiteSpace(tenant.Connection) || tenant.Connection.Length < 10)
            throw Oops.Oh(ErrorCodeEnum.Z1004);

        // 默认数据库配置
        var defaultConfig = App.GetOptions<DbConnectionOptions>().ConnectionConfigs.FirstOrDefault();

        var config = new DbConnectionConfig
        {
            ConfigId = tenant.Id.ToString(),
            DbType = tenant.DbType,
            ConnectionString = tenant.Connection,
            DbSettings = new DbSettings()
            {
                EnableInitDb = true,
                EnableDiffLog = false,
                EnableUnderLine = defaultConfig!.DbSettings.EnableUnderLine,
            }
        };
        SqlSugarSetup.InitTenantDatabase(App.GetRequiredService<ISqlSugarClient>().AsTenant(), config);
    }

    /// <summary>
    /// 获取租户下的用户列表 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取租户下的用户列表")]
    public async Task<List<SysUser>> UserList(TenantIdInput input)
    {
        return await _sysUserRep.AsQueryable().ClearFilter().Where(u => u.TenantId == input.TenantId).ToListAsync();
    }

    /// <summary>
    /// 获取租户数据库连接
    /// </summary>
    /// <returns></returns>
    [NonAction]
    public SqlSugarScopeProvider GetTenantDbConnectionScope(long tenantId)
    {
        var iTenant = _sysTenantRep.AsTenant();

        // 若已存在租户库连接，则直接返回
        if (iTenant.IsAnyConnection(tenantId.ToString())) return iTenant.GetConnectionScope(tenantId.ToString());

        lock (iTenant)
        {
            // 从缓存里面获取租户信息
            var tenant = _sysCacheService.Get<List<SysTenant>>(CacheConst.KeyTenant)?.FirstOrDefault(u => u.Id == tenantId);
            if (tenant == null || tenant.TenantType == TenantTypeEnum.Id) return null;

            // 获取默认库连接配置
            var dbOptions = App.GetOptions<DbConnectionOptions>();
            var mainConnConfig = dbOptions.ConnectionConfigs.First(u => u.ConfigId.ToString() == SqlSugarConst.MainConfigId);

            // 设置租户库连接配置
            var tenantConnConfig = new DbConnectionConfig
            {
                ConfigId = tenant.Id.ToString(),
                DbType = tenant.DbType,
                TenantType = tenant.TenantType,
                IsAutoCloseConnection = true,
                ConnectionString = CryptogramUtil.SM2Decrypt(tenant.Connection), // 对租户库连接进行SM2解密
                DbSettings = new DbSettings()
                {
                    EnableUnderLine = mainConnConfig.DbSettings.EnableUnderLine,
                },
                SlaveConnectionConfigs = JSON.IsValid(tenant.SlaveConnections) ? JSON.Deserialize<List<SlaveConnectionConfig>>(tenant.SlaveConnections) : null // 从库连接配置
            };
            iTenant.AddConnection(tenantConnConfig);

            var sqlSugarScopeProvider = iTenant.GetConnectionScope(tenantId.ToString());
            SqlSugarSetup.SetDbConfig(tenantConnConfig);
            SqlSugarSetup.SetDbAop(sqlSugarScopeProvider, dbOptions.EnableConsoleSql, dbOptions.SuperAdminIgnoreIDeletedFilter);

            return sqlSugarScopeProvider;
        }
    }
}
