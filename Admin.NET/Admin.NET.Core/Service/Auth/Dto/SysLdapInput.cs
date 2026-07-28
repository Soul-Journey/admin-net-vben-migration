// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

public class SysLdapInput : BasePageInput
{
    public string? Host { get; set; }
    public long TenantId { get; set; }
}

public class SaveSysLdapInput
{
    public long TenantId { get; set; }

    [Required, MaxLength(128)]
    public string Host { get; set; }

    [Range(1, 65535)]
    public int Port { get; set; } = 389;

    [Required, MaxLength(128)]
    public string BaseDn { get; set; }

    [Required, MaxLength(128)]
    public string BindDn { get; set; }

    [Required, MaxLength(128)]
    public string AuthFilter { get; set; } = "sAMAccountName=%s";

    [Range(2, 3)]
    public int Version { get; set; } = 3;

    [Required, MaxLength(32)]
    public string BindAttrAccount { get; set; } = "sAMAccountName";

    [Required, MaxLength(32)]
    public string BindAttrEmployeeId { get; set; } = "EmployeeId";

    [Required, MaxLength(64)]
    public string BindAttrCode { get; set; } = "objectGUID";

    public StatusEnum Status { get; set; } = StatusEnum.Enable;
}

public class AddSysLdapInput : SaveSysLdapInput
{
    [Required, MaxLength(512)]
    public string BindPass { get; set; }
}

public class UpdateSysLdapInput : SaveSysLdapInput
{
    [Required]
    public long Id { get; set; }

    [MaxLength(512)]
    public string? BindPass { get; set; }
}

public class SysLdapOutput
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
    public string BaseDn { get; set; }
    public string BindDn { get; set; }
    public bool HasBindPass { get; set; }
    public string AuthFilter { get; set; }
    public int Version { get; set; }
    public string BindAttrAccount { get; set; }
    public string BindAttrEmployeeId { get; set; }
    public string BindAttrCode { get; set; }
    public StatusEnum Status { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime? UpdateTime { get; set; }
    public string? CreateUserName { get; set; }
    public string? UpdateUserName { get; set; }
}

public class DeleteSysLdapInput : BaseIdInput
{
}

public class DetailSysLdapInput : BaseIdInput
{
}

public class SyncSysLdapInput : BaseIdInput
{
}

public class SyncLdapResult
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Total { get; set; }
}
