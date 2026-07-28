// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Npgsql;

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统数据库管理服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 250)]
public class SysDatabaseService : IDynamicApiController, ITransient
{
    private static readonly SemaphoreSlim DatabaseWriteLock = new(1, 1);
    private static readonly Regex DatabaseIdentifierRegex = new("^[\\p{L}_][\\p{L}\\p{N}_]{0,127}$", RegexOptions.Compiled);
    private readonly ISqlSugarClient _db;
    private readonly IViewEngine _viewEngine;
    private readonly CodeGenOptions _codeGenOptions;
    private readonly UserManager _userManager;

    public SysDatabaseService(ISqlSugarClient db,
        IViewEngine viewEngine,
        IOptions<CodeGenOptions> codeGenOptions,
        UserManager userManager)
    {
        _db = db;
        _viewEngine = viewEngine;
        _codeGenOptions = codeGenOptions.Value;
        _userManager = userManager;
    }

    /// <summary>
    /// 获取库列表 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取库列表")]
    public List<string> GetList()
    {
        EnsureSuperAdmin();
        return App.GetOptions<DbConnectionOptions>().ConnectionConfigs.Select(u => u.ConfigId.ToString()).ToList();
    }

    /// <summary>
    /// 获取可视化库表结构 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取可视化库表结构")]
    public VisualDbTable GetVisualDbTable()
    {
        EnsureSuperAdmin();
        var visualTableList = new List<VisualTable>();
        var visualColumnList = new List<VisualColumn>();
        var columnRelationList = new List<ColumnRelation>();
        var dbOptions = App.GetOptions<DbConnectionOptions>().ConnectionConfigs.First(u => u.ConfigId.ToString() == SqlSugarConst.MainConfigId);

        // 遍历所有实体获取所有库表结构
        var random = new Random();
        var entityTypes = App.EffectiveTypes.Where(u => !u.IsInterface && !u.IsAbstract && u.IsClass && u.IsDefined(typeof(SugarTable), false)).ToList();
        foreach (var entityType in entityTypes)
        {
            var entityInfo = _db.EntityMaintenance.GetEntityInfoNoCache(entityType);

            var visualTable = new VisualTable
            {
                TableName = entityInfo.DbTableName,
                TableComents = entityInfo.TableDescription + entityInfo.DbTableName,
                X = random.Next(5000),
                Y = random.Next(5000)
            };
            visualTableList.Add(visualTable);

            foreach (EntityColumnInfo columnInfo in entityInfo.Columns)
            {
                var visualColumn = new VisualColumn
                {
                    TableName = columnInfo.DbTableName,
                    ColumnName = dbOptions.DbSettings.EnableUnderLine ? UtilMethods.ToUnderLine(columnInfo.DbColumnName) : columnInfo.DbColumnName,
                    DataType = columnInfo.PropertyInfo.PropertyType.Name,
                    DataLength = columnInfo.Length.ToString(),
                    ColumnDescription = columnInfo.ColumnDescription,
                };
                visualColumnList.Add(visualColumn);

                // 根据导航配置获取表之间关联关系
                if (columnInfo.Navigat != null)
                {
                    var name1 = columnInfo.Navigat.GetName();
                    var name2 = columnInfo.Navigat.GetName2();
                    var targetColumnName = string.IsNullOrEmpty(name2) ? "Id" : name2;
                    var relation = new ColumnRelation
                    {
                        SourceTableName = columnInfo.DbTableName,
                        SourceColumnName = dbOptions.DbSettings.EnableUnderLine ? UtilMethods.ToUnderLine(name1) : name1,
                        Type = columnInfo.Navigat.GetNavigateType() == NavigateType.OneToOne ? "ONE_TO_ONE" : "ONE_TO_MANY",
                        TargetTableName = dbOptions.DbSettings.EnableUnderLine ? UtilMethods.ToUnderLine(columnInfo.DbColumnName) : columnInfo.DbColumnName,
                        TargetColumnName = dbOptions.DbSettings.EnableUnderLine ? UtilMethods.ToUnderLine(targetColumnName) : targetColumnName
                    };
                    columnRelationList.Add(relation);
                }
            }
        }

        return new VisualDbTable { VisualTableList = visualTableList, VisualColumnList = visualColumnList, ColumnRelationList = columnRelationList };
    }

