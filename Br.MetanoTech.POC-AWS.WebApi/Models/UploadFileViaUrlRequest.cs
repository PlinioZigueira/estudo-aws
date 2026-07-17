namespace Br.MetanoTech.POC_AWS.WebApi.Models
{
    /// <summary>
    /// Modelo para arquivos locais
    /// </summary>
    public class UploadFileViaUrlRequest
    {
        /// <summary>
        /// Caminho do arquivo local a ser realizado o upload
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Url pré-assinada S3 para realizar upload
        /// </summary>
        public required string Url { get; set; }

        public required string Key { get; set; }
    }
}
