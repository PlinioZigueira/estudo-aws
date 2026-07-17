namespace Br.MetanoTech.Aws.Sdk.Models
{
    /// <summary>
    /// Modelo para arquivos locais
    /// </summary>
    public sealed class ConfirmUploadClientRequest
    {
        public required IReadOnlyCollection<FileKey> DocumentKeys { get; set; }
    }

    public sealed class FileKey
    {
        public required string Key { get; set; }
    }
}