    /// <summary>
    /// 获取字段列表 🔖
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="configId">ConfigId</param>
    /// <returns></returns>
    [DisplayName("获取字段列表")]
    public List<DbColumnOutput> GetColumnList(string tableName, string configId = SqlSugarConst.MainConfigId)
    {
        EnsureDatabaseAccess(configId);
        EnsureIdentifier(tableName, "表名");
        var db = _db.AsTenant().GetConnectionScope(configId);
        return string.IsNullOrWhiteSpace(tableName) ? new List<DbColumnOutput>() : db.DbMaintenance.GetColumnInfosByTableName(tableName, false).Adapt<List<DbColumnOutput>>();
    }

    /// <summary>
    /// 获取数据库数据类型列表 🔖
    /// </summary>
    /// <param name="configId"></param>
    /// <returns></returns>
    [DisplayName("获取数据库数据类型列表")]
    public List<string> GetDbTypeList(string configId = SqlSugarConst.MainConfigId)
    {
        EnsureDatabaseAccess(configId);
        var db = _db.AsTenant().GetConnectionScope(configId);
        return db.DbMaintenance.GetDbTypes().OrderBy(u => u).ToList();
    }

    /// <summary>
    /// 增加列 🔖
    /// </summary>
    /// <param name="input"></param>
    [ApiDescriptionSettings(Name = "AddColumn"), HttpPost]
    [DisplayName("增加列")]
    public void AddColumn(DbColumnInput input)
    {
        EnsureDatabaseAccess(input.ConfigId);
        EnsureIdentifier(input.TableName, "表名");
        ValidateColumn(input);
        var column = new DbColumnInfo
        {
            ColumnDescription = input.ColumnDescription,
            DbColumnName = input.DbColumnName,
            IsIdentity = input.IsIdentity == 1,
            IsNullable = input.IsNullable == 1,
            IsPrimarykey = input.IsPrimarykey == 1,
            Length = input.Length,
            DecimalDigits = input.DecimalDigits,
            DataType = input.DataType
        };
        DatabaseWriteLock.Wait();
        try
        {
            var db = _db.AsTenant().GetConnectionScope(input.ConfigId);
            EnsureTableExists(db, input.TableName);
            if (db.DbMaintenance.GetColumnInfosByTableName(input.TableName, false).Any(u => u.DbColumnName.Equals(input.DbColumnName, StringComparison.OrdinalIgnoreCase)))
                throw Oops.Oh($"字段 {input.DbColumnName} 已存在");
            db.DbMaintenance.AddColumn(input.TableName, column);
            if (!string.IsNullOrWhiteSpace(input.ColumnDescription))
                db.DbMaintenance.AddColumnRemark(input.DbColumnName, input.TableName, input.ColumnDescription);
            if (column.IsPrimarykey) db.DbMaintenance.AddPrimaryKey(input.TableName, input.DbColumnName);
        }
        finally
        {
            DatabaseWriteLock.Release();
        }
    }

    /// <summary>
    /// 删除列 🔖
    /// </summary>
    /// <param name="input"></param>
    [ApiDescriptionSettings(Name = "DeleteColumn"), HttpPost]
    [DisplayName("删除列")]
    public void DeleteColumn(DeleteDbColumnInput input)
    {
        EnsureDatabaseAccess(input.ConfigId);
        EnsureIdentifier(input.TableName, "表名");
        EnsureIdentifier(input.DbColumnName, "字段名");
        DatabaseWriteLock.Wait();
        try
        {
            var db = _db.AsTenant().GetConnectionScope(input.ConfigId);
            EnsureTableExists(db, input.TableName);
            var columns = db.DbMaintenance.GetColumnInfosByTableName(input.TableName, false);
            var column = columns.FirstOrDefault(u => u.DbColumnName.Equals(input.DbColumnName, StringComparison.OrdinalIgnoreCase)) ?? throw Oops.Oh("字段不存在或已被删除");
            if (columns.Count <= 1) throw Oops.Oh("不能删除数据表的最后一个字段");
            if (column.IsPrimarykey) throw Oops.Oh("主键字段不能直接删除，请先调整主键结构");
            db.DbMaintenance.DropColumn(input.TableName, input.DbColumnName);
        }
        finally
        {
            DatabaseWriteLock.Release();
        }
    }

