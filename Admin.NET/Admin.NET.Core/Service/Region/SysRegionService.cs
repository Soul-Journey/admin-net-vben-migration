// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.Net;

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统行政区域服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 310)]
public class SysRegionService : IDynamicApiController, ITransient
{
    private const string OfficialRegionSource = "https://www.mca.gov.cn/mzsj/xzqh/2025/202401xzqh.html";
    private readonly SqlSugarRepository<SysRegion> _sysRegionRep;
    private readonly SysConfigService _sysConfigService;
    private readonly UserManager _userManager;

    public SysRegionService(SqlSugarRepository<SysRegion> sysRegionRep, SysConfigService sysConfigService, UserManager userManager)
    {
        _sysRegionRep = sysRegionRep;
        _sysConfigService = sysConfigService;
        _userManager = userManager;
    }

    /// <summary>
    /// 获取行政区域分页列表 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取行政区域分页列表")]
    public async Task<SqlSugarPagedList<SysRegion>> Page(PageRegionInput input)
    {
        return await _sysRegionRep.AsQueryable()
            .WhereIF(input.Pid > 0, u => u.Pid == input.Pid || u.Id == input.Pid)
            .WhereIF(!string.IsNullOrWhiteSpace(input.Name), u => u.Name.Contains(input.Name))
            .WhereIF(!string.IsNullOrWhiteSpace(input.Code), u => u.Code.Contains(input.Code))
            .OrderBy(u => u.Level).OrderBy(u => u.OrderNo).OrderBy(u => u.Code)
            .ToPagedListAsync(input.Page, input.PageSize);
    }

    /// <summary>
    /// 获取行政区域列表 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取行政区域列表")]
    public async Task<List<SysRegion>> GetList([FromQuery] RegionInput input)
    {
        return await _sysRegionRep.AsQueryable()
            .Where(u => u.Pid == input.Id)
            .OrderBy(u => u.OrderNo)
            .OrderBy(u => u.Code)
            .ToListAsync();
    }

    /// <summary>
    /// 增加行政区域 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Add"), HttpPost]
    [DisplayName("增加行政区域")]
    public async Task<long> AddRegion(AddRegionInput input)
    {
        input.Name = input.Name.Trim();
        input.Code = input.Code?.Trim() ?? "";
        if (input.Code.Length != 12 && input.Code.Length != 9 && input.Code.Length != 6) throw Oops.Oh(ErrorCodeEnum.R2003);

        var parent = await ResolveParent(input.Pid);
        input.Pid = parent?.Id ?? 0;

        var isExist = await _sysRegionRep.IsAnyAsync(u => u.Code == input.Code);
        if (isExist) throw Oops.Oh(ErrorCodeEnum.R2002);

        var sysRegion = input.Adapt<SysRegion>();
        sysRegion.Level = (parent?.Level ?? 0) + 1;
        var newRegion = await _sysRegionRep.AsInsertable(sysRegion).ExecuteReturnEntityAsync();
        return newRegion.Id;
    }

