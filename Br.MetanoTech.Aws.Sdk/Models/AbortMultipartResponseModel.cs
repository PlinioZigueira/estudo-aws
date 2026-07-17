using System.Diagnostics.CodeAnalysis;

namespace Br.MetanoTech.Aws.Sdk.Models
{
    public class AbortMultipartResponseModel
    {
        [SetsRequiredMembers]
        public AbortMultipartResponseModel(string message, bool success)
        {
            Message = message;
            Success = success;  
        }

        public required string Message { get; set; }
        public required bool Success { get; set; }
    }
}
