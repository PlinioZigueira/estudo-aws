namespace Br.MetanoTech.Aws.Sdk.Models
{
    public class GeneratePresignedUrlSequentialModel
    {
        public string? UploadId { get; set; }

        public string? Key { get; set; }

        public required Uri? Url { get; set; }
    }
}
