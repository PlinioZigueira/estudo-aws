namespace Br.MetanoTech.Aws.Sdk.Models
{
    public sealed class MultipartUploadRequestModel
    {
        public required string FilePath { get; set; }
        public int PartSize { get; internal set; }
        public required string ContentType { get; set; }
        public required string UploadId { get; set; }
        public required string Key { get; set; }
        public required List<Uri> Urls { get; set; }
        public required int TotalParts { get; set; }
    }
}
