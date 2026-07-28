// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

public class PageRegionInput : BasePageInput
{
    /// <summary>
    /// 父节点Id
    /// </summary>
    public long Pid { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 编码
    /// </summary>
    public string Code { get; set; }
}

public class RegionInput : BaseIdInput
{
}

public class AddRegionInput
{
    /// <summary>
    /// 父节点Id，0 表示根节点
    /// </summary>
    public long Pid { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    [Required(ErrorMessage = "名称不能为空")]
    [MaxLength(128)]
    public string Name { get; set; }

    /// <summary>
    /// 行政代码
    /// </summary>
    [Required(ErrorMessage = "行政代码不能为空")]
    [MaxLength(32)]
    public string Code { get; set; }

    /// <summary>
    /// 区号
    /// </summary>
    [MaxLength(6)]
    public string? CityCode { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    public int OrderNo { get; set; } = 100;

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(128)]
    public string? Remark { get; set; }
}

public class UpdateRegionInput : AddRegionInput
{
    /// <summary>
    /// 区域Id
    /// </summary>
    [Required]
    public long Id { get; set; }
}

public class DeleteRegionInput : BaseIdInput
{
}

public class RegionSyncOutput
{
    /// <summary>
    /// 数据版本
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// 数据来源
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// 省级数量
    /// </summary>
    public int ProvinceCount { get; set; }

    /// <summary>
    /// 市级数量
    /// </summary>
    public int CityCount { get; set; }

    /// <summary>
    /// 区县级数量
    /// </summary>
    public int CountyCount { get; set; }

    /// <summary>
    /// 总数量
    /// </summary>
    public int Total { get; set; }
}
