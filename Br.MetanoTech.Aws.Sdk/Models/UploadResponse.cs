using System.Diagnostics.CodeAnalysis;

namespace Br.MetanoTech.Aws.Sdk.Models
{
    public class UploadResponse
    {
        public UploadResponse() { }

        [SetsRequiredMembers]
        public UploadResponse(string message, string statusCode, string key)

        {
            Key = key;
            Message = message;
            StatusCode = statusCode;
        }

        public required string Key { get; set; }
        public string StatusCode { get; set; }
        public required string? Message { get; set; }
        public ErrorUploadResponse? Error { get; set; }
    }
}
