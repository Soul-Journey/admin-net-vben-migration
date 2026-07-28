// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Azure.Core;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities.Encoders;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统通用服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 101)]
public class SysCommonService : IDynamicApiController, ITransient
{
    private const int MaxStressTestRequests = 2_000;
    private static readonly SemaphoreSlim StressTestLock = new(1, 1);
    private static readonly string[] StressTestDeniedTerms =
    {
        "auth", "login", "logout", "signin", "signout", "register", "password", "secret", "token", "key",
        "pay", "refund", "wechat", "update", "restore", "delete", "remove", "clear", "create", "add", "insert",
        "save", "set", "reset", "sync", "upload", "download", "export", "import", "execute", "compile", "publish",
        "start", "stop", "pause", "cancel", "send", "release", "revoke", "unlock", "force", "webhook", "plugin",
        "stress", "file", "attachment", "image", "excel", "apijson", "wechat", "dingtalk", "syswx", "wxopen",
        "openaccess", "server", "database", "codegen",
        "登录", "退出", "注册", "密码", "密钥", "令牌", "支付", "退款", "更新", "还原", "删除", "清空", "新增",
        "创建", "保存", "设置", "重置", "同步", "上传", "下载", "导出", "导入", "执行", "编译", "发布", "启动",
        "停止", "暂停", "取消", "发送", "撤回", "解锁", "强制"
    };
    private static readonly string[] StressTestAllowedPrefixes =
    {
        "get", "query", "page", "list", "detail", "find", "select", "tree", "check", "preview", "search",
        "statistics", "status"
    };
    private readonly IApiDescriptionGroupCollectionProvider _apiProvider;
    private readonly SqlSugarRepository<SysUser> _sysUserRep;
    private readonly CDConfigOptions _cdConfigOptions;
    private readonly UserManager _userManager;
    private readonly HttpClient _httpClient;

    public SysCommonService(IApiDescriptionGroupCollectionProvider apiProvider,
        SqlSugarRepository<SysUser> sysUserRep,
        IOptions<CDConfigOptions> giteeOptions,
        IHttpClientFactory httpClientFactory,
        UserManager userManager)
    {
        _sysUserRep = sysUserRep;
        _apiProvider = apiProvider;
        _userManager = userManager;
        _cdConfigOptions = giteeOptions.Value;
        _httpClient = httpClientFactory.CreateClient();
    }

    /// <summary>
    /// 获取国密公钥私钥对 🏆
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取国密公钥私钥对")]
    [AllowAnonymous]
    public SmKeyPairOutput GetSmKeyPair()
    {
        var kp = GM.GenerateKeyPair();
        var privateKey = Hex.ToHexString(((ECPrivateKeyParameters)kp.Private).D.ToByteArray()).ToUpper();
        var publicKey = Hex.ToHexString(((ECPublicKeyParameters)kp.Public).Q.GetEncoded()).ToUpper();

        return new SmKeyPairOutput
        {
            PrivateKey = privateKey,
            PublicKey = publicKey,
        };
    }

    /// <summary>
    /// 获取所有接口/动态API 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取所有接口/动态API")]
    public List<ApiOutput> GetApiList()
    {
        var apiList = new List<ApiOutput>();
        foreach (var item in _apiProvider.ApiDescriptionGroups.Items)
        {
            foreach (var apiDescription in item.Items)
            {
                var displayName = apiDescription.TryGetMethodInfo(out MethodInfo apiMethodInfo) ? apiMethodInfo.GetCustomAttribute<DisplayNameAttribute>(true)?.DisplayName : "";

                apiList.Add(new ApiOutput
                {
                    GroupName = item.GroupName,
                    DisplayName = displayName,
                    RouteName = apiDescription.RelativePath
                });
            }
        }
        return apiList;
    }

    /// <summary>
    /// 下载标记错误的临时Excel（全局）
    /// </summary>
    /// <returns></returns>
    [DisplayName("下载标记错误的临时Excel（全局）")]
    public async Task<IActionResult> DownloadErrorExcelTemp([FromQuery] string fileName = null)
    {
        var userId = App.User?.FindFirst(ClaimConst.UserId)?.Value;
        var resultStream = App.GetRequiredService<SysCacheService>().Get<MemoryStream>(CacheConst.KeyExcelTemp + userId);

        if (resultStream == null) throw Oops.Oh("错误标记文件已过期。");

        return await Task.FromResult(new FileStreamResult(resultStream, "application/octet-stream")
        {
            FileDownloadName = $"{(string.IsNullOrEmpty(fileName) ? "错误标记＿" + DateTime.Now.ToString("yyyyMMddhhmmss") : fileName)}.xlsx"
        });
    }

