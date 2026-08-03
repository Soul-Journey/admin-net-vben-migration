// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.IO.Compression;

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统代码生成器服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 270)]
public class SysCodeGenService : IDynamicApiController, ITransient
{
    private const string MaskedConnectionString = "数据库连接已由服务端安全托管";
    private static readonly HashSet<string> SupportedGenerateTypes = new(StringComparer.Ordinal)
    {
        "100", "102", "111", "112", "121", "200", "202", "211", "212", "221"
    };
    private static readonly HashSet<string> VbenGenerateTypes = new(StringComparer.Ordinal) { "102", "112", "202", "212" };
    private static readonly HashSet<string> VbenFrontendOnlyGenerateTypes = new(StringComparer.Ordinal) { "112", "212" };
    private static readonly HashSet<string> BackendOnlyGenerateTypes = new(StringComparer.Ordinal) { "121", "221" };
    private static readonly HashSet<string> LegacyFrontendGenerateTypes = new(StringComparer.Ordinal) { "100", "111", "200", "211" };
    private static readonly Regex IdentifierRegex = new("^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.Compiled);
    private static readonly Regex PagePathRegex = new("^[A-Za-z][A-Za-z0-9/_-]{0,31}$", RegexOptions.Compiled);
    private static readonly SemaphoreSlim CodeGenWriteLock = new(1, 1);
    private readonly ISqlSugarClient _db;

    private readonly SysCodeGenConfigService _codeGenConfigService;
    private readonly DbConnectionOptions _dbConnectionOptions;
    private readonly CodeGenOptions _codeGenOptions;
    private readonly SysMenuService _sysMenuService;
    private readonly IViewEngine _viewEngine;
    private readonly UserManager _userManager;

    public SysCodeGenService(ISqlSugarClient db,
        IOptions<DbConnectionOptions> dbConnectionOptions,
        SysCodeGenConfigService codeGenConfigService,
        IOptions<CodeGenOptions> codeGenOptions,
        SysMenuService sysMenuService,
        UserManager userManager,
        IViewEngine viewEngine)
    {
        _db = db;
        _viewEngine = viewEngine;
        _userManager = userManager;
        _sysMenuService = sysMenuService;
        _codeGenOptions = codeGenOptions.Value;
        _codeGenConfigService = codeGenConfigService;
        _dbConnectionOptions = dbConnectionOptions.Value;
    }

    /// <summary>
    /// 获取代码生成分页列表 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取代码生成分页列表")]
    public async Task<SqlSugarPagedList<SysCodeGen>> Page(CodeGenInput input)
    {
        EnsureSuperAdmin();
        var result = await _db.Queryable<SysCodeGen>()
            .WhereIF(!string.IsNullOrWhiteSpace(input.TableName), u => u.TableName.Contains(input.TableName.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.BusName), u => u.BusName.Contains(input.BusName.Trim()))
            .ToPagedListAsync(input.Page, input.PageSize);
        result.Items.ForEach(MaskConnectionString);
        return result;
    }

    /// <summary>
    /// 增加代码生成 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Add"), HttpPost]
    [DisplayName("增加代码生成")]
    [UnitOfWork]
    public async Task AddCodeGen(AddCodeGenInput input)
    {
        EnsureSuperAdmin();
        await CodeGenWriteLock.WaitAsync();
        try
        {
            await ValidateCodeGenInput(input);
            var isExist = await _db.Queryable<SysCodeGen>().Where(u => u.TableName == input.TableName).AnyAsync();
            if (isExist) throw Oops.Oh(ErrorCodeEnum.D1400);

            if (input.TableUniqueList?.Count > 0) input.TableUniqueConfig = JSON.Serialize(input.TableUniqueList);
            input.ConnectionString = null;

            var codeGen = input.Adapt<SysCodeGen>();
            var dbConfig = GetDatabaseConfig(input.ConfigId);
            codeGen.DbType = dbConfig.DbType.ToString();
            codeGen.ConnectionString = null;
            var newCodeGen = await _db.Insertable(codeGen).ExecuteReturnEntityAsync();

            // 配置主表和字段表在同一事务内写入，任一步失败都会整体回滚。
            await _codeGenConfigService.AddList(GetColumnList(input), newCodeGen);
        }
        finally
        {
            CodeGenWriteLock.Release();
        }
    }

    /// <summary>
    /// 更新代码生成 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    [DisplayName("更新代码生成")]
    [UnitOfWork]
    public async Task UpdateCodeGen(UpdateCodeGenInput input)
    {
        EnsureSuperAdmin();
        await CodeGenWriteLock.WaitAsync();
        try
        {
            if (!await _db.Queryable<SysCodeGen>().AnyAsync(u => u.Id == input.Id))
                throw Oops.Oh("代码生成记录不存在或已被删除");
            await ValidateCodeGenInput(input);
            var isExist = await _db.Queryable<SysCodeGen>().AnyAsync(u => u.TableName == input.TableName && u.Id != input.Id);
            if (isExist) throw Oops.Oh(ErrorCodeEnum.D1400);

            input.TableUniqueConfig = input.TableUniqueList?.Count > 0 ? JSON.Serialize(input.TableUniqueList) : null;
            input.ConnectionString = null;
            var codeGen = input.Adapt<SysCodeGen>();
            codeGen.DbType = GetDatabaseConfig(input.ConfigId).DbType.ToString();
            codeGen.ConnectionString = null;
            await _db.Updateable(codeGen).ExecuteCommandAsync();

            // 字段重建和主表更新共用事务，避免只删不增。
            await _codeGenConfigService.DeleteCodeGenConfig(codeGen.Id);
            await _codeGenConfigService.AddList(GetColumnList(input.Adapt<AddCodeGenInput>()), codeGen);
        }
        finally
        {
            CodeGenWriteLock.Release();
        }
    }

    /// <summary>
    /// 删除代码生成 🔖
    /// </summary>
    /// <param name="inputs"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Delete"), HttpPost]
    [DisplayName("删除代码生成")]
    [UnitOfWork]
    public async Task DeleteCodeGen(List<DeleteCodeGenInput> inputs)
    {
        EnsureSuperAdmin();
        if (inputs == null || inputs.Count < 1) return;
        var ids = inputs.Select(u => u.Id).Where(u => u > 0).Distinct().ToList();
        if (ids.Count != inputs.Count || ids.Count > 100) throw Oops.Oh("删除参数包含重复项或数量超限");

        await CodeGenWriteLock.WaitAsync();
        try
        {
            await _db.Deleteable<SysCodeGenConfig>().Where(u => ids.Contains(u.CodeGenId)).ExecuteCommandAsync();
            await _db.Deleteable<SysCodeGen>().Where(u => ids.Contains(u.Id)).ExecuteCommandAsync();
        }
        finally
        {
            CodeGenWriteLock.Release();
        }
    }

    /// <summary>
    /// 获取代码生成详情 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取代码生成详情")]
    public async Task<SysCodeGen> GetDetail([FromQuery] QueryCodeGenInput input)
    {
        EnsureSuperAdmin();
        var result = await GetTrustedCodeGen(input.Id);
        MaskConnectionString(result);
        return result;
    }

    /// <summary>
    /// 获取数据库库集合 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取数据库库集合")]
    public async Task<List<DatabaseOutput>> GetDatabaseList()
    {
        EnsureSuperAdmin();
        var result = _dbConnectionOptions.ConnectionConfigs.Select(u => new DatabaseOutput
        {
            ConfigId = u.ConfigId.ToString(),
            DbType = u.DbType,
            ConnectionString = MaskedConnectionString
        }).ToList();
        return await Task.FromResult(result);
    }

    /// <summary>
    /// 获取数据库表(实体)集合 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取数据库表(实体)集合")]
    public async Task<List<TableOutput>> GetTableList(string configId = SqlSugarConst.MainConfigId)
    {
        EnsureSuperAdmin();
        GetDatabaseConfig(configId);
        var provider = _db.AsTenant().GetConnectionScope(configId);
        var dbTableInfos = provider.DbMaintenance.GetTableInfoList(false); // 不能走缓存,否则切库不起作用
        var config = _dbConnectionOptions.ConnectionConfigs.FirstOrDefault(u => configId.Equals(u.ConfigId));

        // var dbTableNames = dbTableInfos.Select(u => u.Name.ToLower()).ToList();
        IEnumerable<EntityInfo> entityInfos = await GetEntityInfos(configId);

        var tableOutputList = new List<TableOutput>();
        foreach (var item in entityInfos)
        {
            var tbConfigId = item.Type.GetCustomAttribute<TenantAttribute>()?.configId as string ?? SqlSugarConst.MainConfigId;
            if (item.Type.IsDefined(typeof(LogTableAttribute))) tbConfigId = SqlSugarConst.LogConfigId;
            if (tbConfigId != configId) continue;

            var table = dbTableInfos.FirstOrDefault(u => string.Equals(u.Name, (config!.DbSettings.EnableUnderLine ? UtilMethods.ToUnderLine(item.DbTableName) : item.DbTableName), StringComparison.CurrentCultureIgnoreCase));
            if (table == null) continue;
            tableOutputList.Add(new TableOutput
            {
                ConfigId = configId,
                EntityName = item.EntityName,
                TableName = table.Name,
                TableComment = item.TableDescription
            });
        }
        return tableOutputList;
    }

    /// <summary>
    /// 根据表名获取列集合 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("根据表名获取列集合")]
    public List<ColumnOuput> GetColumnListByTableName([Required] string tableName, string configId = SqlSugarConst.MainConfigId)
    {
        EnsureSuperAdmin();
        if (!IdentifierRegex.IsMatch(tableName)) throw Oops.Oh("实体名称格式无效");
        GetDatabaseConfig(configId);
        // 切库---多库代码生成用
        var provider = _db.AsTenant().GetConnectionScope(configId);
        var config = _dbConnectionOptions.ConnectionConfigs.FirstOrDefault(u => u.ConfigId.ToString() == configId) ?? throw Oops.Oh(ErrorCodeEnum.D1401);
        if (config.DbSettings.EnableUnderLine) tableName = UtilMethods.ToUnderLine(tableName);
        // 获取实体类型属性
        var entityType = provider.DbMaintenance.GetTableInfoList(false).FirstOrDefault(u => u.Name == tableName);
        if (entityType == null) return null;
        var entityBasePropertyNames = _codeGenOptions.EntityBaseColumn[nameof(EntityTenant)];
        var properties = GetEntityInfos(configId).Result.First(e => e.DbTableName == tableName).Type.GetProperties()
            .Where(e => e.GetCustomAttribute<SugarColumn>()?.IsIgnore == false).Select(e => new
            {
                PropertyName = e.Name,
                ColumnComment = e.GetCustomAttribute<SugarColumn>()?.ColumnDescription,
                ColumnName = e.GetCustomAttribute<SugarColumn>()?.ColumnName ?? e.Name
            }).ToList();
        // 按原始类型的顺序获取所有实体类型属性（不包含导航属性，会返回null）
        var columnList = provider.DbMaintenance.GetColumnInfosByTableName(tableName).Select(u => new ColumnOuput
        {
            ColumnName = config!.DbSettings.EnableUnderLine ? UtilMethods.ToUnderLine(u.DbColumnName) : u.DbColumnName,
            ColumnKey = u.IsPrimarykey.ToString(),
            DataType = u.DataType.ToString(),
            NetType = CodeGenUtil.ConvertDataType(u, provider.CurrentConnectionConfig.DbType),
            ColumnComment = u.ColumnDescription
        }).ToList();
        foreach (var column in columnList)
        {
            var property = properties.First(e => (config!.DbSettings.EnableUnderLine ? UtilMethods.ToUnderLine(e.ColumnName) : e.ColumnName) == column.ColumnName);
            column.ColumnComment ??= property?.ColumnComment;
            column.PropertyName = property?.PropertyName;
        }
        return columnList;
    }

    /// <summary>
    /// 获取数据表列（实体属性）集合
    /// </summary>
    /// <returns></returns>
    private List<ColumnOuput> GetColumnList([FromQuery] AddCodeGenInput input)
    {
        var entityType = GetEntityInfos(input.ConfigId).GetAwaiter().GetResult().FirstOrDefault(u => u.EntityName == input.TableName);
        if (entityType == null) return null;

        var config = _dbConnectionOptions.ConnectionConfigs.FirstOrDefault(u => u.ConfigId.ToString() == input.ConfigId);
        var dbTableName = config!.DbSettings.EnableUnderLine ? UtilMethods.ToUnderLine(entityType.DbTableName) : entityType.DbTableName;

        // 切库---多库代码生成用
        var provider = _db.AsTenant().GetConnectionScope(!string.IsNullOrEmpty(input.ConfigId) ? input.ConfigId : SqlSugarConst.MainConfigId);

        var entityBasePropertyNames = _codeGenOptions.EntityBaseColumn[nameof(EntityTenant)];
        var columnInfos = provider.DbMaintenance.GetColumnInfosByTableName(dbTableName, false);
        var result = columnInfos.Select(u => new ColumnOuput
        {
            // 转下划线后的列名需要再转回来（暂时不转）
            //ColumnName = config.DbSettings.EnableUnderLine ? CodeGenUtil.CamelColumnName(u.DbColumnName, entityBasePropertyNames) : u.DbColumnName,
            ColumnName = u.DbColumnName,
            ColumnLength = u.Length,
            IsPrimarykey = u.IsPrimarykey,
            IsNullable = u.IsNullable,
            ColumnKey = u.IsPrimarykey.ToString(),
            NetType = CodeGenUtil.ConvertDataType(u, provider.CurrentConnectionConfig.DbType),
            DataType = CodeGenUtil.ConvertDataType(u, provider.CurrentConnectionConfig.DbType),
            ColumnComment = string.IsNullOrWhiteSpace(u.ColumnDescription) ? u.DbColumnName : u.ColumnDescription
        }).ToList();

        // 获取实体的属性信息，赋值给PropertyName属性(CodeFirst模式应以PropertyName为实际使用名称)
        var entityProperties = entityType.Type.GetProperties();

        for (int i = result.Count - 1; i >= 0; i--)
        {
            var columnOutput = result[i];
            // 先找自定义字段名的，如果找不到就再找自动生成字段名的(并且过滤掉没有SugarColumn的属性)
            var propertyInfo = entityProperties.FirstOrDefault(u => string.Equals((u.GetCustomAttribute<SugarColumn>()?.ColumnName ?? ""), columnOutput.ColumnName, StringComparison.CurrentCultureIgnoreCase)) ??
                entityProperties.FirstOrDefault(u => u.GetCustomAttribute<SugarColumn>() != null && u.Name.ToLower() == (config.DbSettings.EnableUnderLine
                ? CodeGenUtil.CamelColumnName(columnOutput.ColumnName, entityBasePropertyNames).ToLower()
                : columnOutput.ColumnName.ToLower()));
            if (propertyInfo != null)
            {
                columnOutput.PropertyName = propertyInfo.Name;
                columnOutput.ColumnComment = propertyInfo.GetCustomAttribute<SugarColumn>()!.ColumnDescription;
                var propertyType = Nullable.GetUnderlyingType(propertyInfo.PropertyType);
                if (propertyInfo.PropertyType.IsEnum || (propertyType?.IsEnum ?? false))
                {
                    columnOutput.DictTypeCode = (propertyType ?? propertyInfo.PropertyType).Name;
                }
                else
                {
                    var dict = propertyInfo.GetCustomAttribute<DictAttribute>();
                    if (dict != null) columnOutput.DictTypeCode = dict.DictTypeCode;
                }
            }
            else
            {
                result.RemoveAt(i); // 移除没有定义此属性的字段
            }
        }
        return result;
    }

    /// <summary>
    /// 获取库表信息
    /// </summary>
    /// <returns></returns>
    private async Task<IEnumerable<EntityInfo>> GetEntityInfos(string configId)
    {
        var config = _dbConnectionOptions.ConnectionConfigs.FirstOrDefault(u => u.ConfigId.ToString() == configId) ?? throw Oops.Oh(ErrorCodeEnum.D1401);
        var entityInfos = new List<EntityInfo>();

        var type = typeof(SugarTable);
        var types = new List<Type>();
        if (_codeGenOptions.EntityAssemblyNames != null)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var assemblyName = assembly.GetName().Name;
                if (!_codeGenOptions.EntityAssemblyNames.Contains(assemblyName) && !_codeGenOptions.EntityAssemblyNames.Any(name => assemblyName!.Contains(name))) continue;

                Assembly asm = Assembly.Load(assemblyName!);
                types.AddRange(asm.GetExportedTypes().ToList());
            }
        }

