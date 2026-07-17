namespace Br.MetanoTech.Aws.Sdk.Models
{
    public class ConfirmUploadResponse
    {
        public List<FileStatus> Documents { get; set; } = [];
    }

    public class FileStatus
    {
        public required string Key { get; set; }
        public bool IsValid { get; set; } = false;
        public List<Information> Messages { get; set; } = [];
    }

    public class Information
    {
        public required string Message { get; set; }
    }
}