    /// <summary>
    /// 更新行政区域 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    [DisplayName("更新行政区域")]
    public async Task UpdateRegion(UpdateRegionInput input)
    {
        input.Name = input.Name.Trim();
        input.Code = input.Code?.Trim() ?? "";
        if (input.Code.Length != 12 && input.Code.Length != 9 && input.Code.Length != 6) throw Oops.Oh(ErrorCodeEnum.R2003);

        var sysRegion = await _sysRegionRep.GetFirstAsync(u => u.Id == input.Id);
        if (sysRegion == null) throw Oops.Oh(ErrorCodeEnum.D1002);

        var parent = await ResolveParent(input.Pid);
        input.Pid = parent?.Id ?? 0;
        if (sysRegion.Pid != input.Pid)
        {
            var regionTreeList = await _sysRegionRep.AsQueryable().ToChildListAsync(u => u.Pid, input.Id, true);
            var childIdList = regionTreeList.Select(u => u.Id).ToList();
            if (childIdList.Contains(input.Pid)) throw Oops.Oh(ErrorCodeEnum.R2004);
        }

        if (input.Id == input.Pid) throw Oops.Oh(ErrorCodeEnum.R2001);

        var isExist = await _sysRegionRep.IsAnyAsync(u => u.Code == input.Code && u.Id != sysRegion.Id);
        if (isExist) throw Oops.Oh(ErrorCodeEnum.R2002);

        //// 父Id不能为自己的子节点
        //var regionTreeList = await _sysRegionRep.AsQueryable().ToChildListAsync(u => u.Pid, input.Id, true);
        //var childIdList = regionTreeList.Select(u => u.Id).ToList();
        //if (childIdList.Contains(input.Pid))
        //    throw Oops.Oh(ErrorCodeEnum.R2001);

        var newLevel = (parent?.Level ?? 0) + 1;
        var levelOffset = newLevel - sysRegion.Level;
        var updatedRegion = input.Adapt<SysRegion>();
        updatedRegion.Level = newLevel;

        await _sysRegionRep.Context.Ado.BeginTranAsync();
        try
        {
            await _sysRegionRep.AsUpdateable(updatedRegion).IgnoreColumns(true).ExecuteCommandAsync();
            if (levelOffset != 0)
            {
                var descendants = await _sysRegionRep.AsQueryable().ToChildListAsync(u => u.Pid, input.Id, false);
                foreach (var descendant in descendants) descendant.Level += levelOffset;
                if (descendants.Count > 0)
                    await _sysRegionRep.Context.Updateable(descendants).UpdateColumns(u => u.Level).ExecuteCommandAsync();
            }
            await _sysRegionRep.Context.Ado.CommitTranAsync();
        }
        catch
        {
            await _sysRegionRep.Context.Ado.RollbackTranAsync();
            throw;
        }
    }

    /// <summary>
    /// 删除行政区域 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Delete"), HttpPost]
    [DisplayName("删除行政区域")]
    public async Task<int> DeleteRegion(DeleteRegionInput input)
    {
        var regionTreeList = await _sysRegionRep.AsQueryable().ToChildListAsync(u => u.Pid, input.Id, true);
        if (regionTreeList.Count == 0) throw Oops.Oh(ErrorCodeEnum.D1002);
        var regionIdList = regionTreeList.Select(u => u.Id).ToList();
        await _sysRegionRep.DeleteAsync(u => regionIdList.Contains(u.Id));
        return regionIdList.Count;
    }