        Type[] cosType = types.Where(o => IsMyAttribute(Attribute.GetCustomAttributes(o, true))).ToArray();

        foreach (var ct in cosType)
        {
            var sugarAttribute = ct.GetCustomAttributes(type, true).FirstOrDefault();

            var description = "";
            var des = ct.GetCustomAttributes(typeof(DescriptionAttribute), true);
            if (des.Length > 0) description = ((DescriptionAttribute)des[0]).Description;

            var dbTableName = sugarAttribute == null || string.IsNullOrWhiteSpace(((SugarTable)sugarAttribute).TableName) ? ct.Name : ((SugarTable)sugarAttribute).TableName;
            if (config.DbSettings.EnableUnderLine) dbTableName = UtilMethods.ToUnderLine(dbTableName);

            entityInfos.Add(new EntityInfo
            {
                EntityName = ct.Name,
                DbTableName = dbTableName,
                TableDescription = sugarAttribute == null ? description : ((SugarTable)sugarAttribute).TableDescription,
                Type = ct
            });
        }
        return await Task.FromResult(entityInfos);

        bool IsMyAttribute(Attribute[] o) => o.Any(a => a.GetType() == type);
    }

    /// <summary>
    /// 获取程序保存位置 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取程序保存位置")]
    public List<string> GetApplicationNamespaces()
    {
        EnsureSuperAdmin();
        return _codeGenOptions.BackendApplicationNamespaces?.ToList() ?? new();
    }

    /// <summary>
    /// 获取当前项目允许使用的代码生成方式
    /// </summary>
    [DisplayName("获取代码生成方式")]
    public List<CodeGenGenerateTypeOutput> GetGenerateTypeList()
    {
        EnsureSuperAdmin();
        return new()
        {
            new() { Value = "102", Label = "下载 ZIP（Vben 前端 + .NET 后端）", IncludesFrontend = true, IncludesBackend = true },
            new() { Value = "112", Label = "下载 ZIP（仅 Vben 前端）", IncludesFrontend = true },
            new() { Value = "121", Label = "下载 ZIP（仅 .NET 后端）", IncludesBackend = true },
            new() { Value = "202", Label = "写入项目（Vben 前端 + .NET 后端）", IncludesFrontend = true, IncludesBackend = true, WritesSource = true },
            new() { Value = "212", Label = "写入项目（仅 Vben 前端）", IncludesFrontend = true, WritesSource = true },
            new() { Value = "221", Label = "写入项目（仅 .NET 后端）", IncludesBackend = true, WritesSource = true }
        };
    }

    /// <summary>
    /// 从数据库实体重新同步字段配置
    /// </summary>
    [ApiDescriptionSettings(Name = "Sync"), HttpPost]
    [DisplayName("同步代码生成字段配置")]
    [UnitOfWork]
    public async Task SyncCodeGen(QueryCodeGenInput input)
    {
        EnsureSuperAdmin();
        await CodeGenWriteLock.WaitAsync();
        try
        {
            var codeGen = await GetTrustedCodeGen(input.Id);
            await ValidateStoredCodeGen(codeGen);
            var columns = GetColumnList(codeGen.Adapt<AddCodeGenInput>());
            if (columns == null || columns.Count == 0) throw Oops.Oh("未读取到实体字段，原配置保持不变");

            await _codeGenConfigService.DeleteCodeGenConfig(codeGen.Id);
            await _codeGenConfigService.AddList(columns, codeGen);
        }
        finally
        {
            CodeGenWriteLock.Release();
        }
    }

    /// <summary>
    /// 代码生成到本地 🔖
    /// </summary>
    /// <returns></returns>
    [UnitOfWork]
    [DisplayName("代码生成到本地")]
    public async Task<dynamic> RunLocal(QueryCodeGenInput request)
    {
        EnsureSuperAdmin();
        await CodeGenWriteLock.WaitAsync();
        try
        {
            var input = await GetTrustedCodeGen(request.Id);
            await ValidateStoredCodeGen(input);
            EnsureGenerationModeIsSafe(input);

            List<string> targetPathList;
            var zipRoot = Path.Combine(App.WebHostEnvironment.WebRootPath, "CodeGen");
            var zipPath = EnsurePathWithin(zipRoot, Path.Combine(zipRoot, input.TableName!));
            if (input.GenerateType!.StartsWith('1'))
            {
                targetPathList = GetZipPathList(input);
                targetPathList = targetPathList.Select(path => EnsurePathWithin(zipPath, path)).ToList();
                if (Directory.Exists(zipPath)) Directory.Delete(zipPath, true);
            }
            else
            {
                targetPathList = GetTargetPathList(input);
                var allowedRoots = GetAllowedLocalRoots(input);
                targetPathList = targetPathList.Select(path => EnsurePathWithinAny(allowedRoots, path)).ToList();
                var existingFiles = targetPathList.Where(File.Exists).Select(path => Path.GetRelativePath(GetRepositoryRoot(), path)).ToList();
                if (existingFiles.Count > 0)
                    throw Oops.Oh($"目标目录已存在文件：{string.Join("、", existingFiles)}。为防止覆盖源码，本次未写入任何文件");
            }

            var (_, result) = await RenderTemplateAsync(input);
            var templatePathList = GetTemplatePathList(input);
            var generatedFiles = templatePathList
                .Select((templatePath, index) => (
                    TargetPath: targetPathList[index],
                    Content: result.GetValueOrDefault(Path.GetFileNameWithoutExtension(templatePath))))
                .Where(file => !string.IsNullOrWhiteSpace(file.Content))
                .Select(file => (file.TargetPath, file.Content!))
                .ToList();

            if (!input.GenerateType.StartsWith('1'))
            {
                await WriteGeneratedFilesAtomicallyAsync(generatedFiles);
                return null;
            }

            var downloadPath = EnsurePathWithin(zipRoot, zipPath + ".zip");
            var temporaryDownloadPath = EnsurePathWithin(
                zipRoot,
                downloadPath + $".{Guid.NewGuid():N}.tmp");
            try
            {
                await WriteGeneratedFilesAsync(generatedFiles);
                ZipFile.CreateFromDirectory(zipPath, temporaryDownloadPath);
                File.Move(temporaryDownloadPath, downloadPath, true);
                return new { url = $"{App.HttpContext.Request.Scheme}://{App.HttpContext.Request.Host.Value}/codeGen/{input.TableName}.zip" };
            }
            finally
            {
                if (File.Exists(temporaryDownloadPath)) File.Delete(temporaryDownloadPath);
                if (Directory.Exists(zipPath)) Directory.Delete(zipPath, true);
            }
        }
        finally
        {
            CodeGenWriteLock.Release();
        }
    }

    private static async Task WriteGeneratedFilesAsync(IReadOnlyList<(string TargetPath, string Content)> files)
    {
        foreach (var file in files)
        {
            var directoryPath = new DirectoryInfo(file.TargetPath).Parent!.FullName;
            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
            await File.WriteAllTextAsync(file.TargetPath, file.Content, Encoding.UTF8);
        }
    }

    private static async Task WriteGeneratedFilesAtomicallyAsync(IReadOnlyList<(string TargetPath, string Content)> files)
    {
        var pendingFiles = new List<(string TemporaryPath, string TargetPath)>();
        var committedFiles = new List<string>();
        var createdDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var file in files)
            {
                var directoryPath = new DirectoryInfo(file.TargetPath).Parent!.FullName;
                var currentDirectory = directoryPath;
                while (!Directory.Exists(currentDirectory))
                {
                    createdDirectories.Add(currentDirectory);
                    currentDirectory = new DirectoryInfo(currentDirectory).Parent?.FullName
                        ?? throw Oops.Oh("无法确定代码生成目标目录");
                }

                Directory.CreateDirectory(directoryPath);
                var temporaryPath = file.TargetPath + $".codegen-{Guid.NewGuid():N}.tmp";
                await File.WriteAllTextAsync(temporaryPath, file.Content, Encoding.UTF8);
                pendingFiles.Add((temporaryPath, file.TargetPath));
            }

            foreach (var file in pendingFiles)
            {
                File.Move(file.TemporaryPath, file.TargetPath, false);
                committedFiles.Add(file.TargetPath);
            }
        }
        catch
        {
            foreach (var file in pendingFiles.Where(file => File.Exists(file.TemporaryPath)))
                File.Delete(file.TemporaryPath);
            foreach (var file in committedFiles.Where(File.Exists)) File.Delete(file);
            foreach (var directory in createdDirectories.OrderByDescending(path => path.Length))
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }

            throw;
        }
    }

    /// <summary>
    /// 获取代码生成预览 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取代码生成预览")]
    public async Task<Dictionary<string, string>> Preview(QueryCodeGenInput request)
    {
        EnsureSuperAdmin();
        var input = await GetTrustedCodeGen(request.Id);
        await ValidateStoredCodeGen(input);
        var (_, result) = await RenderTemplateAsync(input);
        return result;
    }

    /// <summary>
    /// 渲染模板
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private async Task<(List<CodeGenConfig> tableFieldList, Dictionary<string, string> result)> RenderTemplateAsync(SysCodeGen input)
    {
        var tableFieldList = await _codeGenConfigService.GetList(new CodeGenConfig { CodeGenId = input.Id }); // 字段集合
        var joinTableList = tableFieldList.Where(u => u.EffectType is "Upload" or "ForeignKey" or "ApiTreeSelector").ToList(); // 需要连表查询的字段

        var data = new CustomViewEngine
        {
            ConfigId = input.ConfigId,
            BusName = input.BusName,
            PagePath = input.PagePath,
            NameSpace = input.NameSpace,
            ClassName = input.TableName,
            PrintType = input.PrintType,
            PrintName = input.PrintName,
            AuthorName = input.AuthorName,
            ProjectLastName = input.NameSpace!.Split('.').Last(),
            LowerClassName = input.TableName!.ToFirstLetterLowerCase(),
            TableUniqueConfigList = input.TableUniqueList ?? new(),

            TableField = tableFieldList,
            QueryWhetherList = tableFieldList.Where(u => u.WhetherQuery == "Y").ToList(),
            ImportFieldList = tableFieldList.Where(u => u.WhetherImport == "Y").ToList(),
            UploadFieldList = tableFieldList.Where(u => u.EffectType == "Upload").ToList(),
            PrimaryKeyFieldList = tableFieldList.Where(c => c.ColumnKey == "True").ToList(),
            AddUpdateFieldList = tableFieldList.Where(u => u.WhetherAddUpdate == "Y").ToList(),
            ApiTreeFieldList = tableFieldList.Where(u => u.EffectType == "ApiTreeSelector").ToList(),
            DropdownFieldList = tableFieldList.Where(u => u.EffectType is "ForeignKey" or "ApiTreeSelector").ToList(),

            HasJoinTable = joinTableList.Count > 0,
            HasDictField = tableFieldList.Any(u => u.EffectType == "DictSelector"),
            HasEnumField = tableFieldList.Any(u => u.EffectType == "EnumSelector"),
            HasConstField = tableFieldList.Any(u => u.EffectType == "ConstSelector"),
            HasLikeQuery = tableFieldList.Any(c => c.WhetherQuery == "Y" && c.QueryType == "like")
        };

        // 获取模板文件并替换
        var templatePathList = GetTemplatePathList(input);
        var templatePath = Path.Combine(App.WebHostEnvironment.WebRootPath, "template");

        var result = new Dictionary<string, string>();
        foreach (var path in templatePathList)
        {
            var templateFilePath = Path.Combine(templatePath, path);
            if (!File.Exists(templateFilePath)) continue;
            var tContent = await File.ReadAllTextAsync(templateFilePath);
            var tResult = await _viewEngine.RunCompileFromCachedAsync(tContent, data, builderAction: builder =>
            {
                builder.AddAssemblyReferenceByName("System.Text.RegularExpressions");
                builder.AddAssemblyReferenceByName("System.Collections");
                builder.AddAssemblyReferenceByName("System.Linq");

                builder.AddUsing("System.Text.RegularExpressions");
                builder.AddUsing("System.Collections.Generic");
                builder.AddUsing("System.Linq");
            });
            result.Add(Path.GetFileNameWithoutExtension(path), tResult);
        }
        return (tableFieldList, result);
    }

    private void EnsureSuperAdmin()
    {
        if (!_userManager.SuperAdmin)
            throw Oops.Oh("代码生成仅允许超级管理员使用");
    }

    private DbConnectionConfig GetDatabaseConfig(string configId)
    {
        if (string.IsNullOrWhiteSpace(configId)) throw Oops.Oh("库定位器不能为空");
        return _dbConnectionOptions.ConnectionConfigs.FirstOrDefault(u => u.ConfigId.ToString() == configId)
            ?? throw Oops.Oh("库定位器不存在或不在服务端配置白名单中");
    }

    private async Task<SysCodeGen> GetTrustedCodeGen(long id)
    {
        if (id <= 0) throw Oops.Oh("代码生成记录不能为空");
        return await _db.Queryable<SysCodeGen>().FirstAsync(u => u.Id == id)
            ?? throw Oops.Oh("代码生成记录不存在或已被删除");
    }

    private async Task ValidateStoredCodeGen(SysCodeGen input)
    {
        var validateInput = input.Adapt<AddCodeGenInput>();
        validateInput.TableUniqueList = input.TableUniqueList;
        await ValidateCodeGenInput(validateInput);
    }

    private async Task ValidateCodeGenInput(AddCodeGenInput input)
    {
        if (!IdentifierRegex.IsMatch(input.TableName ?? "")) throw Oops.Oh("实体名称格式无效");
        if (!PagePathRegex.IsMatch(input.PagePath ?? "")) throw Oops.Oh("前端目录格式无效");
        if (!SupportedGenerateTypes.Contains(input.GenerateType ?? "")) throw Oops.Oh("生成方式无效");
        if (_codeGenOptions.BackendApplicationNamespaces == null ||
            !_codeGenOptions.BackendApplicationNamespaces.Contains(input.NameSpace, StringComparer.Ordinal))
            throw Oops.Oh("后端命名空间不在服务端配置白名单中");

        EnsureSafeText(input.BusName, "业务名称", 128);
        EnsureSafeText(input.AuthorName, "作者", 32);
        var config = GetDatabaseConfig(input.ConfigId);
        var entityInfo = (await GetEntityInfos(input.ConfigId)).FirstOrDefault(u => u.EntityName == input.TableName)
            ?? throw Oops.Oh("所选实体不存在或未被代码生成配置收录");
        var provider = _db.AsTenant().GetConnectionScope(input.ConfigId);
        var physicalTableName = config.DbSettings.EnableUnderLine ? UtilMethods.ToUnderLine(entityInfo.DbTableName) : entityInfo.DbTableName;
        if (!provider.DbMaintenance.GetTableInfoList(false).Any(u => u.Name.Equals(physicalTableName, StringComparison.OrdinalIgnoreCase)))
            throw Oops.Oh("实体对应的数据表不存在，请先检查库表结构");

        var uniqueList = input.TableUniqueList ?? new();
        if (uniqueList.Count > 8) throw Oops.Oh("唯一约束配置最多 8 组");
        var entityProperties = entityInfo.Type.GetProperties().Select(u => u.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var unique in uniqueList)
        {
            if (unique.Columns == null || unique.Columns.Count == 0 || unique.Columns.Count > 8 ||
                unique.Columns.Distinct(StringComparer.Ordinal).Count() != unique.Columns.Count ||
                unique.Columns.Any(u => !entityProperties.Contains(u)))
                throw Oops.Oh("唯一约束包含空字段、重复字段或非实体字段");
            EnsureSafeText(unique.Message, "唯一约束提示", 128);
        }
    }

    private static void EnsureSafeText(string value, string label, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength ||
            value.IndexOfAny(new[] { '\"', '\'', '\\', '\r', '\n', '{', '}' }) >= 0)
            throw Oops.Oh($"{label}不能为空、不能超长，且不能包含引号、换行、反斜杠或花括号");
    }

    private static void EnsureGenerationModeIsSafe(SysCodeGen input)
    {
        if (!SupportedGenerateTypes.Contains(input.GenerateType ?? "")) throw Oops.Oh("生成方式无效");
        if (LegacyFrontendGenerateTypes.Contains(input.GenerateType!))
            throw Oops.Oh("该方式使用旧 Element Plus 模板，旧版 Web 已永久设为只读。请选择明确标注 Vben 的生成方式");
    }

    private static string EnsurePathWithin(string rootPath, string targetPath)
    {
        var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var target = Path.GetFullPath(targetPath);
        if (!target.Equals(root, StringComparison.OrdinalIgnoreCase) &&
            !target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw Oops.Oh("生成目标路径超出允许目录");
        return target;
    }

    private static string EnsurePathWithinAny(IEnumerable<string> rootPaths, string targetPath)
    {
        foreach (var rootPath in rootPaths)
        {
            try
            {
                return EnsurePathWithin(rootPath, targetPath);
            }
            catch
            {
                // 继续检查下一个受控根目录。
            }
        }
        throw Oops.Oh("生成目标路径超出后端项目和 Vben 项目的允许目录");
    }

    private static void MaskConnectionString(SysCodeGen item)
    {
        item.ConnectionString = MaskedConnectionString;
    }

    /// <summary>
    /// 增加菜单
    /// </summary>
    /// <param name="className"></param>
    /// <param name="busName"></param>
    /// <param name="pid"></param>
    /// <param name="menuIcon"></param>
    /// <param name="pagePath"></param>
    /// <param name="tableFieldList"></param>
    /// <returns></returns>
    private async Task AddMenu(string className, string busName, long pid, string menuIcon, string pagePath, List<CodeGenConfig> tableFieldList)
    {
        // 删除已存在的菜单
        var title = $"{busName}管理";
        await DeleteMenuTree(title, pid == 0 ? MenuTypeEnum.Dir : MenuTypeEnum.Menu);

        var parentMenuPath = "";
        var lowerClassName = className!.ToFirstLetterLowerCase();
        if (pid == 0)
        {
            // 新增目录，并记录Id
            var dirMenu = new SysMenu { Pid = 0, Title = title, Type = MenuTypeEnum.Dir, Icon = "robot", Path = "/" + className.ToLower(), Component = "Layout" };
            pid = await _sysMenuService.AddMenu(dirMenu.Adapt<AddMenuInput>());
        }
        else
        {
            var parentMenu = await _db.Queryable<SysMenu>().FirstAsync(u => u.Id == pid) ?? throw Oops.Oh(ErrorCodeEnum.D1505);
            parentMenuPath = parentMenu.Path;
        }

        // 新增菜单，并记录Id
        var rootMenu = new SysMenu { Pid = pid, Title = title, Type = MenuTypeEnum.Menu, Icon = menuIcon, Path = $"{parentMenuPath}/{className.ToLower()}", Component = $"/{pagePath}/{lowerClassName}/index" };
        pid = await _sysMenuService.AddMenu(rootMenu.Adapt<AddMenuInput>());

        var orderNo = 100;
        var menuList = new List<SysMenu>
        {
            new() { Title="查询", Permission=$"{lowerClassName}:page", Pid=pid, Type=MenuTypeEnum.Btn, OrderNo=orderNo+=10},
            new() { Title="详情", Permission=$"{lowerClassName}:detail", Pid=pid, Type=MenuTypeEnum.Btn, OrderNo=orderNo+=10},
            new() { Title="增加", Permission=$"{lowerClassName}:add", Pid=pid, Type=MenuTypeEnum.Btn, OrderNo=orderNo+=10},
            new() { Title="编辑", Permission=$"{lowerClassName}:update", Pid=pid, Type=MenuTypeEnum.Btn, OrderNo=orderNo+=10},
            new() { Title="删除", Permission=$"{lowerClassName}:delete", Pid=pid, Type=MenuTypeEnum.Btn, OrderNo=orderNo+=10},
            new() { Title="批量删除", Permission=$"{lowerClassName}:batchDelete", Pid=pid, Type=MenuTypeEnum.Btn, OrderNo=orderNo+=10},
            new() { Title="设置状态", Permission=$"{lowerClassName}:setStatus", Pid=pid, Type=MenuTypeEnum.Btn, OrderNo=orderNo+=10},
            new() { Title="打印", Permission=$"{lowerClassName}:print", Pid=pid, Type=MenuTypeEnum.Btn, OrderNo=orderNo+=10},
            new() { Title="导入", Permission=$"{lowerClassName}:import", Pid=pid, Type=MenuTypeEnum.Btn, OrderNo=orderNo+=10},
            new() { Title="导出", Permission=$"{lowerClassName}:export", Pid=pid, Type=MenuTypeEnum.Btn, OrderNo=orderNo+=10}
        };

        if (tableFieldList.Any(u => u.EffectType is "ForeignKey" or "ApiTreeSelector" && (u.WhetherAddUpdate == "Y" || u.WhetherQuery == "Y")))
            menuList.Add(new SysMenu { Title = "下拉列表数据", Permission = $"{lowerClassName}:dropdownData", Pid = pid, Type = MenuTypeEnum.Btn, OrderNo = orderNo += 10 });

        foreach (var column in tableFieldList.Where(u => u.EffectType == "Upload"))
            menuList.Add(new SysMenu { Title = $"上传{column.ColumnComment}", Permission = $"{lowerClassName}:upload{column.PropertyName}", Pid = pid, Type = MenuTypeEnum.Btn, OrderNo = orderNo += 10 });

        foreach (var menu in menuList) await _sysMenuService.AddMenu(menu.Adapt<AddMenuInput>());
    }

    /// <summary>
    /// 根据菜单名称和类型删除关联的菜单树
    /// </summary>
    /// <param name="title"></param>
    /// <param name="type"></param>
    private async Task DeleteMenuTree(string title, MenuTypeEnum type)
    {
        var menuList = await _db.Queryable<SysMenu>().Where(u => u.Title == title && u.Type == type).ToListAsync() ?? new();
        foreach (var menu in menuList) await _sysMenuService.DeleteMenu(new DeleteMenuInput { Id = menu.Id });
    }

    /// <summary>
    /// 获取模板文件路径集合
    /// </summary>
    /// <returns></returns>
    private static List<string> GetTemplatePathList(SysCodeGen input)
    {
        if (VbenFrontendOnlyGenerateTypes.Contains(input.GenerateType!)) return new() { "vben-index.vue.vm", "vben-api.ts.vm" };
        if (VbenGenerateTypes.Contains(input.GenerateType!))
            return new() { "Service.cs.vm", "Input.cs.vm", "Output.cs.vm", "Dto.cs.vm", "vben-index.vue.vm", "vben-api.ts.vm" };
        if (BackendOnlyGenerateTypes.Contains(input.GenerateType!)) return new() { "Service.cs.vm", "Input.cs.vm", "Output.cs.vm", "Dto.cs.vm" };
        if (input.GenerateType!.Substring(1, 1).Contains('1')) return new() { "index.vue.vm", "editDialog.vue.vm", "api.ts.vm" };
        if (input.GenerateType.Substring(1, 1).Contains('2')) return new() { "Service.cs.vm", "Input.cs.vm", "Output.cs.vm", "Dto.cs.vm" };
        return new() { "Service.cs.vm", "Input.cs.vm", "Output.cs.vm", "Dto.cs.vm", "index.vue.vm", "editDialog.vue.vm", "api.ts.vm" };
    }

    /// <summary>
    /// 设置生成文件路径
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private List<string> GetTargetPathList(SysCodeGen input)
    {
        var backendPath = Path.Combine(GetBackendRoot(input), "Service", input.TableName!);
        var servicePath = Path.Combine(backendPath, input.TableName + "Service.cs");
        var inputPath = Path.Combine(backendPath, "Dto", input.TableName + "Input.cs");
        var outputPath = Path.Combine(backendPath, "Dto", input.TableName + "Output.cs");
        var viewPath = Path.Combine(backendPath, "Dto", input.TableName + "Dto.cs");
        var firstLowerTableName = input.TableName!.ToFirstLetterLowerCase();

        if (VbenGenerateTypes.Contains(input.GenerateType!))
        {
            var vbenRoot = GetVbenRoot();
            var vbenIndexPath = Path.Combine(vbenRoot, "src", "views", input.PagePath!, firstLowerTableName, "index.vue");
            var vbenApiPath = Path.Combine(vbenRoot, "src", "api", input.PagePath!, firstLowerTableName + ".ts");
            if (VbenFrontendOnlyGenerateTypes.Contains(input.GenerateType!)) return new() { vbenIndexPath, vbenApiPath };
            return new() { servicePath, inputPath, outputPath, viewPath, vbenIndexPath, vbenApiPath };
        }

        var legacyRoot = Path.Combine(GetRepositoryRoot(), _codeGenOptions.FrontRootPath);
        var frontendPath = Path.Combine(legacyRoot, "src", "views", input.PagePath!);
        var indexPath = Path.Combine(frontendPath, firstLowerTableName, "index.vue");
        var formModalPath = Path.Combine(frontendPath, firstLowerTableName, "component", "editDialog.vue");
        var apiJsPath = Path.Combine(legacyRoot, "src", "api", input.PagePath, firstLowerTableName + ".ts");

        if (input.GenerateType!.Substring(1, 1).Contains('1'))
        {
            // 生成到本项目(前端)
            return new List<string>
            {
                indexPath,
                formModalPath,
                apiJsPath
            };
        }

        if (input.GenerateType.Substring(1, 1).Contains('2'))
        {
            // 生成到本项目(后端)
            return new List<string>
            {
                servicePath,
                inputPath,
                outputPath,
                viewPath,
            };
        }
        // 前后端同时生成到本项目
        return new List<string>
        {
            servicePath,
            inputPath,
            outputPath,
            viewPath,
            indexPath,
            formModalPath,
            apiJsPath
        };
    }

    /// <summary>
    /// 设置生成文件路径
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private List<string> GetZipPathList(SysCodeGen input)
    {
        var zipPath = Path.Combine(App.WebHostEnvironment.WebRootPath, "CodeGen", input.TableName!);

        var firstLowerTableName = input.TableName!.ToFirstLetterLowerCase();
        var backendPath = Path.Combine(zipPath, input.NameSpace!, "Service", input.TableName);
        var servicePath = Path.Combine(backendPath, input.TableName + "Service.cs");
        var inputPath = Path.Combine(backendPath, "Dto", input.TableName + "Input.cs");
        var outputPath = Path.Combine(backendPath, "Dto", input.TableName + "Output.cs");
        var viewPath = Path.Combine(backendPath, "Dto", input.TableName + "Dto.cs");
        if (VbenGenerateTypes.Contains(input.GenerateType!))
        {
            var vbenRoot = Path.Combine(zipPath, _codeGenOptions.VbenRootPath);
            var vbenIndexPath = Path.Combine(vbenRoot, "src", "views", input.PagePath!, firstLowerTableName, "index.vue");
            var vbenApiPath = Path.Combine(vbenRoot, "src", "api", input.PagePath!, firstLowerTableName + ".ts");
            if (VbenFrontendOnlyGenerateTypes.Contains(input.GenerateType!)) return new() { vbenIndexPath, vbenApiPath };
            return new() { servicePath, inputPath, outputPath, viewPath, vbenIndexPath, vbenApiPath };
        }

        var frontendPath = Path.Combine(zipPath, _codeGenOptions.FrontRootPath, "src", "views", input.PagePath!);
        var indexPath = Path.Combine(frontendPath, firstLowerTableName, "index.vue");
        var formModalPath = Path.Combine(frontendPath, firstLowerTableName, "component", "editDialog.vue");
        var apiJsPath = Path.Combine(zipPath, _codeGenOptions.FrontRootPath, "src", "api", input.PagePath, firstLowerTableName + ".ts");
        if (input.GenerateType!.StartsWith("11"))
        {
            return new List<string>
            {
                indexPath,
                formModalPath,
                apiJsPath
            };
        }

        if (input.GenerateType.StartsWith("12"))
        {
            return new List<string>
            {
                servicePath,
                inputPath,
                outputPath,
                viewPath
            };
        }

        return new List<string>
        {
            servicePath,
            inputPath,
            outputPath,
            viewPath,
            indexPath,
            formModalPath,
            apiJsPath
        };
    }

    private string GetRepositoryRoot() => new DirectoryInfo(App.WebHostEnvironment.ContentRootPath).Parent!.Parent!.FullName;

    private string GetBackendRoot(SysCodeGen input) => Path.Combine(
        new DirectoryInfo(App.WebHostEnvironment.ContentRootPath).Parent!.FullName,
        input.NameSpace!);

    private string GetVbenRoot()
    {
        if (string.IsNullOrWhiteSpace(_codeGenOptions.VbenRootPath)) throw Oops.Oh("VbenRootPath 未配置");
        var vbenRoot = EnsurePathWithin(GetRepositoryRoot(), Path.Combine(GetRepositoryRoot(), _codeGenOptions.VbenRootPath));
        var legacyRoot = Path.GetFullPath(Path.Combine(GetRepositoryRoot(), _codeGenOptions.FrontRootPath ?? "Web"));
        if (Path.GetFullPath(vbenRoot).Equals(legacyRoot, StringComparison.OrdinalIgnoreCase))
            throw Oops.Oh("VbenRootPath 不能指向旧版 Web 目录");
        return vbenRoot;
    }

    private List<string> GetAllowedLocalRoots(SysCodeGen input)
    {
        var roots = new List<string>();
        if (!VbenFrontendOnlyGenerateTypes.Contains(input.GenerateType!)) roots.Add(GetBackendRoot(input));
        if (VbenGenerateTypes.Contains(input.GenerateType!)) roots.Add(GetVbenRoot());
        return roots;
    }
}
