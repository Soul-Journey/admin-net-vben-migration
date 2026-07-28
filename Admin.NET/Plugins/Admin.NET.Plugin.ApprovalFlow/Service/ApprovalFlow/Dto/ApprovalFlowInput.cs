// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Plugin.ApprovalFlow.Service;

/// <summary>
/// 审批流分页查询输入参数
/// </summary>
public class ApprovalFlowInput : BasePageInput
{
    /// <summary>
    /// 编号
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 审批流增加输入参数
/// </summary>
public class AddApprovalFlowInput
{
    /// <summary>
    /// 编号，为空时由系统生成
    /// </summary>
    [MaxLength(32)]
    public string? Code { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    [Required(ErrorMessage = "名称不能为空")]
    [MaxLength(32)]
    public string Name { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    [Required(ErrorMessage = "状态不能为空")]
    [Range(1, 2, ErrorMessage = "状态值无效")]
    public int? Status { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(256)]
    public string? Remark { get; set; }
}

/// <summary>
/// 审批流更新输入参数
/// </summary>
public class UpdateApprovalFlowInput : AddApprovalFlowInput
{
    /// <summary>
    /// 主键Id
    /// </summary>
    [Required(ErrorMessage = "主键Id不能为空")]
    public long Id { get; set; }

    /// <summary>
    /// 旧版兼容：业务表绑定配置
    /// </summary>
    public string? FormJson { get; set; }

    /// <summary>
    /// 旧版兼容：流程设计配置
    /// </summary>
    public string? FlowJson { get; set; }
}

/// <summary>
/// 单独保存审批流 JSON 配置
/// </summary>
public class UpdateApprovalFlowJsonInput
{
    /// <summary>
    /// 主键Id
    /// </summary>
    [Required(ErrorMessage = "主键Id不能为空")]
    public long Id { get; set; }

    /// <summary>
    /// JSON 配置
    /// </summary>
    [Required(ErrorMessage = "配置不能为空")]
    public string Json { get; set; }
}