    /// <summary>
    /// 编辑列 🔖
    /// </summary>
    /// <param name="input"></param>
    [ApiDescriptionSettings(Name = "UpdateColumn"), HttpPost]
    [DisplayName("编辑列")]
    public void UpdateColumn(UpdateDbColumnInput input)
    {
        EnsureDatabaseAccess(input.ConfigId);
        EnsureIdentifier(input.TableName, "表名");
        EnsureIdentifier(input.OldColumnName, "原字段名");
        EnsureIdentifier(input.ColumnName, "新字段名");
        DatabaseWriteLock.Wait();
        try
        {
            var db = _db.AsTenant().GetConnectionScope(input.ConfigId);
            EnsureTableExists(db, input.TableName);
            var columns = db.DbMaintenance.GetColumnInfosByTableName(input.TableName, false);
            if (!columns.Any(u => u.DbColumnName.Equals(input.OldColumnName, StringComparison.OrdinalIgnoreCase)))
                throw Oops.Oh("原字段不存在或已被修改");
            if (!input.OldColumnName.Equals(input.ColumnName, StringComparison.OrdinalIgnoreCase) && columns.Any(u => u.DbColumnName.Equals(input.ColumnName, StringComparison.OrdinalIgnoreCase)))
                throw Oops.Oh($"字段 {input.ColumnName} 已存在");
            if (!input.OldColumnName.Equals(input.ColumnName, StringComparison.Ordinal))
                db.DbMaintenance.RenameColumn(input.TableName, input.OldColumnName, input.ColumnName);
            if (db.DbMaintenance.IsAnyColumnRemark(input.ColumnName, input.TableName))
                db.DbMaintenance.DeleteColumnRemark(input.ColumnName, input.TableName);
            if (!string.IsNullOrWhiteSpace(input.Description))
                db.DbMaintenance.AddColumnRemark(input.ColumnName, input.TableName, input.Description);
        }
        finally
        {
            DatabaseWriteLock.Release();
        }
    }

    /// <summary>
    /// 获取表列表 🔖
    /// </summary>
    /// <param name="configId">ConfigId</param>
    /// <returns></returns>
    [DisplayName("获取表列表")]
    public List<DbTableInfo> GetTableList(string configId = SqlSugarConst.MainConfigId)
    {
        EnsureDatabaseAccess(configId);
        var db = _db.AsTenant().GetConnectionScope(configId);
        return db.DbMaintenance.GetTableInfoList(false);
    }

    /// <summary>
    /// 增加表 🔖
    /// </summary>
    /// <param name="input"></param>
    [ApiDescriptionSettings(Name = "AddTable"), HttpPost]
    [DisplayName("增加表")]
    public void AddTable(DbTableInput input)
    {
        EnsureDatabaseAccess(input.ConfigId);
        EnsureIdentifier(input.TableName, "表名");
        if (input.DbColumnInfoList == null || !input.DbColumnInfoList.Any())
            throw Oops.Oh(ErrorCodeEnum.db1000);

        if (input.DbColumnInfoList.GroupBy(u => u.DbColumnName).Any(u => u.Count() > 1))
            throw Oops.Oh(ErrorCodeEnum.db1002);

        input.DbColumnInfoList.ForEach(ValidateColumn);
        DatabaseWriteLock.Wait();
        try
        {
            var config = App.GetOptions<DbConnectionOptions>().ConnectionConfigs.First(u => u.ConfigId.ToString() == input.ConfigId);
            var db = _db.AsTenant().GetConnectionScope(input.ConfigId);
            if (db.DbMaintenance.GetTableInfoList(false).Any(u => u.Name.Equals(input.TableName, StringComparison.OrdinalIgnoreCase)))
                throw Oops.Oh($"数据表 {input.TableName} 已存在");
            var validTypes = db.DbMaintenance.GetDbTypes();
            if (input.DbColumnInfoList.Any(u => !validTypes.Contains(u.DataType, StringComparer.OrdinalIgnoreCase)))
                throw Oops.Oh("包含当前数据库不支持的字段类型");
            var typeBuilder = db.DynamicBuilder().CreateClass(input.TableName, new SugarTable() { TableName = input.TableName, TableDescription = input.Description });
            input.DbColumnInfoList.ForEach(u =>
            {
                var dbColumnName = config.DbSettings.EnableUnderLine ? UtilMethods.ToUnderLine(u.DbColumnName.Trim()) : u.DbColumnName.Trim();
                typeBuilder.CreateProperty(dbColumnName, typeof(string), new SugarColumn()
                {
                    IsPrimaryKey = u.IsPrimarykey == 1,
                    IsIdentity = u.IsIdentity == 1,
                    ColumnDataType = u.DataType,
                    Length = u.Length,
                    IsNullable = u.IsNullable == 1,
                    DecimalDigits = u.DecimalDigits,
                    ColumnDescription = u.ColumnDescription,
                });
            });
            db.CodeFirst.InitTables(typeBuilder.BuilderType());
        }
        finally
        {
            DatabaseWriteLock.Release();
        }
    }

