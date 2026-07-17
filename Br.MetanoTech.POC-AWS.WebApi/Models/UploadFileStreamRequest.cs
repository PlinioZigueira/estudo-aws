namespace Br.MetanoTech.POC_AWS.WebApi.Models
{
    /// <summary>
    /// Modelo recomendado para qualquer tamanho de arquivo
    /// </summary>
    public class UploadFileStreamRequest
    {
        /// <summary>
        /// Stream containing the file content to be uploaded.
        /// </summary>
        public required Stream Stream { get; set; }

        /// <summary>
        /// Define o tamanho de cada parte em que o arquivo será dividido durante o upload multipart.
        /// </summary>
        public required int PartSize { get; set; }
    }
}
