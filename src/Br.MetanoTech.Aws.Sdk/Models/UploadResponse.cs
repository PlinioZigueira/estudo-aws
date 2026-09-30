using System.Diagnostics.CodeAnalysis;

namespace Br.MetanoTech.Aws.Sdk.Models
{
    public class UploadResponse
    {
        public UploadResponse() { }

        [SetsRequiredMembers]
        public UploadResponse(string message, string statusCode)
        {   
            Message = message;
            StatusCode = statusCode;
        }
        
        public required string StatusCode { get; set; }
        public required string? Message { get; set; }
        public ErrorUploadResponse? Error { get; set; }
    }
}