    /// <summary>
    /// 删除表 🔖
    /// </summary>
    /// <param name="input"></param>
    [ApiDescriptionSettings(Name = "DeleteTable"), HttpPost]
    [DisplayName("删除表")]
    public void DeleteTable(DeleteDbTableInput input)
    {
        EnsureDatabaseAccess(input.ConfigId);
        EnsureIdentifier(input.TableName, "表名");
        DatabaseWriteLock.Wait();
        try
        {
            var db = _db.AsTenant().GetConnectionScope(input.ConfigId);
            EnsureTableExists(db, input.TableName);
            db.DbMaintenance.DropTable(input.TableName);
        }
        finally
        {
            DatabaseWriteLock.Release();
        }
    }

    /// <summary>
    /// 编辑表 🔖
    /// </summary>
    /// <param name="input"></param>
    [ApiDescriptionSettings(Name = "UpdateTable"), HttpPost]
    [DisplayName("编辑表")]
    public void UpdateTable(UpdateDbTableInput input)
    {
        EnsureDatabaseAccess(input.ConfigId);
        EnsureIdentifier(input.OldTableName, "原表名");
        EnsureIdentifier(input.TableName, "新表名");
        DatabaseWriteLock.Wait();
        try
        {
            var db = _db.AsTenant().GetConnectionScope(input.ConfigId);
            EnsureTableExists(db, input.OldTableName);
            if (!input.OldTableName.Equals(input.TableName, StringComparison.OrdinalIgnoreCase) && db.DbMaintenance.GetTableInfoList(false).Any(u => u.Name.Equals(input.TableName, StringComparison.OrdinalIgnoreCase)))
                throw Oops.Oh($"数据表 {input.TableName} 已存在");
            if (!input.OldTableName.Equals(input.TableName, StringComparison.Ordinal))
                db.DbMaintenance.RenameTable(input.OldTableName, input.TableName);
            if (db.DbMaintenance.IsAnyTableRemark(input.TableName))
                db.DbMaintenance.DeleteTableRemark(input.TableName);

            if (!string.IsNullOrWhiteSpace(input.Description))
                db.DbMaintenance.AddTableRemark(input.TableName, input.Description);
        }
        catch (NotSupportedException ex)
        {
            throw Oops.Oh(ex.ToString());
        }
        finally
        {
            DatabaseWriteLock.Release();
        }
    }

    /// <summary>
    /// 创建实体 🔖
    /// </summary>
    /// <param name="input"></param>
    [ApiDescriptionSettings(Name = "CreateEntity"), HttpPost]
    [DisplayName("创建实体")]
    public void CreateEntity(CreateEntityInput input)
    {
        EnsureDatabaseAccess(input.ConfigId);
        EnsureIdentifier(input.TableName, "表名");
        input.Position = string.IsNullOrWhiteSpace(input.Position) ? "Admin.NET.Application" : input.Position;
        EnsureCodeOutputPosition(input.Position);
        var config = App.GetOptions<DbConnectionOptions>().ConnectionConfigs.First(u => u.ConfigId.ToString() == input.ConfigId);
        input.EntityName = string.IsNullOrWhiteSpace(input.EntityName) ? (config.DbSettings.EnableUnderLine ? CodeGenUtil.CamelColumnName(input.TableName, null) : input.TableName) : input.EntityName;
        EnsureIdentifier(input.EntityName, "实体名");
        string[] dbColumnNames = Array.Empty<string>();
        // Entity.cs.vm中是允许创建没有基类的实体的，所以这里也要做出相同的判断
        if (!string.IsNullOrWhiteSpace(input.BaseClassName))
        {
            _codeGenOptions.EntityBaseColumn.TryGetValue(input.BaseClassName, out dbColumnNames);
            if (dbColumnNames is null || dbColumnNames is { Length: 0 })
                throw Oops.Oh("基类配置文件不存在此类型");
        }
        var templatePath = GetEntityTemplatePath();
        var targetPath = GetEntityTargetPath(input);
        if (File.Exists(targetPath)) throw Oops.Oh($"实体文件 {input.EntityName}.cs 已存在，请先人工确认旧文件");
        var db = _db.AsTenant().GetConnectionScope(input.ConfigId);
        DbTableInfo dbTableInfo = db.DbMaintenance.GetTableInfoList(false).FirstOrDefault(u => u.Name == input.TableName || u.Name == input.TableName.ToLower()) ?? throw Oops.Oh(ErrorCodeEnum.db1001);
        List<DbColumnInfo> dbColumnInfos = db.DbMaintenance.GetColumnInfosByTableName(input.TableName, false);
        dbColumnInfos.ForEach(u =>
        {
            u.PropertyName = config.DbSettings.EnableUnderLine ? CodeGenUtil.CamelColumnName(u.DbColumnName, dbColumnNames) : u.DbColumnName; // 转下划线后的列名需要再转回来
            u.DataType = CodeGenUtil.ConvertDataType(u, config.DbType);
        });
        if (_codeGenOptions.BaseEntityNames.Contains(input.BaseClassName, StringComparer.OrdinalIgnoreCase))
            dbColumnInfos = dbColumnInfos.Where(u => !dbColumnNames.Contains(u.PropertyName, StringComparer.OrdinalIgnoreCase)).ToList();

        var tContent = File.ReadAllText(templatePath);
        var tResult = _viewEngine.RunCompileFromCached(tContent, new
        {
            NameSpace = $"{input.Position}.Entity",
            input.TableName,
            input.EntityName,
            BaseClassName = string.IsNullOrWhiteSpace(input.BaseClassName) ? "" : $": {input.BaseClassName}",
            input.ConfigId,
            dbTableInfo.Description,
            TableField = dbColumnInfos
        });
        File.WriteAllText(targetPath, tResult, Encoding.UTF8);
    }

