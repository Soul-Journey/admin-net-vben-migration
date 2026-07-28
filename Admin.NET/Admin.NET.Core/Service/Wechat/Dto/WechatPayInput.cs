// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

public class WechatPayTransactionInput
{
    /// <summary>
    /// OpenId
    /// </summary>
    public string OpenId { get; set; }

    /// <summary>
    /// 订单金额
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Total { get; set; }

    /// <summary>
    /// 商品描述
    /// </summary>
    [Required]
    [MaxLength(127)]
    public string Description { get; set; }

    /// <summary>
    /// 附加数据
    /// </summary>
    [MaxLength(127)]
    public string Attachment { get; set; }

    /// <summary>
    /// 优惠标记
    /// </summary>
    [MaxLength(32)]
    public string GoodsTag { get; set; }

    /// <summary>
    /// 业务标签，用来区分做什么业务
    /// </summary>
    [MaxLength(64)]
    public string Tags { get; set; }

    /// <summary>
    /// 对应业务的主键
    /// </summary>
    public long BusinessId { get; set; }
}

public class WechatPayParaInput
{
    /// <summary>
    /// 订单Id
    /// </summary>
    public string PrepayId { get; set; }
}

public class WechatPayRefundDomesticInput
{
    /// <summary>
    /// 商户端生成的业务流水号
    /// </summary>
    [Required]
    public string TradeId { get; set; }

    /// <summary>
    /// 退款原因
    /// </summary>
    [Required]
    [MaxLength(80)]
    public string Reason { get; set; }

    /// <summary>
    /// 退款金额
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int Refund { get; set; }

    /// <summary>
    /// 原订单金额
    /// </summary>
    [Required]
    public int Total { get; set; }
}

public class WechatPayPageInput : BasePageInput
{
    /// <summary>
    /// 添加时间范围
    /// </summary>
    public List<DateTime?> CreateTimeRange { get; set; }
}
