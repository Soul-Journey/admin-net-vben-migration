// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Aliyun.OSS.Util;
using OnceMi.AspNetCore.OSS;

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统文件服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 410, Description = "系统文件")]
public class SysFileService : IDynamicApiController, ITransient
{
    private readonly UserManager _userManager;
    private readonly SqlSugarRepository<SysFile> _sysFileRep;
    private readonly OSSProviderOptions _OSSProviderOptions;
    private readonly UploadOptions _uploadOptions;
    private readonly IOSSService _OSSService;
    private readonly string _imageType = ".jpeg.jpg.png.bmp.gif.tif";

    public SysFileService(UserManager userManager,
        SqlSugarRepository<SysFile> sysFileRep,
        IOptions<OSSProviderOptions> oSSProviderOptions,
        IOptions<UploadOptions> uploadOptions,
        IOSSServiceFactory ossServiceFactory)
    {
        _userManager = userManager;
        _sysFileRep = sysFileRep;
        _OSSProviderOptions = oSSProviderOptions.Value;
        _uploadOptions = uploadOptions.Value;
        if (_OSSProviderOptions.Enabled)
            _OSSService = ossServiceFactory.Create(Enum.GetName(_OSSProviderOptions.Provider));
    }

    /// <summary>
    /// 获取文件分页列表 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取文件分页列表")]
    public async Task<SqlSugarPagedList<SysFile>> Page(PageFileInput input)
    {
        // 获取所有公开附件
        var publicList = _sysFileRep.AsQueryable().ClearFilter()
            .Where(u => u.IsPublic == true && u.IsDelete == false);
        // 获取私有附件
        var privateList = _sysFileRep.AsQueryable().Where(u => u.IsPublic == false);
        // 合并公开和私有附件并分页
        return await _sysFileRep.Context.UnionAll(publicList, privateList)
            .WhereIF(_userManager.SuperAdmin && input.TenantId > 0, u => u.TenantId == input.TenantId)
            .WhereIF(!string.IsNullOrWhiteSpace(input.FileName), u => u.FileName.Contains(input.FileName.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.StartTime.ToString()) && !string.IsNullOrWhiteSpace(input.EndTime.ToString()),
                u => u.CreateTime >= input.StartTime && u.CreateTime <= input.EndTime)
            .OrderBy(u => u.CreateTime, OrderByType.Desc)
            .ToPagedListAsync(input.Page, input.PageSize);
    }