    /// <summary>
    /// 创建种子数据 🔖
    /// </summary>
    /// <param name="input"></param>
    [ApiDescriptionSettings(Name = "CreateSeedData"), HttpPost]
    [DisplayName("创建种子数据")]
    public async Task CreateSeedData(CreateSeedDataInput input)
    {
        EnsureDatabaseAccess(input.ConfigId);
        EnsureIdentifier(input.TableName, "表名");
        input.Position = string.IsNullOrWhiteSpace(input.Position) ? "Admin.NET.Core" : input.Position;
        EnsureCodeOutputPosition(input.Position);
        if (!string.IsNullOrWhiteSpace(input.Suffix) && !Regex.IsMatch(input.Suffix, "^[A-Za-z0-9_]{1,64}$"))
            throw Oops.Oh("种子后缀只能包含英文字母、数字和下划线");
        var config = App.GetOptions<DbConnectionOptions>().ConnectionConfigs.First(u => u.ConfigId.ToString() == input.ConfigId);

        await DatabaseWriteLock.WaitAsync();
        try
        {
        var templatePath = GetSeedDataTemplatePath();
        var db = _db.AsTenant().GetConnectionScope(input.ConfigId);
        var tableInfo = db.DbMaintenance.GetTableInfoList(false).First(u => u.Name == input.TableName); // 表名
        List<DbColumnInfo> dbColumnInfos = db.DbMaintenance.GetColumnInfosByTableName(input.TableName, false); // 所有字段
        IEnumerable<EntityInfo> entityInfos = await GetEntityInfos();
        Type entityType = null;
        foreach (var item in entityInfos)
        {
            if (tableInfo.Name.ToLower() != (config.DbSettings.EnableUnderLine ? UtilMethods.ToUnderLine(item.DbTableName) : item.DbTableName).ToLower()) continue;
            entityType = item.Type;
            break;
        }
        if (entityType == null) throw Oops.Oh(ErrorCodeEnum.db1003);

        input.EntityName = entityType.Name;
        input.SeedDataName = entityType.Name + "SeedData";
        if (!string.IsNullOrWhiteSpace(input.Suffix)) input.SeedDataName += input.Suffix;

        // 查询所有数据
        var query = db.QueryableByObject(entityType);
        // 优先用创建时间排序
        DbColumnInfo orderField = dbColumnInfos.FirstOrDefault(u => u.DbColumnName.ToLower() == "create_time" || u.DbColumnName.ToLower() == "createtime");
        if (orderField != null) query = query.OrderBy(orderField.DbColumnName);
        // 再使用第一个主键排序
        query = query.OrderBy(dbColumnInfos.First(u => u.IsPrimarykey).DbColumnName);
        var records = ((IEnumerable)await query.ToListAsync()).ToDynamicList();

        // 过滤已存在的数据
        if (input.FilterExistingData && records.Any())
        {
            // 获取实体类型-所有种数据数据类型
            var entityTypes = App.EffectiveTypes.Where(u => !u.IsInterface && !u.IsAbstract && u.IsClass && u.IsDefined(typeof(SugarTable), false) && u.FullName.EndsWith("." + input.EntityName))
                .Where(u => !u.GetCustomAttributes<IgnoreTableAttribute>().Any())
                .ToList();
            if (entityTypes.Count == 1) // 只有一个实体匹配才能过滤
            {
                // 获取实体的主键对应的属性名称
                var pkInfo = entityTypes[0].GetProperties().FirstOrDefault(u => u.GetCustomAttribute<SugarColumn>()?.IsPrimaryKey == true);
                if (pkInfo != null)
                {
                    var seedDataTypes = App.EffectiveTypes
                        .Where(u => !u.IsInterface && !u.IsAbstract && u.IsClass && u.GetInterfaces().Any(
                            i => i.HasImplementedRawGeneric(typeof(ISqlSugarEntitySeedData<>)) && i.GenericTypeArguments[0] == entityTypes[0]
                            )
                        )
                        .ToList();
                    // 可能会重名的种子数据不作为过滤项
                    string doNotFilterFullName1 = $"{input.Position}.SeedData.{input.SeedDataName}";
                    string doNotFilterFullName2 = $"{input.Position}.{input.SeedDataName}"; // Core中的命名空间没有SeedData

                    PropertyInfo idPropertySeedData = records[0].GetType().GetProperty("Id");

                    for (int i = seedDataTypes.Count - 1; i >= 0; i--)
                    {
                        string fullName = seedDataTypes[i].FullName;
                        if ((fullName == doNotFilterFullName1) || (fullName == doNotFilterFullName2)) continue;

                        // 删除重复数据
                        var instance = Activator.CreateInstance(seedDataTypes[i]);
                        var hasDataMethod = seedDataTypes[i].GetMethod("HasData");
                        var seedData = ((IEnumerable)hasDataMethod?.Invoke(instance, null))?.Cast<object>();
                        if (seedData == null) continue;

                        List<object> recordsToRemove = new();
                        foreach (var record in records)
                        {
                            object recordId = pkInfo.GetValue(record);
                            if (seedData.Select(d1 => idPropertySeedData.GetValue(d1)).Any(dataId => recordId != null && dataId != null && recordId.Equals(dataId)))
                            {
                                recordsToRemove.Add(record);
                            }
                        }
                        foreach (var itemToRemove in recordsToRemove)
                        {
                            records.Remove(itemToRemove);
                        }
                    }
                }
            }
        }

        // 检查有没有 System.Text.Json.Serialization.JsonIgnore 的属性
        // var jsonIgnoreProperties = entityType.GetProperties().Where(p => (p.GetAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() != null ||
        //     p.GetAttribute<JsonIgnoreAttribute>() != null) && p.GetAttribute<SugarColumn>() != null).ToList();
        // var jsonIgnoreInfo = new List<List<JsonIgnoredPropertyData>>();
        // if (jsonIgnoreProperties.Count > 0)
        // {
        //     int recordIndex = 0;
        //     foreach (var r in (IEnumerable)records)
        //     {
        //         List<JsonIgnoredPropertyData> record = new();
        //         foreach (var item in jsonIgnoreProperties)
        //         {
        //             object v = item.GetValue(r);
        //             string strValue = "null";
        //             if (v != null)
        //             {
        //                 strValue = v.ToString();
        //                 if (v.GetType() == typeof(string))
        //                     strValue = "\"" + strValue + "\"";
        //                 else if (v.GetType() == typeof(DateTime))
        //                     strValue = "DateTime.Parse(\"" + ((DateTime)v).ToString("yyyy-MM-dd HH:mm:ss") + "\")";
        //             }
        //             record.Add(new JsonIgnoredPropertyData { RecordIndex = recordIndex, Name = item.Name, Value = strValue });
        //         }
        //         recordIndex++;
        //         jsonIgnoreInfo.Add(record);
        //     }
        // }

        // 获取所有字段信息
        var propertyList = entityType.GetProperties().Where(x => false == (x.GetCustomAttribute<SugarColumn>()?.IsIgnore ?? false)).ToList();
        for (var i = 0; i < propertyList.Count; i++)
        {
            if (propertyList[i].Name != nameof(EntityBaseId.Id) || !(propertyList[i].GetCustomAttribute<SugarColumn>()?.IsPrimaryKey ?? true)) continue;
            var temp = propertyList[i];
            for (var j = i; j > 0; j--) propertyList[j] = propertyList[j - 1];
            propertyList[0] = temp;
        }
        // 拼接数据
        var recordList = records.Select(obj => string.Join(", ", propertyList.Select(prop =>
        {
            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            object value = prop.GetValue(obj);
            if (value == null) value = "null";
            else if (propType == typeof(string))
            {
                value = $"@\"{value}\"";
            }
            else if (propType.IsEnum)
            {
                value = $"{propType.Name}.{value}";
            }
            else if (propType == typeof(bool))
            {
                value = (bool)value ? "true" : "false";
            }
            else if (propType == typeof(DateTime))
            {
                value = $"DateTime.Parse(\"{((DateTime)value):yyyy-MM-dd HH:mm:ss.fff}\")";
            }
            return $"{prop.Name}={value}";
        }))).ToList();

        var tContent = await File.ReadAllTextAsync(templatePath);
        var data = new
        {
            NameSpace = $"{input.Position}.SeedData",
            EntityNameSpace = entityType.Namespace,
            input.TableName,
            input.EntityName,
            input.SeedDataName,
            input.ConfigId,
            tableInfo.Description,
            // JsonIgnoreInfo = jsonIgnoreInfo,
            RecordList = recordList
        };
        var tResult = await _viewEngine.RunCompileAsync(tContent, data, builderAction: builder =>
        {
            builder.AddAssemblyReferenceByName("System.Linq");
            builder.AddAssemblyReferenceByName("System.Collections");
            builder.AddUsing("System.Collections.Generic");
            builder.AddUsing("System.Linq");
        });

        var targetPath = GetSeedDataTargetPath(input);
        if (File.Exists(targetPath)) throw Oops.Oh($"种子文件 {input.SeedDataName}.cs 已存在，请先人工确认旧文件");
        await File.WriteAllTextAsync(targetPath, tResult, Encoding.UTF8);
        }
        finally
        {
            DatabaseWriteLock.Release();
        }
    }