    /// <summary>
    /// 加密字符串 🔖
    /// </summary>
    /// <returns></returns>
    [SuppressMonitor]
    [DisplayName("加密字符串")]
    [AllowAnonymous]
    public dynamic EncryptPlainText([Required] string plainText)
    {
        return CryptogramUtil.Encrypt(plainText);
    }

    /// <summary>
    /// 接口压测 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("接口压测")]
    public async Task<StressTestOutput> StressTest(StressTestInput input)
    {
        if (!_userManager.SuperAdmin) throw Oops.Oh(ErrorCodeEnum.SA001);
        if (!await StressTestLock.WaitAsync(0)) throw Oops.Oh("已有接口压测任务正在执行，请稍后再试");

        try
        {
            return await ExecuteStressTest(input);
        }
        finally
        {
            StressTestLock.Release();
        }
    }

    /// <summary>
    /// 获取允许压测的只读接口
    /// </summary>
    [DisplayName("获取允许压测的只读接口")]
    public List<StressTestEndpointOutput> GetStressTestEndpoints()
    {
        if (!_userManager.SuperAdmin) throw Oops.Oh(ErrorCodeEnum.SA001);

        return GetSafeStressTestEndpoints()
            .Select(item => new StressTestEndpointOutput
            {
                GroupName = item.GroupName,
                DisplayName = item.DisplayName,
                Route = item.Route,
                Method = item.Method
            })
            .OrderBy(item => item.GroupName)
            .ThenBy(item => item.DisplayName)
            .ThenBy(item => item.Route)
            .ToList();
    }

