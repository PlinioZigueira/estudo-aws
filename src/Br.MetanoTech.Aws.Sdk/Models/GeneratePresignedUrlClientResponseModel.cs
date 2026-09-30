namespace Br.MetanoTech.Aws.Sdk.Models
{
    public class GeneratePresignedUrlClientResponseModel
    {
        public required IReadOnlyCollection<PreSignedUrl> PreSignedUrls { get; set; }
    }

    public class PreSignedUrl
    {
        public required string FileName { get; set; }
        public required string Key { get; set; }
        public required string Url { get; set; }
        public required UploadRule Rule { get; set; }
    }

    public class UploadRule
    {
        public required string ContentType { get; set; }
        public required long MaxFileSizeBytes { get; set; }
        public double ExpiresInMinutes { get; set; }
    }
}