namespace Br.MetanoTech.Aws.Sdk.Models
{
    public sealed class GenerateUploadUrlClientRequest
    {
        public required IReadOnlyCollection<GeneratePresignedDocumentRequest> Documents { get; init; }
    }

    public sealed class GeneratePresignedDocumentRequest
    {
        public required string FileName { get; set; }
        public required UploadDocumenType DocumenType { get; init; }
    }

    public enum UploadDocumenType
    {
        // Images
        Png,
        Jpeg,
        Gif,
        Bmp,
        Tiff,

        // DocumentKeys
        Pdf,
        Rtf,
        Xml,
        Json,
        Csv,
        Txt,

        // Microsoft Office
        Docx,
        Xlsx,
        Pptx,

        // Compressed
        Zip,
        Rar,
        SevenZip,

        // Audio
        Mp3,
        Wav,

        // Video
        Mp4,
        Webm
    }
}
