// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.AspNetCore.Http;

namespace Admin.NET.Plugin.ApprovalFlow.Service;

/// <summary>
/// 审批流程服务
/// </summary>
[ApiDescriptionSettings(ApprovalFlowConst.GroupName, Order = 100)]
public class ApprovalFlowService : IDynamicApiController, ITransient
{
    private static readonly SemaphoreSlim CodeLock = new(1, 1);
    private static readonly HashSet<string> AllowedFormOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add", "update", "delete", "select", "export"
    };
    private static readonly HashSet<string> AllowedNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "bpmn:startEvent", "bpmn:userTask", "bpmn:exclusiveGateway", "task-node", "bpmn:endEvent",
        "start-node", "end-node", "user-node", "sql-node"
    };
    private static readonly HashSet<string> AllowedEdgeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "polyline", "line", "bezier", "bpmn:sequenceFlow", "edge-sql"
    };

    private readonly SqlSugarRepository<ApprovalFlow> _approvalFlowRep;
    private readonly UserManager _userManager;

    public ApprovalFlowService(SqlSugarRepository<ApprovalFlow> approvalFlowRep, UserManager userManager)
    {
        _approvalFlowRep = approvalFlowRep;
        _userManager = userManager;
    }

    /// <summary>
    /// 分页查询审批流
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpPost]
    [ApiDescriptionSettings(Name = "Page")]
    [DisplayName("分页查询审批流程定义")]
    public async Task<SqlSugarPagedList<ApprovalFlowOutput>> Page(ApprovalFlowInput input)
    {
        EnsureSuperAdmin();
        return await _approvalFlowRep.AsQueryable()
            .WhereIF(!string.IsNullOrWhiteSpace(input.Code), u => u.Code.Contains(input.Code.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.Name), u => u.Name.Contains(input.Name.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.Remark), u => u.Remark.Contains(input.Remark.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.Keyword), u => u.Code.Contains(input.Keyword.Trim()) || u.Name.Contains(input.Keyword.Trim()) || u.Remark.Contains(input.Keyword.Trim()))
            .Select<ApprovalFlowOutput>()
            .ToPagedListAsync(input.Page, input.PageSize);
    }

    /// <summary>
    /// 增加审批流
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Add"), HttpPost]
    [DisplayName("新增审批流程定义")]
    public async Task<long> Add(AddApprovalFlowInput input)
    {
        EnsureSuperAdmin();
        var name = input.Name.Trim();
        var code = input.Code?.Trim();
        if (await _approvalFlowRep.IsAnyAsync(u => u.Name == name || (!string.IsNullOrEmpty(code) && u.Code == code)))
            throw Oops.Oh("流程名称或编号已存在");

        await CodeLock.WaitAsync();
        try
        {
            code ??= await LastCode("");
            if (await _approvalFlowRep.IsAnyAsync(u => u.Code == code))
                throw Oops.Oh("流程编号已存在，请重试");
            var entity = new ApprovalFlow
            {
                Code = code,
                Name = name,
                Status = input.Status,
                Remark = input.Remark?.Trim()
            };
            await _approvalFlowRep.InsertAsync(entity);
            return entity.Id;
        }
        finally
        {
            CodeLock.Release();
        }
    }

    /// <summary>
    /// 更新审批流
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    [DisplayName("更新审批流程定义")]
    public async Task Update(UpdateApprovalFlowInput input)
    {
        EnsureSuperAdmin();
        var entity = await GetEntity(input.Id);
        var name = input.Name.Trim();
        var code = input.Code?.Trim();
        if (await _approvalFlowRep.IsAnyAsync(u => u.Id != input.Id && (u.Name == name || (!string.IsNullOrEmpty(code) && u.Code == code))))
            throw Oops.Oh("流程名称或编号已存在");

        if (input.FormJson != null) ValidateFormJson(input.FormJson);
        if (input.FlowJson != null) ValidateFlowJson(input.FlowJson);
        entity.Code = string.IsNullOrWhiteSpace(code) ? entity.Code : code;
        entity.Name = name;
        entity.Status = input.Status;
        entity.Remark = input.Remark?.Trim();
        if (input.FormJson != null) entity.FormJson = input.FormJson;
        if (input.FlowJson != null) entity.FlowJson = input.FlowJson;
        await _approvalFlowRep.UpdateAsync(entity);
    }

    /// <summary>
    /// 单独保存业务表绑定配置
    /// </summary>
    [ApiDescriptionSettings(Name = "UpdateForm"), HttpPost]
    [DisplayName("保存审批流程业务表绑定")]
    public async Task UpdateForm(UpdateApprovalFlowJsonInput input)
    {
        EnsureSuperAdmin();
        ValidateFormJson(input.Json);
        var entity = await GetEntity(input.Id);
        entity.FormJson = input.Json;
        await _approvalFlowRep.AsUpdateable(entity).UpdateColumns(u => u.FormJson).ExecuteCommandAsync();
    }

    /// <summary>
    /// 单独保存流程设计配置
    /// </summary>
    [ApiDescriptionSettings(Name = "UpdateFlow"), HttpPost]
    [DisplayName("保存审批流程设计")]
    public async Task UpdateFlow(UpdateApprovalFlowJsonInput input)
    {
        EnsureSuperAdmin();
        ValidateFlowJson(input.Json);
        var entity = await GetEntity(input.Id);
        entity.FlowJson = input.Json;
        await _approvalFlowRep.AsUpdateable(entity).UpdateColumns(u => u.FlowJson).ExecuteCommandAsync();
    }

    /// <summary>
    /// 删除审批流
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Delete"), HttpPost]
    [DisplayName("删除审批流程定义")]
    public async Task Delete(BaseIdInput input)
    {
        EnsureSuperAdmin();
        var entity = await GetEntity(input.Id);
        await _approvalFlowRep.FakeDeleteAsync(entity);  // 假删除
    }

    /// <summary>
    /// 获取审批流
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async Task<ApprovalFlow> GetDetail([FromQuery] BaseIdInput input)
    {
        EnsureSuperAdmin();
        return await GetEntity(input.Id);
    }

    /// <summary>
    /// 根据编码获取审批流信息
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public async Task<ApprovalFlow> GetInfo([FromQuery] string code)
    {
        EnsureSuperAdmin();
        return await _approvalFlowRep.GetFirstAsync(u => u.Code == code) ?? throw Oops.Oh(ErrorCodeEnum.D1002);
    }

    /// <summary>
    /// 获取审批流列表
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async Task<List<ApprovalFlowOutput>> GetList([FromQuery] ApprovalFlowInput input)
    {
        EnsureSuperAdmin();
        return await _approvalFlowRep.AsQueryable()
            .WhereIF(!string.IsNullOrWhiteSpace(input.Code), u => u.Code.Contains(input.Code.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.Name), u => u.Name.Contains(input.Name.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.Remark), u => u.Remark.Contains(input.Remark.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.Keyword), u => u.Code.Contains(input.Keyword.Trim()) || u.Name.Contains(input.Keyword.Trim()) || u.Remark.Contains(input.Keyword.Trim()))
            .Select<ApprovalFlowOutput>()
            .ToListAsync();
    }

    /// <summary>
    /// 获取今天创建的最大编号
    /// </summary>
    /// <param name="prefix"></param>
    /// <returns></returns>
    private async Task<string> LastCode(string prefix)
    {
        var dayPrefix = prefix + DateTime.Now.ToString("yyMMdd");
        var latest = await _approvalFlowRep.AsQueryable()
            .Where(u => u.Code.StartsWith(dayPrefix))
            .OrderByDescending(u => u.Code)
            .Select(u => u.Code)
            .FirstAsync();
        var sequence = 0;
        if (!string.IsNullOrWhiteSpace(latest) && latest.Length > dayPrefix.Length)
            int.TryParse(latest[dayPrefix.Length..], out sequence);
        return $"{dayPrefix}{sequence + 1:d2}";
    }

    private void EnsureSuperAdmin()
    {
        if (!_userManager.SuperAdmin) throw Oops.Oh(ErrorCodeEnum.D3010);
    }

    private async Task<ApprovalFlow> GetEntity(long id)
    {
        return await _approvalFlowRep.GetFirstAsync(u => u.Id == id) ?? throw Oops.Oh(ErrorCodeEnum.D1002);
    }

    private static void ValidateFormJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 16 * 1024)
            throw Oops.Oh("业务表绑定配置为空或超过 16KB");
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw Oops.Oh("业务表绑定配置必须是 JSON 对象");
            ValidateIdentifier(root, "configId", 64, false);
            ValidateIdentifier(root, "tableName", 128, true);
            if (!root.TryGetProperty("typeName", out var typeName) || typeName.ValueKind != JsonValueKind.String ||
                !AllowedFormOperations.Contains(typeName.GetString() ?? string.Empty))
                throw Oops.Oh("业务操作类型仅支持新增、更新、删除、查询或导出");
        }
        catch (JsonException)
        {
            throw Oops.Oh("业务表绑定配置不是有效 JSON");
        }
    }

    private static void ValidateIdentifier(JsonElement root, string propertyName, int maxLength, bool required)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            if (required) throw Oops.Oh($"{propertyName} 不能为空");
            return;
        }
        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required) throw Oops.Oh($"{propertyName} 不能为空");
            return;
        }
        if (value.Length > maxLength || value.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.')))
            throw Oops.Oh($"{propertyName} 格式无效");
    }

    private static void ValidateFlowJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 1024 * 1024)
            throw Oops.Oh("流程配置为空或超过 1MB");
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array ||
                !root.TryGetProperty("edges", out var edges) || edges.ValueKind != JsonValueKind.Array)
                throw Oops.Oh("流程配置必须包含 nodes 和 edges 数组");
            if (nodes.GetArrayLength() > 500 || edges.GetArrayLength() > 1000)
                throw Oops.Oh("流程节点或连线数量超过安全上限");
            foreach (var node in nodes.EnumerateArray()) ValidateGraphType(node, AllowedNodeTypes, "节点");
            foreach (var edge in edges.EnumerateArray()) ValidateGraphType(edge, AllowedEdgeTypes, "连线");
        }
        catch (JsonException)
        {
            throw Oops.Oh("流程配置不是有效 JSON");
        }
    }

    private static void ValidateGraphType(JsonElement item, HashSet<string> allowedTypes, string label)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
            !allowedTypes.Contains(type.GetString() ?? string.Empty))
            throw Oops.Oh($"{label}类型不受支持");
    }

    /// <summary>
    /// 匹配审批流程
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    [NonAction]
    public async Task MatchApproval(HttpContext context)
    {
        var request = context.Request;
        var response = context.Response;

        var path = request.Path.ToString().Split("/");

        var method = request.Method;
        var query = request.QueryString;
        var header = request.Headers;
        var body = request.Body;

        var requestHeaders = request.Headers;
        var responseHeaders = response.Headers;

        await Task.CompletedTask;
    }
}
