namespace Br.MetanoTech.Aws.Sdk.Models
{
    /// <summary>
    /// Modelo para arquivos locais
    /// </summary>
    public class UploadFileViaUrlClientRequest
    {
        public required IReadOnlyList<Documents> Documents { get; set; }
    }

    public class Documents
    {
        public required string FilePath { get; set; }
        public required string Url { get; set; }
        public required string Key { get; set; }
    }
}
