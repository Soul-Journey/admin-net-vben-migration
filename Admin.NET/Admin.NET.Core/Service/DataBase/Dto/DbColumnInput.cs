// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

public class DbColumnInput
{
    [MaxLength(64, ErrorMessage = "数据库标识不能超过64个字符")]
    public string ConfigId { get; set; }

    [MaxLength(128, ErrorMessage = "表名不能超过128个字符")]
    public string TableName { get; set; }

    [Required(ErrorMessage = "字段名不能为空")]
    [MaxLength(128, ErrorMessage = "字段名不能超过128个字符")]
    public string DbColumnName { get; set; }

    [Required(ErrorMessage = "字段类型不能为空")]
    [MaxLength(64, ErrorMessage = "字段类型不能超过64个字符")]
    public string DataType { get; set; }

    [Range(0, 65535, ErrorMessage = "字段长度必须为0-65535")]
    public int Length { get; set; }

    [MaxLength(256, ErrorMessage = "字段描述不能超过256个字符")]
    public string ColumnDescription { get; set; }

    [Range(0, 1, ErrorMessage = "可空标记无效")]
    public int IsNullable { get; set; }

    [Range(0, 1, ErrorMessage = "自增标记无效")]
    public int IsIdentity { get; set; }

    [Range(0, 1, ErrorMessage = "主键标记无效")]
    public int IsPrimarykey { get; set; }

    [Range(0, 30, ErrorMessage = "小数位必须为0-30")]
    public int DecimalDigits { get; set; }
}

public class UpdateDbColumnInput
{
    [Required, MaxLength(64)]
    public string ConfigId { get; set; }

    [Required, MaxLength(128)]
    public string TableName { get; set; }

    [Required, MaxLength(128)]
    public string ColumnName { get; set; }

    [Required, MaxLength(128)]
    public string OldColumnName { get; set; }

    [MaxLength(256)]
    public string Description { get; set; }
}

public class DeleteDbColumnInput
{
    [Required, MaxLength(64)]
    public string ConfigId { get; set; }

    [Required, MaxLength(128)]
    public string TableName { get; set; }

    [Required, MaxLength(128)]
    public string DbColumnName { get; set; }
}