    /// <summary>
    /// 获取库表信息
    /// </summary>
    /// <returns></returns>
    private async Task<IEnumerable<EntityInfo>> GetEntityInfos()
    {
        var entityInfos = new List<EntityInfo>();

        var type = typeof(SugarTable);
        var types = new List<Type>();
        if (_codeGenOptions.EntityAssemblyNames != null)
        {
            foreach (var asm in _codeGenOptions.EntityAssemblyNames.Select(Assembly.Load))
            {
                types.AddRange(asm.GetExportedTypes().ToList());
            }
        }

        Type[] cosType = types.Where(o => IsMyAttribute(Attribute.GetCustomAttributes(o, true))).ToArray();

        foreach (var c in cosType)
        {
            var sugarAttribute = c.GetCustomAttributes(type, true)?.FirstOrDefault();

            var des = c.GetCustomAttributes(typeof(DescriptionAttribute), true);
            var description = "";
            if (des.Length > 0)
            {
                description = ((DescriptionAttribute)des[0]).Description;
            }
            entityInfos.Add(new EntityInfo()
            {
                EntityName = c.Name,
                DbTableName = sugarAttribute == null ? c.Name : ((SugarTable)sugarAttribute).TableName,
                TableDescription = description,
                Type = c
            });
        }
        return await Task.FromResult(entityInfos);

        bool IsMyAttribute(Attribute[] o)
        {
            return o.Any(a => a.GetType() == type);
        }
    }