    /// <summary>
    /// 上传文件Base64 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("上传文件Base64")]
    public async Task<SysFile> UploadFileFromBase64(UploadFileFromBase64Input input)
    {
        var pattern = @"data:(?<type>.+?);base64,(?<data>[^""]+)";
        var regex = new Regex(pattern, RegexOptions.Compiled);
        var match = regex.Match(input.FileDataBase64);

        byte[] fileData = Convert.FromBase64String(match.Groups["data"].Value);
        var contentType = match.Groups["type"].Value;
        if (string.IsNullOrEmpty(input.FileName))
            input.FileName = $"{YitIdHelper.NextId()}.{contentType.AsSpan(contentType.LastIndexOf('/') + 1)}";

        var ms = new MemoryStream();
        ms.Write(fileData);
        ms.Seek(0, SeekOrigin.Begin);
        IFormFile formFile = new FormFile(ms, 0, fileData.Length, "file", input.FileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
        var uploadFileInput = input.Adapt<UploadFileInput>();
        uploadFileInput.File = formFile;
        return await UploadFile(uploadFileInput);
    }

    /// <summary>
    /// 上传多文件 🔖
    /// </summary>
    /// <param name="files"></param>
    /// <returns></returns>
    [DisplayName("上传多文件")]
    public List<SysFile> UploadFiles([Required] List<IFormFile> files)
    {
        var fileList = new List<SysFile>();
        files.ForEach(file => fileList.Add(UploadFile(new UploadFileInput { File = file }).Result));
        return fileList;
    }

    /// <summary>
    /// 根据文件Id或Url下载 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("根据文件Id或Url下载")]
    public async Task<IActionResult> DownloadFile(SysFile input)
    {
        var file = input.Id > 0
            ? await GetFile(input.Id)
            : await GetReadableFileByUrl(input.Url);
        var fileName = HttpUtility.UrlEncode(file.FileName, Encoding.GetEncoding("UTF-8"));
        return await GetFileStreamResult(file, fileName);
    }

    /// <summary>
    /// 文件预览 🔖
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [DisplayName("文件预览")]
    public async Task<IActionResult> GetPreview([FromRoute] long id)
    {
        var file = await GetFile(id);
        return await GetFileStreamResult(file, file.Id + "");
    }

    /// <summary>
    /// 获取文件流
    /// </summary>
    /// <param name="file"></param>
    /// <param name="fileName"></param>
    /// <returns></returns>
    [NonAction]
    public async Task<IActionResult> GetFileStreamResult(SysFile file, string fileName)
    {
        var filePath = Path.Combine(file.FilePath ?? "", file.Id + file.Suffix);
        if (_OSSProviderOptions.Enabled)
        {
            var stream = await (await _OSSService.PresignedGetObjectAsync(file.BucketName, filePath, 5)).GetAsStreamAsync();
            return new FileStreamResult(stream.Stream, "application/octet-stream") { FileDownloadName = fileName + file.Suffix };
        }

        if (App.Configuration["SSHProvider:Enabled"].ToBoolean())
        {
            using SSHHelper helper = new(App.Configuration["SSHProvider:Host"],
                App.Configuration["SSHProvider:Port"].ToInt(), App.Configuration["SSHProvider:Username"], App.Configuration["SSHProvider:Password"]);
            return new FileStreamResult(helper.OpenRead(filePath), "application/octet-stream") { FileDownloadName = fileName + file.Suffix };
        }

        var path = Path.Combine(App.WebHostEnvironment.WebRootPath, filePath);
        return new FileStreamResult(new FileStream(path, FileMode.Open), "application/octet-stream") { FileDownloadName = fileName + file.Suffix };
    }

    /// <summary>
    /// 下载指定文件Base64格式 🔖
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    [DisplayName("下载指定文件Base64格式")]
    public async Task<string> DownloadFileBase64([FromBody] string url)
    {
        if (_OSSProviderOptions.Enabled)
        {
            using var httpClient = new HttpClient();
            HttpResponseMessage response = await httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                // 读取文件内容并将其转换为 Base64 字符串
                byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
                return Convert.ToBase64String(fileBytes);
            }
            throw new HttpRequestException($"Request failed with status code: {response.StatusCode}");
        }

        if (App.Configuration["SSHProvider:Enabled"].ToBoolean())
        {
            var sysFile = await _sysFileRep.CopyNew().GetFirstAsync(u => u.Url == url) ?? throw Oops.Oh($"文件不存在");
            using SSHHelper helper = new SSHHelper(App.Configuration["SSHProvider:Host"],
                App.Configuration["SSHProvider:Port"].ToInt(), App.Configuration["SSHProvider:Username"], App.Configuration["SSHProvider:Password"]);
            return Convert.ToBase64String(helper.ReadAllBytes(sysFile.FilePath));
        }
        else
        {
            var sysFile = await _sysFileRep.CopyNew().GetFirstAsync(u => u.Url == url) ?? throw Oops.Oh($"文件不存在");
            var filePath = Path.Combine(App.WebHostEnvironment.WebRootPath, sysFile.FilePath);
            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            var realFile = Path.Combine(filePath, $"{sysFile.Id}{sysFile.Suffix}");
            if (!File.Exists(realFile))
            {
                Log.Error($"DownloadFileBase64:文件[{realFile}]不存在");
                throw Oops.Oh($"文件[{sysFile.FilePath}]不存在");
            }
            byte[] fileBytes = await File.ReadAllBytesAsync(realFile);
            return Convert.ToBase64String(fileBytes);
        }
    }

