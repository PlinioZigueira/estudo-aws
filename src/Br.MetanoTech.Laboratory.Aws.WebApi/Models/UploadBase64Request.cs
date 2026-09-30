namespace Br.MetanoTech.POC_AWS.WebApi.Models
{
    /// <summary>
    /// Modelo reservado para cenários em que os dados seja base64. 
    /// Não recomendado uploads de arquivos grandes.
    /// </summary>
    public class UploadBase64Request
    {
        /// <summary>
        /// Arquivo no formato base64
        /// </summary>
        public string Base64Content { get; set; } = string.Empty;

        /// <summary>
        /// Url pré-assinada S3 para realizar upload
        /// </summary>
        public required string Url { get; set; }
    }
}