    /// <summary>
    /// 获取实体模板文件路径
    /// </summary>
    /// <returns></returns>
    private static string GetEntityTemplatePath()
    {
        var templatePath = Path.Combine(App.WebHostEnvironment.WebRootPath, "template");
        return Path.Combine(templatePath, "Entity.cs.vm");
    }

    /// <summary>
    /// 获取种子数据模板文件路径
    /// </summary>
    /// <returns></returns>
    private static string GetSeedDataTemplatePath()
    {
        var templatePath = Path.Combine(App.WebHostEnvironment.WebRootPath, "template");
        return Path.Combine(templatePath, "SeedData.cs.vm");
    }

    /// <summary>
    /// 设置生成实体文件路径
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private static string GetEntityTargetPath(CreateEntityInput input)
    {
        var backendPath = Path.Combine(new DirectoryInfo(App.WebHostEnvironment.ContentRootPath).Parent.FullName, input.Position, "Entity");
        if (!Directory.Exists(backendPath))
            Directory.CreateDirectory(backendPath);
        return Path.Combine(backendPath, input.EntityName + ".cs");
    }

    /// <summary>
    /// 设置生成种子数据文件路径
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private static string GetSeedDataTargetPath(CreateSeedDataInput input)
    {
        var backendPath = Path.Combine(new DirectoryInfo(App.WebHostEnvironment.ContentRootPath).Parent.FullName, input.Position, "SeedData");
        if (!Directory.Exists(backendPath))
            Directory.CreateDirectory(backendPath);
        return Path.Combine(backendPath, input.SeedDataName + ".cs");
    }