    /// <summary>
    /// 同步行政区域 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("同步行政区域")]
    public async Task<RegionSyncOutput> Sync()
    {
        if (!_userManager.SuperAdmin) throw Oops.Oh("仅超级管理员可同步全国行政区域");

        var syncLevel = await _sysConfigService.GetConfigValue<int>(ConfigConst.SysRegionSyncLevel);
        if (syncLevel is < 1 or > 5) syncLevel = 3;//默认区县级
        syncLevel = Math.Min(syncLevel, 3);//当前官方公开数据为县级以上

        var regions = await GetRegionsFromOfficialList(syncLevel);
        if (regions.Count == 0 || regions.Any(u => string.IsNullOrWhiteSpace(u.Code)))
            throw Oops.Oh("外部行政区域数据不完整，已取消同步，原数据未变更");
        if (regions.GroupBy(u => u.Code).Any(g => g.Count() > 1))
            throw Oops.Oh("外部行政区域存在重复编码，已取消同步，原数据未变更");

        var regionIds = regions.Select(u => u.Id).ToHashSet();
        if (regions.Any(u => u.Pid != 0 && !regionIds.Contains(u.Pid)))
            throw Oops.Oh("外部行政区域父子关系不完整，已取消同步，原数据未变更");

        await _sysRegionRep.Context.Ado.BeginTranAsync();
        try
        {
            await _sysRegionRep.Context.Deleteable<SysRegion>().ExecuteCommandAsync();
            foreach (var batch in regions.Chunk(500))
                await _sysRegionRep.Context.Insertable(batch.ToList()).ExecuteCommandAsync();
            await _sysRegionRep.Context.Ado.CommitTranAsync();
        }
        catch
        {
            await _sysRegionRep.Context.Ado.RollbackTranAsync();
            throw;
        }

        return new RegionSyncOutput
        {
            Version = "2024",
            Source = "中华人民共和国民政部：县以上行政区划代码",
            ProvinceCount = regions.Count(u => u.Level == 1),
            CityCount = regions.Count(u => u.Level == 2),
            CountyCount = regions.Count(u => u.Level == 3),
            Total = regions.Count,
        };

        // var context = BrowsingContext.New(AngleSharp.Configuration.Default.WithDefaultLoader());
        // var dom = await context.OpenAsync(_url);
        //
        // // 省级列表
        // var itemList = dom.QuerySelectorAll("table.provincetable tr.provincetr td a");
        // if (itemList.Length == 0) throw Oops.Oh(ErrorCodeEnum.R2005);
        //
        // await _sysRegionRep.DeleteAsync(u => u.Id > 0);
        //
        // foreach (var element in itemList)
        // {
        //     var item = (IHtmlAnchorElement)element;
        //     var list = new List<SysRegion>();
        //
        //     var region = new SysRegion
        //     {
        //         Id = YitIdHelper.NextId(),
        //         Pid = 0,
        //         Name = item.TextContent,
        //         Remark = item.Href,
        //         Level = 1,
        //     };
        //     list.Add(region);
        //
        //     // 市级
        //     if (!string.IsNullOrEmpty(item.Href))
        //     {
        //         var dom1 = await context.OpenAsync(item.Href);
        //         var itemList1 = dom1.QuerySelectorAll("table.citytable tr.citytr td a");
        //         for (var i1 = 0; i1 < itemList1.Length; i1 += 2)
        //         {
        //             var item1 = (IHtmlAnchorElement)itemList1[i1 + 1];
        //             var region1 = new SysRegion
        //             {
        //                 Id = YitIdHelper.NextId(),
        //                 Pid = region.Id,
        //                 Name = item1.TextContent,
        //                 Code = itemList1[i1].TextContent,
        //                 Remark = item1.Href,
        //                 Level = 2,
        //             };
        //
        //             // 若URL中查询的一级行政区域缺少Code则通过二级区域填充
        //             if (list.Count == 1 && !string.IsNullOrEmpty(region1.Code))
        //                 region.Code = region1.Code.Substring(0, 2).PadRight(region1.Code.Length, '0');
        //
        //             // 同步层级为“1-省级”退出
        //             if (syncLevel < 2) break;
        //
        //             list.Add(region1);
        //
        //             // 区县级
        //             if (string.IsNullOrEmpty(item1.Href) || syncLevel <= 2) continue;
        //
        //             var dom2 = await context.OpenAsync(item1.Href);
        //             var itemList2 = dom2.QuerySelectorAll("table.countytable tr.countytr td a");
        //             for (var i2 = 0; i2 < itemList2.Length; i2 += 2)
        //             {
        //                 var item2 = (IHtmlAnchorElement)itemList2[i2 + 1];
        //                 var region2 = new SysRegion
        //                 {
        //                     Id = YitIdHelper.NextId(),
        //                     Pid = region1.Id,
        //                     Name = item2.TextContent,
        //                     Code = itemList2[i2].TextContent,
        //                     Remark = item2.Href,
        //                     Level = 3,
        //                 };
        //                 list.Add(region2);
        //
        //                 // 街道级
        //                 if (string.IsNullOrEmpty(item2.Href) || syncLevel <= 3) continue;
        //
        //                 var dom3 = await context.OpenAsync(item2.Href);
        //                 var itemList3 = dom3.QuerySelectorAll("table.towntable tr.towntr td a");
        //                 for (var i3 = 0; i3 < itemList3.Length; i3 += 2)
        //                 {
        //                     var item3 = (IHtmlAnchorElement)itemList3[i3 + 1];
        //                     var region3 = new SysRegion
        //                     {
        //                         Id = YitIdHelper.NextId(),
        //                         Pid = region2.Id,
        //                         Name = item3.TextContent,
        //                         Code = itemList3[i3].TextContent,
        //                         Remark = item3.Href,
        //                         Level = 4,
        //                     };
        //                     list.Add(region3);
        //
        //                     // 村级
        //                     if (string.IsNullOrEmpty(item3.Href) || syncLevel <= 4) continue;
        //
        //                     var dom4 = await context.OpenAsync(item3.Href);
        //                     var itemList4 = dom4.QuerySelectorAll("table.villagetable tr.villagetr td");
        //                     for (var i4 = 0; i4 < itemList4.Length; i4 += 3)
        //                     {
        //                         list.Add(new SysRegion
        //                         {
        //                             Id = YitIdHelper.NextId(),
        //                             Pid = region3.Id,
        //                             Name = itemList4[i4 + 2].TextContent,
        //                             Code = itemList4[i4].TextContent,
        //                             CityCode = itemList4[i4 + 1].TextContent,
        //                             Level = 5,
        //                         });
        //                     }
        //                 }
        //             }
        //         }
        //     }
        //
        //     //按省份同步快速写入提升同步效率，全部一次性写入容易出现从统计局获取数据失败
        //     await _sysRegionRep.Context.Fastest<SysRegion>().BulkCopyAsync(list);
        // }
    }