    private async Task<StressTestOutput> ExecuteStressTest(StressTestInput input)
    {
        input.RequestMethod = input.RequestMethod?.Trim().ToUpperInvariant();
        var allowedEndpoint = ResolveStressTestEndpoint(input.RequestUri, input.RequestMethod);
        var numberOfRounds = input.NumberOfRounds ?? 0;
        var numberOfRequests = input.NumberOfRequests ?? 0;
        var maxDegreeOfParallelism = input.MaxDegreeOfParallelism ?? Environment.ProcessorCount;
        var requestedTotal = checked(numberOfRounds * numberOfRequests);
        if (requestedTotal <= 0 || requestedTotal > MaxStressTestRequests)
            throw Oops.Oh($"单次压测总请求数必须为1-{MaxStressTestRequests}");
        if (maxDegreeOfParallelism is < 1 or > 50) throw Oops.Oh("最大并发量必须为1-50");

        ValidateStressTestParameters(input);

        var stopwatch = new Stopwatch();
        var responseTimes = new ConcurrentBag<double>();
        long totalRequests = 0, successfulRequests = 0, failedRequests = 0;

        stopwatch.Start();
        using var executionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var request = App.HttpContext.Request;
        var baseUriBuilder = new UriBuilder(request.Scheme, request.Host.Host, request.Host.Port ?? -1)
        {
            Path = allowedEndpoint.Route
        };
        var queryString = HttpUtility.ParseQueryString(baseUriBuilder.Query);

        foreach (var param in input.PathParameters ?? new())
        {
            baseUriBuilder.Path = baseUriBuilder.Path.Replace($"{{{param.Key}}}", Uri.EscapeDataString(param.Value), StringComparison.OrdinalIgnoreCase);
        }
        if (baseUriBuilder.Path.Contains('{') || baseUriBuilder.Path.Contains('}')) throw Oops.Oh("请填写完整的路径参数");

        foreach (var param in input.QueryParameters ?? new())
        {
            queryString[param.Key] = param.Value;
        }

        baseUriBuilder.Query = queryString.ToString() ?? string.Empty;
        var fullUri = baseUriBuilder.Uri;
        HttpRequestMessage requestTemplate = CreateRequestMessage(input, fullUri);
        if (App.HttpContext.Request.Headers.TryGetValue("Authorization", out var authorization))
            requestTemplate.Headers.TryAddWithoutValidation("Authorization", authorization.ToString());

        var timedOut = false;
        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, requestedTotal),
                new ParallelOptions
                {
                    CancellationToken = executionTimeout.Token,
                    MaxDegreeOfParallelism = maxDegreeOfParallelism
                },
                async (_, cancellationToken) =>
                {
                var requestStopwatch = new Stopwatch();
                requestStopwatch.Start();

                try
                {
                    using var requestMessage = requestTemplate.DeepCopy();
                    if (input.RequestMethod == "POST" && (input.RequestParameters?.Count ?? 0) > 0)
                    {
                        requestMessage.Content = new FormUrlEncodedContent(input.RequestParameters);
                    }

                    using var response = await _httpClient.SendAsync(
                        requestMessage,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);
                    response.EnsureSuccessStatusCode();
                    Interlocked.Increment(ref successfulRequests);
                }
                catch (OperationCanceledException) when (executionTimeout.IsCancellationRequested)
                {
                    Interlocked.Increment(ref failedRequests);
                }
                catch
                {
                    Interlocked.Increment(ref failedRequests);
                }
                finally
                {
                    requestStopwatch.Stop();
                    responseTimes.Add(requestStopwatch.Elapsed.TotalMilliseconds);
                    Interlocked.Increment(ref totalRequests);
                }
            });
        }
        catch (OperationCanceledException) when (executionTimeout.IsCancellationRequested)
        {
            timedOut = true;
        }

        stopwatch.Stop();

        var totalTimeInSeconds = stopwatch.Elapsed.TotalSeconds;
        var qps = totalTimeInSeconds > 0 ? totalRequests / totalTimeInSeconds : 0;
        var orderResponseTimes = responseTimes.OrderBy(t => t).ToList();
        var averageResponseTime = orderResponseTimes.Count > 0 ? orderResponseTimes.Average() : 0;
        var minResponseTime = orderResponseTimes.Count > 0 ? orderResponseTimes.Min() : 0;
        var maxResponseTime = orderResponseTimes.Count > 0 ? orderResponseTimes.Max() : 0;

        return new StressTestOutput
        {
            TotalRequests = totalRequests,
            TotalTimeInSeconds = totalTimeInSeconds,
            SuccessfulRequests = successfulRequests,
            FailedRequests = failedRequests,
            QueriesPerSecond = qps,
            MinResponseTime = minResponseTime,
            MaxResponseTime = maxResponseTime,
            AverageResponseTime = averageResponseTime,
            Percentile10ResponseTime = CalculatePercentile(orderResponseTimes, 0.1),
            Percentile25ResponseTime = CalculatePercentile(orderResponseTimes, 0.25),
            Percentile50ResponseTime = CalculatePercentile(orderResponseTimes, 0.5),
            Percentile75ResponseTime = CalculatePercentile(orderResponseTimes, 0.75),
            Percentile90ResponseTime = CalculatePercentile(orderResponseTimes, 0.9),
            Percentile99ResponseTime = CalculatePercentile(orderResponseTimes, 0.99),
            Percentile999ResponseTime = CalculatePercentile(orderResponseTimes, 0.999),
            TimedOut = timedOut
        };
    }

    private StressTestEndpoint ResolveStressTestEndpoint(string requestUri, string requestMethod)
    {
        var sourcePath = Uri.TryCreate(requestUri, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri.AbsolutePath
            : requestUri?.Split('?', 2)[0];
        if (string.IsNullOrWhiteSpace(sourcePath)) throw Oops.Oh("接口路径不能为空");
        sourcePath = "/" + sourcePath.TrimStart('/');

        var endpoint = GetSafeStressTestEndpoints().FirstOrDefault(item =>
            item.Method.Equals(requestMethod, StringComparison.OrdinalIgnoreCase)
            && item.Route.Equals(sourcePath, StringComparison.OrdinalIgnoreCase));
        return endpoint ?? throw Oops.Oh("该接口不在允许压测的只读接口清单中");
    }

    private List<StressTestEndpoint> GetSafeStressTestEndpoints()
    {
        var endpoints = new List<StressTestEndpoint>();
        foreach (var group in _apiProvider.ApiDescriptionGroups.Items)
        {
            foreach (var apiDescription in group.Items)
            {
                if (!apiDescription.TryGetMethodInfo(out MethodInfo methodInfo)) continue;
                var httpMethod = apiDescription.HttpMethod?.ToUpperInvariant();
                if (httpMethod is not ("GET" or "POST")) continue;
                var route = "/" + (apiDescription.RelativePath ?? string.Empty).Split('?', 2)[0].TrimStart('/');
                var displayName = methodInfo.GetCustomAttribute<DisplayNameAttribute>(true)?.DisplayName ?? methodInfo.Name;
                if (!IsSafeStressTestEndpoint(methodInfo.Name, displayName, route)) continue;

                var serviceGroup = string.IsNullOrWhiteSpace(group.GroupName)
                    ? route.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault() ?? "other"
                    : group.GroupName;
                endpoints.Add(new StressTestEndpoint(serviceGroup, displayName, route, httpMethod));
            }
        }

        return endpoints
            .DistinctBy(item => $"{item.Method}:{item.Route}", StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsSafeStressTestEndpoint(string methodName, string displayName, string route)
    {
        var normalized = $"{methodName} {displayName} {route}".ToLowerInvariant();
        if (StressTestDeniedTerms.Any(term => normalized.Contains(term, StringComparison.OrdinalIgnoreCase))) return false;
        return StressTestAllowedPrefixes.Any(prefix => methodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateStressTestParameters(StressTestInput input)
    {
        ValidateParameters(input.Headers, "请求头", 20);
        ValidateParameters(input.PathParameters, "路径参数", 20);
        ValidateParameters(input.QueryParameters, "查询参数", 50);
        if ((input.RequestParameters?.Count ?? 0) > 50) throw Oops.Oh("请求体参数不能超过50项");
        foreach (var pair in input.RequestParameters ?? new()) ValidateParameter(pair.Key, pair.Value, "请求体参数");

        var restrictedHeaders = new[] { "Authorization", "Cookie", "Host", "Content-Length", "Connection", "Transfer-Encoding", "Proxy-Authorization" };
        if ((input.Headers ?? new()).Keys.Any(key => restrictedHeaders.Contains(key, StringComparer.OrdinalIgnoreCase)))
            throw Oops.Oh("请求头包含禁止由页面设置的敏感字段");
    }

    private static void ValidateParameters(Dictionary<string, string> parameters, string label, int maximumCount)
    {
        if ((parameters?.Count ?? 0) > maximumCount) throw Oops.Oh($"{label}不能超过{maximumCount}项");
        foreach (var pair in parameters ?? new()) ValidateParameter(pair.Key, pair.Value, label);
    }

    private static void ValidateParameter(string key, string value, string label)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100) throw Oops.Oh($"{label}名称不能为空且不能超过100个字符");
        if ((value?.Length ?? 0) > 2_000) throw Oops.Oh($"{label}值不能超过2000个字符");
    }

    private sealed record StressTestEndpoint(string GroupName, string DisplayName, string Route, string Method);

    /// <summary>
    /// 创建请求消息
    /// </summary>
    /// <param name="input">输入参数</param>
    /// <param name="fullUri">url</param>
    /// <returns></returns>
    private HttpRequestMessage CreateRequestMessage(StressTestInput input, Uri fullUri)
    {
        HttpRequestMessage request = input.RequestMethod switch
        {
            "GET" => new HttpRequestMessage(HttpMethod.Get, fullUri),
            "PUT" => new HttpRequestMessage(HttpMethod.Put, fullUri),
            "POST" => new HttpRequestMessage(HttpMethod.Post, fullUri),
            "DELETE" => new HttpRequestMessage(HttpMethod.Delete, fullUri),
            _ => throw Oops.Bah("请求方式异常")
        };

        // 设置请求头
        foreach (var header in input.Headers ?? new())
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return request;
    }

    /// <summary>
    /// 计算百分位请求耗时
    /// </summary>
    /// <param name="times">请求耗时列表</param>
    /// <param name="percentile">百分位</param>
    /// <returns></returns>
    private double CalculatePercentile(List<double> times, double percentile)
    {
        if (!times.Any()) return 0;
        var index = (int)Math.Ceiling(percentile * times.Count) - 1;
        return times[index < times.Count ? index : times.Count - 1];
    }
}
