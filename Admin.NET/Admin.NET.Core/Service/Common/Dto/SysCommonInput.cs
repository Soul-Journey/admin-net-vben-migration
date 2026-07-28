// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

/// <summary>
/// 接口压测输入参数
/// </summary>
public class StressTestInput
{
    /// <summary>
    /// 接口请求地址
    /// </summary>
    /// <example>https://gitee.com/zuohuaijun/Admin.NET</example>
    [Required(ErrorMessage = "接口请求地址不能为空")]
    [MaxLength(512, ErrorMessage = "接口请求地址不能超过512个字符")]
    public string RequestUri { get; set; }

    /// <summary>
    /// 请求方式
    /// </summary>
    [Required(ErrorMessage = "请求方式不能为空")]
    [RegularExpression("^(GET|POST)$", ErrorMessage = "仅支持GET或POST只读接口")]
    public string RequestMethod { get; set; } = nameof(HttpMethod.Get);

    /// <summary>
    /// 每轮请求量
    /// </summary>
    /// <example>100</example>
    [Required(ErrorMessage = "每轮请求量不能为空")]
    [Range(1, 500, ErrorMessage = "每轮请求量必须为1-500")]
    public int? NumberOfRequests { get; set; }

    /// <summary>
    /// 压测轮数
    /// </summary>
    /// <example>5</example>
    [Required(ErrorMessage = "压测轮数不能为空")]
    [Range(1, 10, ErrorMessage = "压测轮数必须为1-10")]
    public int? NumberOfRounds { get; set; }

    /// <summary>
    /// 最大并行量（默认为当前主机逻辑处理器的数量）
    /// </summary>
    /// <example>500</example>
    [Range(1, 50, ErrorMessage = "最大并行量必须为1-50")]
    public int? MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// 请求参数
    /// </summary>
    public List<KeyValuePair<string, string>> RequestParameters { get; set; } = new();

    /// <summary>
    /// 请求头参数
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// 路径参数
    /// </summary>
    public Dictionary<string, string> PathParameters { get; set; } = new();

    /// <summary>
    /// Query参数
    /// </summary>
    public Dictionary<string, string> QueryParameters { get; set; } = new();
}
