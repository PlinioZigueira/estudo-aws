namespace Br.MetanoTech.POC_AWS.WebApi.Models
{
    /// <summary>
    /// Modelo recomendado para qualquer tamanho de arquivo
    /// </summary>
    public class UploadFilePathRequest
    {
        /// <summary>
        ///  Caminho do arquivo local a ser realizado o upload
        /// </summary>
        public required string FilePath { get; set; }

        /// <summary>
        /// Define o tamanho de cada parte em que o arquivo será dividido durante o upload multipart.
        /// </summary>
        public required int PartSize { get; set; }
    }
}
