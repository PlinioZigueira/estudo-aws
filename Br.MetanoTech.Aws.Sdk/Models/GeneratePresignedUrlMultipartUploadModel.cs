namespace Br.MetanoTech.Aws.Sdk.Models
{
    public class GeneratePresignedUrlMultiPartUploadModel
    {
        public string UploadId { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        public required List<Uri> Urls { get; set; }
    }
}
