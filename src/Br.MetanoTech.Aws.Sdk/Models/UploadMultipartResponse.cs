using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Br.MetanoTech.Aws.Sdk.Models
{
    public class UploadMultipartResponse
    {
        [SetsRequiredMembers]
        public UploadMultipartResponse(string message, HttpStatusCode httpStatusCode, string? key = null)
        {   
            Message = message;
            HttpStatusCode = httpStatusCode;
            Key = key;
        }

        public HttpStatusCode HttpStatusCode { get; set; }
        public required string Message { get; set; }
        public string? Key { get; set; }
    }
}
