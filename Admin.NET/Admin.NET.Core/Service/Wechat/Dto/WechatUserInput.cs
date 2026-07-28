// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

public class WechatUserInput : BasePageInput
{
    /// <summary>
    /// 昵称
    /// </summary>
    public string NickName { get; set; }

    /// <summary>
    /// 手机号码
    /// </summary>
    public string Mobile { get; set; }
}

public class DeleteWechatUserInput : BaseIdInput
{
}

/// <summary>
/// 微信账号安全输出（不向管理端返回会话密钥和访问令牌）
/// </summary>
public class WechatUserOutput
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public PlatformTypeEnum PlatformType { get; set; }
    public string OpenId { get; set; }
    public string? UnionId { get; set; }
    public string? NickName { get; set; }
    public string? Avatar { get; set; }
    public string? Mobile { get; set; }
    public int? Sex { get; set; }
    public string? Language { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? Country { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime? UpdateTime { get; set; }
    public string? CreateUserName { get; set; }
    public string? UpdateUserName { get; set; }
}

/// <summary>
/// 微信账号管理端保存参数（令牌字段只允许由 OAuth/微信服务维护）
/// </summary>
public class SaveWechatUserInput : BaseIdInput
{
    [Required]
    public PlatformTypeEnum PlatformType { get; set; }

    [Required, MaxLength(64)]
    public string OpenId { get; set; }

    [MaxLength(64)]
    public string? UnionId { get; set; }

    [MaxLength(64)]
    public string? NickName { get; set; }

    [MaxLength(256)]
    public string? Avatar { get; set; }

    [MaxLength(16)]
    public string? Mobile { get; set; }

    public int? Sex { get; set; }

    [MaxLength(64)]
    public string? Language { get; set; }

    [MaxLength(64)]
    public string? City { get; set; }

    [MaxLength(64)]
    public string? Province { get; set; }

    [MaxLength(64)]
    public string? Country { get; set; }
}