    /// <summary>
    /// 删除文件 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Delete"), HttpPost]
    [DisplayName("删除文件")]
    [UnitOfWork]
    public async Task DeleteFile(DeleteFileInput input)
    {
        var file = await _sysFileRep.GetByIdAsync(input.Id);
        if (file != null)
        {
            await _sysFileRep.DeleteAsync(file);

            if (_OSSProviderOptions.Enabled)
            {
                await _OSSService.RemoveObjectAsync(file.BucketName, string.Concat(file.FilePath, "/", $"{input.Id}{file.Suffix}"));
            }
            else if (App.Configuration["SSHProvider:Enabled"].ToBoolean())
            {
                var fullPath = string.Concat(file.FilePath, "/", file.Id + file.Suffix);
                using SSHHelper helper = new(App.Configuration["SSHProvider:Host"],
                    App.Configuration["SSHProvider:Port"].ToInt(), App.Configuration["SSHProvider:Username"], App.Configuration["SSHProvider:Password"]);
                helper.DeleteFile(fullPath);
            }
            else
            {
                var filePath = Path.Combine(App.WebHostEnvironment.WebRootPath, file.FilePath ?? "", input.Id + file.Suffix);
                if (File.Exists(filePath)) File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// 更新文件 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    [DisplayName("更新文件")]
    public async Task UpdateFile(UpdateFileInput input)
    {
        var file = await _sysFileRep.GetByIdAsync(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.D8000);
        file.FileName = input.FileName.Trim();
        file.FileType = input.FileType?.Trim();
        file.IsPublic = input.IsPublic;
        file.RelationName = input.RelationName?.Trim();
        file.RelationId = input.RelationId;
        file.BelongId = input.BelongId;
        await _sysFileRep.AsUpdateable(file)
            .UpdateColumns(u => new
            {
                u.FileName,
                u.FileType,
                u.IsPublic,
                u.RelationName,
                u.RelationId,
                u.BelongId,
                u.UpdateTime,
                u.UpdateUserId,
                u.UpdateUserName,
            })
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// 获取文件 🔖
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [DisplayName("获取文件")]
    public async Task<SysFile> GetFile([FromQuery] long id)
    {
        var file = await _sysFileRep.CopyNew().GetByIdAsync(id);
        if (file != null) return file;

        file = await _sysFileRep.CopyNew().AsQueryable().ClearFilter()
            .FirstAsync(u => u.Id == id && u.IsPublic == true && u.IsDelete == false);
        return file ?? throw Oops.Oh(ErrorCodeEnum.D8000);
    }

    [NonAction]
    private async Task<SysFile> GetReadableFileByUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) throw Oops.Oh(ErrorCodeEnum.D8000);
        var file = await _sysFileRep.CopyNew().GetFirstAsync(u => u.Url == url);
        if (file != null) return file;

        file = await _sysFileRep.CopyNew().AsQueryable().ClearFilter()
            .FirstAsync(u => u.Url == url && u.IsPublic == true && u.IsDelete == false);
        return file ?? throw Oops.Oh(ErrorCodeEnum.D8000);
    }

    /// <summary>
    /// 上传文件 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("上传文件")]
    public async Task<SysFile> UploadFile([FromForm] UploadFileInput input)
    {
        if (input.File == null) throw Oops.Oh(ErrorCodeEnum.D8000);

        // 判断是否重复上传的文件
        var sizeKb = input.File.Length / 1024; // 大小KB
        var fileMd5 = string.Empty;
        if (_uploadOptions.EnableMd5)
        {
            await using (var fileStream = input.File.OpenReadStream())
            {
                fileMd5 = OssUtils.ComputeContentMd5(fileStream, fileStream.Length);
            }
            // Mysql8 中如果使用了 utf8mb4_general_ci 之外的编码会出错，尽量避免在条件里使用.ToString()
            // 因为 Squsugar 并不是把变量转换为字符串来构造SQL语句，而是构造了CAST(123 AS CHAR)这样的语句，这样这个返回值是utf8mb4_general_ci，所以容易出错。
            var sysFile = await _sysFileRep.GetFirstAsync(u => u.FileMd5 == fileMd5 && u.SizeKb == sizeKb);
            if (sysFile != null) return sysFile;
        }

        // 验证文件类型
        if (!_uploadOptions.ContentType.Contains(input.File.ContentType)) throw Oops.Oh($"{ErrorCodeEnum.D8001}:{input.File.ContentType}");

        // 验证文件大小
        if (sizeKb > _uploadOptions.MaxSize) throw Oops.Oh($"{ErrorCodeEnum.D8002}，允许最大：{_uploadOptions.MaxSize}KB");

        // 获取文件后缀
        var suffix = Path.GetExtension(input.File.FileName).ToLower(); // 后缀
        if (string.IsNullOrWhiteSpace(suffix))
            suffix = string.Concat(".", input.File.ContentType.AsSpan(input.File.ContentType.LastIndexOf('/') + 1));
        if (!string.IsNullOrWhiteSpace(suffix))
        {
            //var contentTypeProvider = FS.GetFileExtensionContentTypeProvider();
            //suffix = contentTypeProvider.Mappings.FirstOrDefault(u => u.Value == file.ContentType).Key;
            // 修改 image/jpeg 类型返回的 .jpeg、jpe 后缀
            if (suffix == ".jpeg" || suffix == ".jpe")
                suffix = ".jpg";
        }
        if (string.IsNullOrWhiteSpace(suffix)) throw Oops.Oh(ErrorCodeEnum.D8003);

        // 防止客户端伪造文件类型
        if (!string.IsNullOrWhiteSpace(input.AllowSuffix) && !input.AllowSuffix.Contains(suffix)) throw Oops.Oh(ErrorCodeEnum.D8003);
        //if (!VerifyFileExtensionName.IsSameType(file.OpenReadStream(), suffix))
        //    throw Oops.Oh(ErrorCodeEnum.D8001);

        // 文件存储位置
        var path = string.IsNullOrWhiteSpace(input.SavePath) ? _uploadOptions.Path : input.SavePath;
        path = path.ParseToDateTimeForRep();

        var newFile = input.Adapt<SysFile>();
        newFile.Id = YitIdHelper.NextId();
        newFile.BucketName = _OSSProviderOptions.Enabled ? _OSSProviderOptions.Bucket : "Local"; // 阿里云对bucket名称有要求，1.只能包括小写字母，数字，短横线（-）2.必须以小写字母或者数字开头  3.长度必须在3-63字节之间
        newFile.FileName = Path.GetFileNameWithoutExtension(input.File.FileName);
        newFile.Suffix = suffix;
        newFile.SizeKb = sizeKb;
        newFile.FilePath = path;
        newFile.FileMd5 = fileMd5;

        var finalName = newFile.Id + suffix; // 文件最终名称
        if (_OSSProviderOptions.Enabled)
        {
            newFile.Provider = Enum.GetName(_OSSProviderOptions.Provider);
            var filePath = string.Concat(path, "/", finalName);
            await _OSSService.PutObjectAsync(newFile.BucketName, filePath, input.File.OpenReadStream());
            //  http://<你的bucket名字>.oss.aliyuncs.com/<你的object名字>
            //  生成外链地址 方便前端预览
            switch (_OSSProviderOptions.Provider)
            {
                case OSSProvider.Aliyun:
                    newFile.Url = $"{(_OSSProviderOptions.IsEnableHttps ? "https" : "http")}://{newFile.BucketName}.{_OSSProviderOptions.Endpoint}/{filePath}";
                    break;

                case OSSProvider.QCloud:
                    var protocol = _OSSProviderOptions.IsEnableHttps ? "https" : "http";
                    newFile.Url = !string.IsNullOrWhiteSpace(_OSSProviderOptions.CustomHost)
                        ? $"{protocol}://{_OSSProviderOptions.CustomHost}/{filePath}"
                        : $"{protocol}://{newFile.BucketName}-{_OSSProviderOptions.Endpoint}.cos.{_OSSProviderOptions.Region}.myqcloud.com/{filePath}";
                    break;

                case OSSProvider.Minio:
                    // 获取Minio文件的下载或者预览地址
                    // newFile.Url = await GetMinioPreviewFileUrl(newFile.BucketName, filePath);// 这种方法生成的Url是有7天有效期的，不能这样使用
                    // 需要在MinIO中的Buckets开通对 Anonymous 的readonly权限
                    var customHost = _OSSProviderOptions.CustomHost;
                    if (string.IsNullOrWhiteSpace(customHost))
                        customHost = _OSSProviderOptions.Endpoint;
                    newFile.Url = $"{(_OSSProviderOptions.IsEnableHttps ? "https" : "http")}://{customHost}/{newFile.BucketName}/{filePath}";
                    break;
            }
        }
        else if (App.Configuration["SSHProvider:Enabled"].ToBoolean())
        {
            var fullPath = string.Concat(path.StartsWith('/') ? path : "/" + path, "/", finalName);
            using SSHHelper helper = new(App.Configuration["SSHProvider:Host"],
                App.Configuration["SSHProvider:Port"].ToInt(), App.Configuration["SSHProvider:Username"], App.Configuration["SSHProvider:Password"]);
            helper.UploadFile(input.File.OpenReadStream(), fullPath);
        }
        else
        {
            newFile.Provider = ""; // 本地存储 Provider 显示为空
            var filePath = Path.Combine(App.WebHostEnvironment.WebRootPath, path);
            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            var realFile = Path.Combine(filePath, finalName);
            await using (var stream = File.Create(realFile))
            {
                await input.File.CopyToAsync(stream);
            }

            newFile.Url = $"{newFile.FilePath}/{newFile.Id + newFile.Suffix}";
        }
        await _sysFileRep.AsInsertable(newFile).ExecuteCommandAsync();
        return newFile;
    }

    /// <summary>
    /// 上传头像 🔖
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    [DisplayName("上传头像")]
    public async Task<SysFile> UploadAvatar([Required] IFormFile file)
    {
        var sysFile = await UploadFile(new UploadFileInput { File = file, AllowSuffix = _imageType, SavePath = "upload/avatar" });

        var sysUserRep = _sysFileRep.ChangeRepository<SqlSugarRepository<SysUser>>();
        var user = await sysUserRep.GetByIdAsync(_userManager.UserId);
        // 删除已有头像文件
        if (!string.IsNullOrWhiteSpace(user.Avatar))
        {
            var fileId = Path.GetFileNameWithoutExtension(user.Avatar);
            await DeleteFile(new DeleteFileInput { Id = long.Parse(fileId) });
        }
        await sysUserRep.UpdateAsync(u => new SysUser() { Avatar = sysFile.Url }, u => u.Id == user.Id);
        return sysFile;
    }

    /// <summary>
    /// 上传电子签名 🔖
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    [DisplayName("上传电子签名")]
    public async Task<SysFile> UploadSignature([Required] IFormFile file)
    {
        var sysFile = await UploadFile(new UploadFileInput { File = file, AllowSuffix = _imageType, SavePath = "upload/signature" });

        var sysUserRep = _sysFileRep.ChangeRepository<SqlSugarRepository<SysUser>>();
        var user = await sysUserRep.GetByIdAsync(_userManager.UserId);
        // 删除已有电子签名文件
        if (!string.IsNullOrWhiteSpace(user.Signature) && user.Signature.EndsWith(".png"))
        {
            var fileId = Path.GetFileNameWithoutExtension(user.Signature);
            await DeleteFile(new DeleteFileInput { Id = long.Parse(fileId) });
        }
        await sysUserRep.UpdateAsync(u => new SysUser() { Signature = sysFile.Url }, u => u.Id == user.Id);
        return sysFile;
    }

    /// <summary>
    /// 修改附件关联对象 🔖
    /// </summary>
    /// <param name="ids"></param>
    /// <param name="relationName"></param>
    /// <param name="relationId"></param>
    /// <param name="belongId"></param>
    /// <returns></returns>
    [NonAction]
    public async Task<int> UpdateRelation(List<long> ids, string relationName, long relationId, long belongId = 0)
    {
        if (ids == null || ids.Count == 0)
            return 0;
        return await _sysFileRep.AsUpdateable()
            .SetColumns(u => u.RelationName == relationName)
            .SetColumns(u => u.RelationId == relationId)
            .SetColumns(u => u.BelongId == belongId)
            .Where(u => ids.Contains(u.Id))
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// 根据关联查询附件
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    [DisplayName("根据关联查询附件")]
    public async Task<List<SysFile>> GetRelationFiles([FromQuery] RelationQueryInput input)
    {
        return await _sysFileRep.AsQueryable()
            .WhereIF(input.RelationId is > 0, u => u.RelationId == input.RelationId)
            .WhereIF(input.BelongId is > 0, u => u.BelongId == input.BelongId.Value)
            .WhereIF(!string.IsNullOrWhiteSpace(input.RelationName), u => u.RelationName == input.RelationName)
            .WhereIF(!string.IsNullOrWhiteSpace(input.FileTypes), u => input.GetFileTypeBS().Contains(u.FileType))
            .Select(u => new SysFile
            {
                Url = SqlFunc.MergeString("/api/sysFile/Preview/", u.Id.ToString()),
            }, true)
           .ToListAsync();
    }
}
