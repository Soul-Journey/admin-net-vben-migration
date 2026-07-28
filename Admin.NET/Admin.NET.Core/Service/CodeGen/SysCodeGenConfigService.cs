// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统代码生成配置服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 260)]
public class SysCodeGenConfigService : IDynamicApiController, ITransient
{
    private readonly ISqlSugarClient _db;
    private readonly UserManager _userManager;

    public SysCodeGenConfigService(ISqlSugarClient db, UserManager userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    /// <summary>
    /// 获取代码生成配置列表 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取代码生成配置列表")]
    public async Task<List<CodeGenConfig>> GetList([FromQuery] CodeGenConfig input)
    {
        EnsureSuperAdmin();
        if (input.CodeGenId <= 0) throw Oops.Oh("代码生成记录不能为空");
        return await _db.Queryable<SysCodeGenConfig>()
            .Where(u => u.CodeGenId == input.CodeGenId)
            .Select<CodeGenConfig>()
            .Mapper(u =>
            {
                u.NetType = (u.EffectType == "EnumSelector" ? u.DictTypeCode : u.NetType);
                u.FkDisplayColumnList = u.FkDisplayColumns?.Split(",").ToList();
            })
            .OrderBy(u => new { u.OrderNo, u.Id })
            .ToListAsync();
    }

    /// <summary>
    /// 更新代码生成配置 🔖
    /// </summary>
    /// <param name="inputList"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    [DisplayName("更新代码生成配置")]
    public async Task UpdateCodeGenConfig(List<CodeGenConfig> inputList)
    {
        EnsureSuperAdmin();
        if (inputList == null || inputList.Count < 1) return;
        if (inputList.Count > 256) throw Oops.Oh("单次最多配置 256 个字段");

        var ids = inputList.Select(u => u.Id).Where(u => u > 0).Distinct().ToList();
        if (ids.Count != inputList.Count) throw Oops.Oh("字段配置包含空标识或重复项");
        var storedList = await _db.Queryable<SysCodeGenConfig>().Where(u => ids.Contains(u.Id)).ToListAsync();
        if (storedList.Count != ids.Count) throw Oops.Oh("部分字段配置不存在或已被修改，请刷新后重试");
        if (storedList.Select(u => u.CodeGenId).Distinct().Count() != 1)
            throw Oops.Oh("不能跨代码生成记录批量修改字段");

        inputList.ForEach(e =>
        {
            ValidateConfig(e);
            e.FkDisplayColumns = e.FkDisplayColumnList?.Count > 0 ? string.Join(",", e.FkDisplayColumnList) : null;
        });
        await _db.Updateable(inputList.Adapt<List<SysCodeGenConfig>>())
            .IgnoreColumns(u => new { u.ColumnLength, u.ColumnName, u.PropertyName })
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// 删除代码生成配置
    /// </summary>
    /// <param name="codeGenId"></param>
    /// <returns></returns>
    [NonAction]
    public async Task DeleteCodeGenConfig(long codeGenId)
    {
        await _db.Deleteable<SysCodeGenConfig>().Where(u => u.CodeGenId == codeGenId).ExecuteCommandAsync();
    }

    /// <summary>
    /// 获取代码生成配置详情 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取代码生成配置详情")]
    public async Task<SysCodeGenConfig> GetDetail([FromQuery] CodeGenConfig input)
    {
        return await _db.Queryable<SysCodeGenConfig>().FirstAsync(u => u.Id == input.Id);
    }

    /// <summary>
    /// 批量增加代码生成配置
    /// </summary>
    /// <param name="tableColumnOutputList"></param>
    /// <param name="codeGenerate"></param>
    [NonAction]
    public async Task AddList(List<ColumnOuput> tableColumnOutputList, SysCodeGen codeGenerate)
    {
        if (tableColumnOutputList == null) return;

        var codeGenConfigs = new List<SysCodeGenConfig>();
        var orderNo = 100;
        foreach (var tableColumn in tableColumnOutputList)
        {
            var codeGenConfig = new SysCodeGenConfig();

            var yesOrNo = YesNoEnum.Y.ToString();
            if (Convert.ToBoolean(tableColumn.ColumnKey)) yesOrNo = YesNoEnum.N.ToString();

            if (CodeGenUtil.IsCommonColumn(tableColumn.PropertyName))
            {
                codeGenConfig.WhetherCommon = YesNoEnum.Y.ToString();
                yesOrNo = YesNoEnum.N.ToString();
            }
            else
            {
                codeGenConfig.WhetherCommon = YesNoEnum.N.ToString();
            }

            codeGenConfig.CodeGenId = codeGenerate.Id;
            codeGenConfig.ColumnName = tableColumn.ColumnName; // 字段名
            codeGenConfig.PropertyName = tableColumn.PropertyName;// 实体属性名
            codeGenConfig.ColumnLength = tableColumn.ColumnLength;// 长度
            codeGenConfig.ColumnComment = tableColumn.ColumnComment;
            codeGenConfig.NetType = tableColumn.NetType;
            codeGenConfig.WhetherRetract = YesNoEnum.N.ToString();

            // 生成代码时，主键并不是必要输入项，故一定要排除主键字段
            codeGenConfig.WhetherRequired = (tableColumn.IsNullable || tableColumn.IsPrimarykey) ? YesNoEnum.N.ToString() : YesNoEnum.Y.ToString();
            codeGenConfig.WhetherQuery = yesOrNo;
            codeGenConfig.WhetherImport = yesOrNo;
            codeGenConfig.WhetherAddUpdate = yesOrNo;
            codeGenConfig.WhetherTable = yesOrNo;

            codeGenConfig.ColumnKey = tableColumn.ColumnKey;

            codeGenConfig.DataType = tableColumn.DataType;
            codeGenConfig.EffectType = CodeGenUtil.DataTypeToEff(codeGenConfig.NetType);
            codeGenConfig.QueryType = GetDefaultQueryType(codeGenConfig); // QueryTypeEnum.eq.ToString();
            codeGenConfig.OrderNo = orderNo;
            codeGenConfigs.Add(codeGenConfig);

            if (!string.IsNullOrWhiteSpace(tableColumn.DictTypeCode))
            {
                codeGenConfig.QueryType = "==";
                codeGenConfig.DictTypeCode = tableColumn.DictTypeCode;
                codeGenConfig.EffectType = tableColumn.DictTypeCode.EndsWith("Enum") ? "EnumSelector" : "DictSelector";
            }

            orderNo += 10; // 每个配置排序间隔10
        }
        // 多库代码生成---这里要切回主库
        var provider = _db.AsTenant().GetConnectionScope(SqlSugarConst.MainConfigId);
        await provider.Insertable(codeGenConfigs).ExecuteCommandAsync();
    }

    /// <summary>
    /// 默认查询类型
    /// </summary>
    /// <param name="codeGenConfig"></param>
    /// <returns></returns>
    private static string GetDefaultQueryType(SysCodeGenConfig codeGenConfig)
    {
        return (codeGenConfig.NetType?.TrimEnd('?')) switch
        {
            "string" => "like",
            "DateTime" => "~",
            _ => "==",
        };
    }

    private void EnsureSuperAdmin()
    {
        if (!_userManager.SuperAdmin)
            throw Oops.Oh("代码生成仅允许超级管理员使用");
    }

    private static void ValidateConfig(CodeGenConfig input)
    {
        var yesNoValues = new[] { "Y", "N" };
        var queryTypes = new[] { "==", "!=", ">", ">=", "<", "<=", "like", "in", "not in", "isNotNull", "~" };
        var effectTypes = new[]
        {
            "Input", "InputNumber", "InputPassword", "InputTextArea", "TextArea", "Select", "Radio", "Checkbox",
            "DatePicker", "TimePicker", "Switch", "Slider", "Rate", "ColorPicker", "Upload",
            "DictSelector", "EnumSelector", "ConstSelector", "ForeignKey", "ApiTreeSelector"
        };

        if (!string.IsNullOrWhiteSpace(input.EffectType) && !effectTypes.Contains(input.EffectType))
            throw Oops.Oh($"字段 {input.ColumnComment ?? input.ColumnName} 的控件类型无效");
        if (!string.IsNullOrWhiteSpace(input.QueryType) && !queryTypes.Contains(input.QueryType))
            throw Oops.Oh($"字段 {input.ColumnComment ?? input.ColumnName} 的查询方式无效");

        var switches = new[]
        {
            input.WhetherQuery, input.WhetherRetract, input.WhetherRequired, input.WhetherSortable,
            input.WhetherTable, input.WhetherAddUpdate, input.WhetherImport, input.WhetherCommon
        };
        if (switches.Any(u => !string.IsNullOrWhiteSpace(u) && !yesNoValues.Contains(u)))
            throw Oops.Oh($"字段 {input.ColumnComment ?? input.ColumnName} 的开关值无效");

        if (input.OrderNo is < 0 or > 100000) throw Oops.Oh("字段排序必须在 0 到 100000 之间");
        if (input.FkDisplayColumnList?.Count > 8) throw Oops.Oh("外键显示字段最多选择 8 个");
        if (input.FkDisplayColumnList?.Any(u => !Regex.IsMatch(u ?? "", "^[A-Za-z_][A-Za-z0-9_]{0,63}$")) == true)
            throw Oops.Oh("外键显示字段格式无效");

        EnsureOptionalSafeText(input.ColumnComment, "字段说明", 128);
        EnsureOptionalIdentifier(input.DictTypeCode, "字典或枚举编码", 64, true);
        EnsureOptionalIdentifier(input.FkConfigId, "关联库标识", 20, true);
        EnsureOptionalIdentifier(input.FkEntityName, "关联实体", 64);
        EnsureOptionalIdentifier(input.FkTableName, "关联表", 128);
        EnsureOptionalIdentifier(input.FkLinkColumnName, "关联字段", 64);
        EnsureOptionalIdentifier(input.PidColumn, "父级字段", 128);
    }

    private static void EnsureOptionalIdentifier(string value, string label, int maxLength, bool allowNumberPrefix = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var pattern = allowNumberPrefix ? "^[A-Za-z0-9_][A-Za-z0-9_.-]*$" : "^[A-Za-z_][A-Za-z0-9_]*$";
        if (value.Length > maxLength || !Regex.IsMatch(value, pattern))
            throw Oops.Oh($"{label}格式无效");
    }

    private static void EnsureOptionalSafeText(string value, string label, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value.Length > maxLength || value.IndexOfAny(new[] { '\"', '\'', '\\', '\r', '\n', '{', '}' }) >= 0)
            throw Oops.Oh($"{label}不能超长，且不能包含引号、换行、反斜杠或花括号");
    }
}
