using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Admin.NET.Core.Service;

internal static class LogSanitizer
{
    private static readonly Regex SensitiveTextRegex = new(
        @"(?i)(password|pwd|token|secret|authorization|cookie|private.?key|connection(?:string)?)\s*[:=]\s*([^,;\s\""']+)",
        RegexOptions.Compiled);

    private static readonly string[] SensitiveNames =
    {
        "password", "pwd", "token", "accessToken", "refreshToken", "secret",
        "authorization", "cookie", "privateKey", "connection", "connectionString",
    };

    public static SysLogOp Sanitize(SysLogOp log)
    {
        if (log == null) return null;
        log.RequestParam = SanitizeValue(log.RequestParam);
        log.ReturnResult = SanitizeValue(log.ReturnResult);
        log.Message = SanitizeValue(log.Message);
        log.Exception = SanitizeValue(log.Exception);
        return log;
    }

    public static SysLogEx Sanitize(SysLogEx log)
    {
        log.RequestParam = SanitizeValue(log.RequestParam);
        log.ReturnResult = SanitizeValue(log.ReturnResult);
        log.Message = SanitizeValue(log.Message);
        log.Exception = SanitizeValue(log.Exception);
        return log;
    }

    public static SysLogDiff Sanitize(SysLogDiff log)
    {
        log.DiffData = SanitizeValue(log.DiffData);
        log.Parameters = SanitizeValue(log.Parameters);
        log.BusinessData = SanitizeValue(log.BusinessData);
        log.Sql = SanitizeValue(log.Sql);
        return log;
    }

    private static string? SanitizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        try
        {
            var token = JToken.Parse(value);
            Redact(token);
            return token.ToString(Formatting.Indented);
        }
        catch (JsonReaderException)
        {
            return SensitiveTextRegex.Replace(value, "$1=******");
        }
    }

    private static void Redact(JToken token)
    {
        if (token is JObject obj)
        {
            var namedField = obj.Properties().FirstOrDefault(property =>
                property.Name.Equals("columnName", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Equals("parameterName", StringComparison.OrdinalIgnoreCase));
            if (namedField != null && IsSensitiveName(namedField.Value.ToString()))
            {
                foreach (var valueName in new[] { "value", "beforeValue", "afterValue" })
                {
                    var valueProperty = obj.Properties().FirstOrDefault(property => property.Name.Equals(valueName, StringComparison.OrdinalIgnoreCase));
                    if (valueProperty != null) valueProperty.Value = "******";
                }
            }

            foreach (var property in obj.Properties().ToList())
            {
                if (IsSensitiveName(property.Name))
                    property.Value = "******";
                else
                    Redact(property.Value);
            }
            return;
        }

        if (token is JArray array)
            foreach (var item in array) Redact(item);
    }

    private static bool IsSensitiveName(string name) =>
        SensitiveNames.Any(sensitiveName => name.Contains(sensitiveName, StringComparison.OrdinalIgnoreCase));
}
