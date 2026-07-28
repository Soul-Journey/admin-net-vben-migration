// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

/// <summary>
/// 接口压测输出参数
/// </summary>
public class StressTestOutput
{
    /// <summary>
    /// 总请求次数
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// 总用时（秒）
    /// </summary>
    public double TotalTimeInSeconds { get; set; }

    /// <summary>
    /// 成功请求次数
    /// </summary>
    public long SuccessfulRequests { get; set; }

    /// <summary>
    /// 失败请求次数
    /// </summary>
    public long FailedRequests { get; set; }

    /// <summary>
    /// 每秒查询率（QPS）
    /// </summary>
    public double QueriesPerSecond { get; set; }

    /// <summary>
    /// 最小响应时间（毫秒）
    /// </summary>
    public double MinResponseTime { get; set; }

    /// <summary>
    /// 最大响应时间（毫秒）
    /// </summary>
    public double MaxResponseTime { get; set; }

    /// <summary>
    /// 平均响应时间（毫秒）
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// P10 响应时间（毫秒）
    /// </summary>
    public double Percentile10ResponseTime { get; set; }

    /// <summary>
    /// P25 响应时间（毫秒）
    /// </summary>
    public double Percentile25ResponseTime { get; set; }

    /// <summary>
    /// P50 响应时间（毫秒）
    /// </summary>
    public double Percentile50ResponseTime { get; set; }

    /// <summary>
    /// P75 响应时间（毫秒）
    /// </summary>
    public double Percentile75ResponseTime { get; set; }

    /// <summary>
    /// P90 响应时间（毫秒）
    /// </summary>
    public double Percentile90ResponseTime { get; set; }

    /// <summary>
    /// P99 响应时间（毫秒）
    /// </summary>
    public double Percentile99ResponseTime { get; set; }

    /// <summary>
    /// P999 响应时间（毫秒）
    /// </summary>
    public double Percentile999ResponseTime { get; set; }

    /// <summary>
    /// 是否达到服务端最长执行时间
    /// </summary>
    public bool TimedOut { get; set; }
}

/// <summary>
/// 允许执行压力测试的接口
/// </summary>
public class StressTestEndpointOutput
{
    public string GroupName { get; set; }
    public string DisplayName { get; set; }
    public string Route { get; set; }
    public string Method { get; set; }
}
