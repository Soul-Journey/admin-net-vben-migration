// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

public class BackupOutput
{
    /// <summary>
    /// 文件路径
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public string FilePath { get; set; }

    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 系统更新配置状态（不包含令牌和部署目录等敏感值）
/// </summary>
public class UpdateConfigurationStatusOutput
{
    public bool Enabled { get; set; }
    public bool AccessTokenConfigured { get; set; }
    public bool BackendOutputConfigured { get; set; }
    public bool BackendOutputExists { get; set; }
    public bool PublishConfigured { get; set; }
    public bool ReadyForUpdate { get; set; }
    public bool ReadyForRestore { get; set; }
    public string Repository { get; set; }
    public string Branch { get; set; }
    public string TargetFramework { get; set; }
    public string RuntimeIdentifier { get; set; }
    public int UpdateInterval { get; set; }
    public int BackupCount { get; set; }
}