    /// <summary>
    /// 备份数据库（PostgreSQL）🔖
    /// </summary>
    /// <returns></returns>
    [HttpPost, NonUnify]
    [DisplayName("备份数据库（PostgreSQL）")]
    public async Task<IActionResult> BackupDatabase()
    {
        EnsureSuperAdmin();
        if (_db.CurrentConnectionConfig.DbType != SqlSugar.DbType.PostgreSQL)
            throw Oops.Oh("只支持 PostgreSQL 数据库 😁");

        var npgsqlConn = new NpgsqlConnectionStringBuilder(_db.CurrentConnectionConfig.ConnectionString);
        if (npgsqlConn == null || string.IsNullOrWhiteSpace(npgsqlConn.Host) || string.IsNullOrWhiteSpace(npgsqlConn.Username) || string.IsNullOrWhiteSpace(npgsqlConn.Password) || string.IsNullOrWhiteSpace(npgsqlConn.Database))
            throw Oops.Oh("PostgreSQL 数据库配置错误");

        // 确保备份目录存在
        var backupDirectory = Path.Combine(Directory.GetCurrentDirectory(), "backups");
        Directory.CreateDirectory(backupDirectory);

        // 构建备份文件名
        string backupFileName = $"backup_{DateTime.Now:yyyyMMddHHmmss}.sql";
        string backupFilePath = Path.Combine(backupDirectory, backupFileName);

        // 启动pg_dump进程进行备份
        // 设置密码：export PGPASSWORD='xxxxxx'
        var bash = $"-U {npgsqlConn.Username} -h {npgsqlConn.Host} -p {npgsqlConn.Port} -E UTF8 -F c -b -v -f {backupFilePath} {npgsqlConn.Database}";
        var startInfo = new ProcessStartInfo
        {
            FileName = "pg_dump",
            Arguments = bash,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            EnvironmentVariables =
            {
                ["PGPASSWORD"] = npgsqlConn.Password
            }
        };

        //_logger.LogInformation("备份数据库：pg_dump " + bash);

        //try
        //{
        using (var backupProcess = Process.Start(startInfo))
        {
            await backupProcess.WaitForExitAsync();

            //var output = await backupProcess.StandardOutput.ReadToEndAsync();
            //var error = await backupProcess.StandardError.ReadToEndAsync();

            // 检查备份是否成功
            if (backupProcess.ExitCode != 0)
            {
                throw Oops.Oh($"备份失败：ExitCode({backupProcess.ExitCode})");
            }
        }

        //    _logger.LogInformation($"备份成功：{backupFilePath}");
        //}
        //catch (Exception ex)
        //{
        //    _logger.LogError(ex, $"备份失败：");
        //    throw;
        //}

        // 若备份成功则提供下载链接
        return new FileStreamResult(new FileStream(backupFilePath, FileMode.Open), "application/octet-stream")
        {
            FileDownloadName = backupFileName
        };
    }

    private void EnsureSuperAdmin()
    {
        if (!_userManager.SuperAdmin)
            throw Oops.Oh("库表管理仅允许超级管理员使用");
    }

    private void EnsureDatabaseAccess(string configId)
    {
        EnsureSuperAdmin();
        if (string.IsNullOrWhiteSpace(configId) || !App.GetOptions<DbConnectionOptions>().ConnectionConfigs.Any(u => u.ConfigId.ToString() == configId))
            throw Oops.Oh("数据库标识不存在或不在系统配置白名单中");
    }

    private static void EnsureIdentifier(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || !DatabaseIdentifierRegex.IsMatch(value))
            throw Oops.Oh($"{label}格式无效，只允许字母、数字、下划线且不能以数字开头");
    }

    private void ValidateColumn(DbColumnInput input)
    {
        EnsureIdentifier(input.DbColumnName, "字段名");
        if (string.IsNullOrWhiteSpace(input.DataType) || !Regex.IsMatch(input.DataType, "^[A-Za-z0-9_ ]{1,64}$"))
            throw Oops.Oh("字段类型格式无效");
        if (input.IsIdentity == 1 && input.IsNullable == 1)
            throw Oops.Oh($"自增字段 {input.DbColumnName} 不能设置为可空");
    }

    private static void EnsureTableExists(SqlSugarScopeProvider db, string tableName)
    {
        if (!db.DbMaintenance.GetTableInfoList(false).Any(u => u.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase)))
            throw Oops.Oh($"数据表 {tableName} 不存在或已被修改");
    }

    private void EnsureCodeOutputPosition(string position)
    {
        if (_codeGenOptions.BackendApplicationNamespaces == null || !_codeGenOptions.BackendApplicationNamespaces.Contains(position, StringComparer.Ordinal))
            throw Oops.Oh("代码存放位置不在后台配置白名单中");
    }
}
