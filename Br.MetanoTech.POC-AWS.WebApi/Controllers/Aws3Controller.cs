using Amazon.S3.Model;
using Br.MetanoTech.Aws.Sdk;
using Br.MetanoTech.Aws.Sdk.Models;
using Br.MetanoTech.POC_AWS.WebApi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Br.MetanoTech.POC_AWS.WebApi.Controllers
{
    [Controller]
    [Route("api/[controller]")]
    public class Aws3Controller : Controller
    {
        [HttpGet("GeneratePresignedUploadUrl")]
        public IActionResult GenerateUploadUrl()
        {
            try
            {
                return Ok(S3Services.GeneratePresignedUrlToPutAsync());
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        [HttpGet("CreateMultipartUploadUrl")]
        public async Task<IActionResult> CreateMultipartUploadUrl(int totalParts, string contentType)
        {
            try
            {
                return Ok(await S3Services.GeneratePresignedUrlToPutAsync(totalParts, contentType));
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        //implementação do lado do cliente. Recomendado via worker.
        //[RequestSizeLimit(4294967296)] // Libera o tamanho total da requisição (4 GB em bytes)
        //[RequestFormLimits(MultipartBodyLengthLimit = 4294967296)] // Libera o tamanho do arquivo no Form
        [HttpPut("upload/file")]
        public async Task<IActionResult> UploadFile([FromBody] UploadFilePathRequest request)
        {
            try
            {
                if (request.PartSize > 500)
                    return BadRequest(new UploadMultipartResponse("O limite de tamanho de cada parte em que o arquivo pode ser dividido é de 500MB", HttpStatusCode.BadRequest));

               return Ok(await S3Services.UploadFileAsync(request.FilePath, request.PartSize));
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        //implementação do lado do cliente. Recomendado via worker.
        //[RequestSizeLimit(4294967296)] // Libera o tamanho total da requisição (4 GB em bytes)
        //[RequestFormLimits(MultipartBodyLengthLimit = 4294967296)] // Libera o tamanho do arquivo no Form
        [HttpPut("upload/file/stream")]
        public async Task<IActionResult> UploadFileStreamAsync([FromQuery] int partSize)
        {
            try
            {
                if (partSize > 500)
                    return BadRequest(new UploadMultipartResponse("O limite de tamanho de cada parte em que o arquivo pode ser dividido é de 500MB", HttpStatusCode.BadRequest));

                Stream stream = Request.Body;
                return Ok(await S3Services.UploadFileStreamAsync(stream, partSize));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("upload/presigned/file")]
        public async Task<IActionResult> UploadViaPresignedUrl([FromBody] UploadFileViaUrlRequest request)
        {
            try
            {
                return Ok(await S3Services.UploadViaPresignedUrl(request.Url, request.FilePath, request.Key));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("upload/presigned/base64")]
        public async Task<ActionResult<UploadResponse>> UploadBase64ViaPresignedUrl([FromBody] UploadBase64Request request)
        {
            try
            {
                return Ok(await S3Services.UploadBase64ViaPresignedUrl(request.Url, request.Base64Content, request.Key));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("CompleteMultipartUpload")]
        public async Task<IActionResult> CompleteMultipartUpload([FromBody] CompleteMultipartUploadRequest request)
        {
            try
            {
                return Ok(await S3Services.CompleteMultipartUploadAsync(request));
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        [HttpPut("AbortMultipartUpload")]
        public async Task<IActionResult> AbortMultipartUpload(string key, string uploadId)
        {
            try
            {
                return Ok(await S3Services.AbortMultipartUploadAsync(key, uploadId));
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }


        // Client
        [HttpGet("GeneratePresignedUploadUrlClient")]
        public async Task<IActionResult> GenerateUploadUrlClient([FromBody] GenerateUploadUrlClientRequest request)
        {
            try
            {
                var res = await S3Services.GeneratePresignedUrlToPutClientAsync(request);

                return Ok(res);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        
        [HttpPut("UploadViaPresignedClientUrl")]
        public async Task<IActionResult> UploadViaPresignedClientUrl([FromBody] UploadFileViaUrlClientRequest request)
        {
            try
            {
                var res = await S3Services.UploadViaPresignedUrlClient(request);

                return Ok(res);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        [HttpPut("ConfirmUpload")]
        public async Task<IActionResult> ConfirmUploadClient([FromBody] ConfirmUploadClientRequest request)
        {
            try
            {
               return Ok(await S3Services.ConfirmUpload(request));
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}