    /// <summary>
    /// 从民政部公开的县以上行政区划代码页面同步
    /// </summary>
    /// <param name="syncLevel"></param>
    private async Task<List<SysRegion>> GetRegionsFromOfficialList(int syncLevel)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Admin.NET Region Sync/1.0");
        var html = await client.GetStringAsync(OfficialRegionSource);
        var rowMatches = Regex.Matches(html, @"<tr\b[^>]*>(?<row>[\s\S]*?)</tr>", RegexOptions.IgnoreCase);
        var regions = new List<SysRegion>();
        SysRegion? currentProvince = null;
        SysRegion? currentCity = null;
        var orderByLevel = new Dictionary<int, int>();

        foreach (Match rowMatch in rowMatches)
        {
            var row = rowMatch.Groups["row"].Value;
            var dataMatch = Regex.Match(
                row,
                @"<td\b[^>]*>\s*(?<code>\d{6})\s*</td>\s*<td\b[^>]*>(?<name>[\s\S]*?)</td>",
                RegexOptions.IgnoreCase);
            if (!dataMatch.Success) continue;

            var code = dataMatch.Groups["code"].Value;
            var decodedName = WebUtility.HtmlDecode(
                Regex.Replace(dataMatch.Groups["name"].Value, @"<[^>]+>", ""));
            var leadingSpaces = decodedName.TakeWhile(char.IsWhiteSpace).Count();
            var name = decodedName.Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var level = leadingSpaces switch
            {
                >= 2 => 3,
                1 => 2,
                _ => 1,
            };

            if (level == 1)
            {
                currentProvince = new SysRegion
                {
                    Id = YitIdHelper.NextId(),
                    Pid = 0,
                    Name = name,
                    Code = code,
                    Level = 1,
                    OrderNo = NextOrder(1),
                };
                currentCity = null;
                regions.Add(currentProvince);
                continue;
            }

            if (currentProvince == null)
                throw Oops.Oh("官方行政区域数据缺少省级父节点，原数据未变更");

            if (level == 2)
            {
                currentCity = new SysRegion
                {
                    Id = YitIdHelper.NextId(),
                    Pid = currentProvince.Id,
                    Name = name,
                    Code = code,
                    Level = 2,
                    OrderNo = NextOrder(2),
                };
                if (syncLevel >= 2) regions.Add(currentCity);
                continue;
            }

            if (syncLevel >= 3)
            {
                regions.Add(new SysRegion
                {
                    Id = YitIdHelper.NextId(),
                    Pid = currentCity?.Id ?? currentProvince.Id,
                    Name = name,
                    Code = code,
                    Level = 3,
                    OrderNo = NextOrder(3),
                });
            }
        }

        if (regions.Count(u => u.Level == 1) < 30)
            throw Oops.Oh("官方行政区域省级数据数量异常，原数据未变更");

        return regions;

        int NextOrder(int level)
        {
            orderByLevel[level] = orderByLevel.GetValueOrDefault(level) + 1;
            return orderByLevel[level] * 10;
        }
    }

    private async Task<SysRegion?> ResolveParent(long pid)
    {
        if (pid == 0) return null;

        var parent = await _sysRegionRep.GetFirstAsync(u => u.Id == pid);
        parent ??= await _sysRegionRep.GetFirstAsync(u => u.Code == pid.ToString());
        return parent ?? throw Oops.Oh(ErrorCodeEnum.D2000);
    }
}
