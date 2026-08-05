namespace Br.MetanoTech.Aws.Sdk.Models
{
    public sealed class MultipartUploadModel
    {
        public required string Key { get; init; }
        public required string UploadId { get; init; }
        public required string ContentType { get; set; }
        public required long PartSizeBytes { get; set; }
        public required List<UploadPart> Parts { get; init; }
    }

    public sealed class UploadPart
    {
        public int PartNumber { get; init; }
        public Uri Url { get; set; } = default!;
        public long Offset { get; init; }
        public long Length { get; init; }
        public string? ETag { get; set; }
        public int Attempts { get; set; }
        public bool Uploaded => ETag != null;
        public Exception? Error { get; set; }
    }
}