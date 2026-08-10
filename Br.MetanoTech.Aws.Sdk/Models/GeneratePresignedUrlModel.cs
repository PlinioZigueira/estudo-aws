namespace Br.MetanoTech.Aws.Sdk.Models
{
    public class GeneratePresignedUrlModel
    {
        public required string Key { get; set; } = string.Empty;
        public required string Url { get; set; } = string.Empty;
        public required DateTime ExpiresAt { get; set; }
    }
}
