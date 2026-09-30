using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Br.MetanoTech.Aws.Sdk.Helpers;
using Br.MetanoTech.Aws.Sdk.Models;
using System.Buffers;
using System.Collections.Concurrent;
using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Xml.Serialization;

namespace Br.MetanoTech.Aws.Sdk
{
    internal static class S3UploadService
    {
        private static readonly AmazonS3Client client = new(RegionEndpoint.USEast1);
        private static readonly HttpClient _httpClient = new();
        private static readonly string bucketName = "dev-lab-local";

        internal static async Task<UploadMultipartResponse> UploadAsync(string filePath, int partSize, CancellationToken cancellationToken)
        {
            var key = S3Services.CreateRandomKey();

            string uploadId = await InitiateMultipartUploadAsync(key, cancellationToken);

            try
            {
                var multiPartUpload = await PrepareMultipartUploadAsync(key, filePath, partSize, uploadId, cancellationToken);

                var uploadResult = await UploadPartsAsync(filePath, multiPartUpload, cancellationToken);

                if (uploadResult.Parts.Any(x => !x.Uploaded))
                {
                    //abortar upload 
                    var abortSuccess = await AbortMultipartUploadAsync(uploadResult.Key, uploadResult.UploadId, cancellationToken);

                    if (!abortSuccess.Success)
                        return new UploadMultipartResponse("Erro ao cancelar o upload do arquivo.", HttpStatusCode.InternalServerError, uploadResult.Key);
                    else
                        return new UploadMultipartResponse("Upload cancelado com sucesso.", HttpStatusCode.BadRequest, uploadResult.Key);
                }
                else
                {
                    var partETags = new List<PartETag>();

                    uploadResult.Parts.ForEach(x =>
                    {
                        partETags.Add(new PartETag
                        {
                            PartNumber = x.PartNumber,
                            ETag = x.ETag,
                        });
                    });

                    var completeMultipartModel = new CompleteMultipartUploadRequest
                    {
                        BucketName = bucketName,
                        Key = uploadResult.Key,
                        UploadId = uploadResult.UploadId,
                        PartETags = partETags
                    };

                    var completedMultiparUploadResult = await CompleteMultipartUploadAsync(completeMultipartModel, cancellationToken);

                    if (completedMultiparUploadResult.HttpStatusCode == HttpStatusCode.OK)
                        return new UploadMultipartResponse("Upload realizado com sucesso.", HttpStatusCode.OK, uploadResult.Key);
                    else
                    {
                        var abortSuccess = await AbortMultipartUploadAsync(uploadResult.Key, uploadResult.UploadId, cancellationToken);

                        if (!abortSuccess.Success)
                            return new UploadMultipartResponse("Erro ao cancelar o upload do arquivo.", HttpStatusCode.InternalServerError, uploadResult.Key);
                        else
                            return new UploadMultipartResponse("Upload cancelado com sucesso.", HttpStatusCode.BadRequest, uploadResult.Key);
                    }
                }
            }
            catch (AmazonS3Exception ex)
            {
                throw new AmazonS3Exception(ex);
            }
            catch (OperationCanceledException)
            {
                await AbortMultipartUploadAsync(key, uploadId, cancellationToken);
                throw;
            }
        }

        internal static async Task<UploadMultipartResponse> UploadFileStreamAsync(Stream stream, int partSize, CancellationToken cancellationToken)
        {
            var key = S3Services.CreateRandomKey();

            try
            {
                var uploadPending = await UploadPartAsync(stream, partSize, key, cancellationToken);

                if (uploadPending is null)
                    return new UploadMultipartResponse("Erro  ao relizar upload  do arquivo.", HttpStatusCode.BadRequest, key);

                var completedMultiparUploadResult = await CompleteMultipartUploadAsync(uploadPending, cancellationToken);

                if (completedMultiparUploadResult.HttpStatusCode != HttpStatusCode.OK)
                {
                    var abortSuccess = await AbortMultipartUploadAsync(uploadPending.Key, uploadPending.UploadId, cancellationToken);

                    if (!abortSuccess.Success)
                        return new UploadMultipartResponse("Ocorreu erro ao realizar e cancelar a solicitação de upload do arquivo.", HttpStatusCode.BadRequest, key);
                    else
                        return new UploadMultipartResponse("Erro ao realizado Upload do arquivo. Upload cancelado com sucesso.", HttpStatusCode.BadRequest, key);
                }
                else
                    return new UploadMultipartResponse("Upload realizado com sucesso.", HttpStatusCode.OK, key);
            }
            catch (AmazonS3Exception ex)
            {
                throw new AmazonS3Exception(ex);
            }
        }

        internal static async Task<UploadResponse> UploadViaPresignedUrl(string url, string filePath, CancellationToken cancellationToken)
        {
            using var streamContent = await FileUtilities.ConvertFileToStreamAsync(filePath);
            var httpClientResult = await _httpClient.PutAsync(url, streamContent, cancellationToken);

            if (httpClientResult.StatusCode != HttpStatusCode.OK)
            {
                var message = await httpClientResult.Content.ReadAsStringAsync(cancellationToken);

                if (message is null)
                    return new UploadResponse("Erro ao realizar upload para S3. Detalhes não informado", HttpStatusCode.BadRequest.ToString());

                XmlSerializer serializer = new(typeof(ErrorUploadResponse));
                using StringReader reader = new(message);

                var resultHttpClientS3 = (ErrorUploadResponse?)serializer.Deserialize(reader);

                return new UploadResponse
                {
                    Message = resultHttpClientS3?.Message,
                    Error = resultHttpClientS3,
                    StatusCode = resultHttpClientS3?.Code ?? ""
                };
            }
            else
                return new UploadResponse("Upload realizado com sucesso!", httpClientResult.StatusCode.ToString());
        }

        internal static async Task<UploadResponse> UploadBase64ViaPresignedUrl(string url, string base64Content, CancellationToken cancellationToken)
        {
            var contentType = FileUtilities.GetContentTypeFromBase64(base64Content)
                ?? throw new ArgumentException("Formato do arquivo (MIME) não reconhecido.");

            var contentBodyBytes = FileUtilities.DecodeBase64(base64Content);

            using var memoryStream = new MemoryStream(contentBodyBytes);
            using var streamContent = new StreamContent(memoryStream);

            streamContent.Headers.ContentType =
                new MediaTypeHeaderValue(contentType);

            var httpClientResult = await _httpClient.PutAsync(url, streamContent, cancellationToken);

            if (httpClientResult.StatusCode != HttpStatusCode.OK)
            {
                var message = await httpClientResult.Content.ReadAsStringAsync();

                if (message is null)
                    return new UploadResponse("Erro ao realizar upload para S3. Detalhes não informado", HttpStatusCode.BadRequest.ToString());

                XmlSerializer serializer = new(typeof(ErrorUploadResponse));
                using StringReader reader = new(message);

                var resultHttpClientS3 = (ErrorUploadResponse?)serializer.Deserialize(reader);

                return new UploadResponse
                {
                    Message = resultHttpClientS3?.Message,
                    Error = resultHttpClientS3,
                    StatusCode = resultHttpClientS3?.Code ?? ""
                };
            }
            else
                return new UploadResponse("Upload realizado com sucesso!", httpClientResult.StatusCode.ToString());
        }

        internal static async Task<ConfirmUploadResponse> ConfirmUpload(ConfirmUploadClientRequest request, CancellationToken cancellationToken)
        {
            var confirmUpload = new ConfirmUploadResponse();
            var messages = new List<Information>();

            try
            {
                foreach (var key in request.DocumentKeys.Select(x => x.Key))
                {
                    var metadata = await client.GetObjectMetadataAsync(bucketName, key, cancellationToken);
                    if (metadata != null)
                    {
                        var rule = FileUtilities.GetRuleByDocumentType(metadata.ContentType);

                        if (rule is null)
                        {
                            messages.Add(new Information
                            {
                                Message = $"Regra não cadastrada para o tipo de arquivo {metadata.ContentType}"
                            });
                        }
                        else
                        {
                            if ((metadata.ContentLength > rule.MaxFileSizeBytes) || (metadata.Headers.ContentLength > rule.MaxFileSizeBytes))
                            {
                                messages.Add(new Information
                                {
                                    Message = $"Regra violada. Arquivo chave: '{key}' negado. Tamanho do arquivo: {metadata.ContentLength} / Tamanho permitido: {rule.MaxFileSizeBytes}"
                                });
                            }

                            if (metadata.ContentType != rule.ContentType)
                            {
                                messages.Add(new Information
                                {
                                    Message = $"Regra violada. Arquivo chave :  '{key}' negado. Tipo arquivo: {metadata.ContentType} / Tipo permitido: {rule.ContentType}"
                                });
                            }
                        }

                        var fileStatus = new FileStatus() { Key = key };
                        if (messages.Count > 0)
                            fileStatus.Messages = messages;
                        else
                        {
                            fileStatus.IsValid = true;
                            fileStatus.Messages.Add(new Information
                            {
                                Message = "Upload do arquivo confirmado com sucesso"
                            });
                        }
                        confirmUpload.Documents.Add(fileStatus);
                    }
                }
                return confirmUpload;
            }
            catch (AmazonS3Exception ex)
            {
                throw new AmazonS3Exception(ex);
            }
        }

        internal static async Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(CompleteMultipartUploadRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return await client.CompleteMultipartUploadAsync(request, cancellationToken);
            }
            catch (AmazonS3Exception ex)
            {
                throw new AmazonS3Exception(ex);
            }
        }

        internal static async Task<AbortMultipartResponseModel> AbortMultipartUploadAsync(string key, string uploadId, CancellationToken cancellationToken)
        {
            try
            {
                var response = await client.AbortMultipartUploadAsync(bucketName, key, uploadId, cancellationToken);

                if (response.HttpStatusCode != HttpStatusCode.NoContent)
                    return new AbortMultipartResponseModel($"Erro ao cancelar solicitação Upload. Key: {key}", false);

                return new AbortMultipartResponseModel($"Cancelamento Upload efetuado com sucesso. Key: {key}", true);
            }
            catch (AmazonS3Exception ex)
            {
                throw new AmazonS3Exception(ex);
            }
        }

        internal static async Task<IReadOnlyList<UploadResponse>> UploadViaPresignedUrlClient(UploadFileViaUrlClientRequest request, CancellationToken cancellationToken)
        {
            var uploadResponseList = new ConcurrentBag<UploadResponse>();

            UploadResponse? response = null;
            await Parallel.ForEachAsync(request.Documents,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 10
                },
                async (document, ct) =>
                {
                    response = await UploadViaPresignedUrl(document.Url, document.FilePath, cancellationToken);
                    uploadResponseList.Add(response);
                });

            return uploadResponseList.ToList();
        }

        internal static async Task<string> InitiateMultipartUploadAsync(string key, CancellationToken cancellationToken)
        {
            var response = await client.InitiateMultipartUploadAsync(
                    new InitiateMultipartUploadRequest
                    {
                        BucketName = bucketName,
                        Key = key
                    }, cancellationToken
            );
            return response.UploadId;
        }




        private static async Task<MultipartUploadModel> PrepareMultipartUploadAsync(string key, string filePath, int partSize, string uploadId, CancellationToken cancellationToken)
        {
            var parts = new List<UploadPart>();

            var fileInfo = new FileInfo(filePath);

            var partSizeBytes = CalculateSizeFileMbToByte(partSize);

            // Divide o arquivo na quantidade necessária de partes.
            var totalParts = (int)Math.Ceiling((double)fileInfo.Length / partSizeBytes);

            string contentType;
            using (var fileStream = File.OpenRead(filePath))
            {
                // Lê apenas o cabeçalho do arquivo para identificar o Content-DocumenType.
                var header = new byte[32];
                await fileStream.ReadExactlyAsync(header, cancellationToken);

                contentType = FileUtilities.GetContentTypeFromBytes(header)
                    ?? throw new ArgumentException("Formato do arquivo (MIME) não reconhecido.");
            }

            for (int part = 1; part <= totalParts; part++)
            {
                var offset = (part - 1) * partSizeBytes; // inicio da leitura do arquivo correspondente a parte

                var remaining = fileInfo.Length - offset;

                var length = Math.Min(partSizeBytes, remaining); // quantos bytes a ser lido a partir do offset

                var uploadFileDetail = new UploadPart
                {
                    PartNumber = part,
                    Offset = offset,
                    Length = length
                };
                parts.Add(uploadFileDetail);
            }

            return new MultipartUploadModel
            {
                Key = key,
                UploadId = uploadId,
                ContentType = contentType,
                PartSizeBytes = partSizeBytes,
                Parts = parts
            };
        }

        private static async Task<MultipartUploadModel> UploadPartsAsync(string filePath, MultipartUploadModel request, CancellationToken cancellationToken)
        {
            var uploadParts = new List<UploadPart>();

            await Parallel.ForEachAsync(request.Parts,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 30,
                CancellationToken = cancellationToken
            },
            async (uploadPart, ct) =>
            {
                using var fileStream = File.OpenRead(filePath);

                try
                {
                    uploadPart.Url = await S3Services.GeneratePresignedUrlMultipartToPutAsync(request.Key, request.ContentType, uploadPart.PartNumber, request.UploadId);

                    var uploadPartResult = await UploadPartAsync(fileStream, request.Key, request.UploadId, request.ContentType, uploadPart, cancellationToken);

                    uploadParts.Add(new UploadPart
                    {
                        Attempts = uploadPartResult.Attempts,
                        ETag = uploadPartResult.ETag,
                        Error = uploadPartResult.Error,
                        Length = uploadPartResult.Length,
                        Offset = uploadPartResult.Offset,
                        PartNumber = uploadPartResult.PartNumber,
                        Url = uploadPartResult.Url
                    });
                }
                catch (Exception ex)
                {
                    uploadPart.Error = ex;
                }
            });

            return new MultipartUploadModel
            {
                Key = request.Key,
                UploadId = request.UploadId,
                ContentType = request.ContentType,
                PartSizeBytes = request.PartSizeBytes,
                Parts = uploadParts
            };
        }

        private static async Task<string> UploadInternalPartAsync(FileStream fileStream, string contentType, string url, long offSet, long length, CancellationToken cancellationToken)
        {

            //Posiciona o ponteiro do arquivo no início da parte correspondente.
            fileStream.Seek(offSet, SeekOrigin.Begin);

            // Cria um buffer para armazenar a parte.
            var buffer = new byte[length];

            // Lê exatamente a quantidade de bytes da parte.
            await fileStream.ReadExactlyAsync(buffer, cancellationToken);

            // Cria o conteúdo HTTP enviado ao S3.
            using var content = new ByteArrayContent(buffer);

            if (!string.IsNullOrWhiteSpace(contentType))
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            using var response = await _httpClient.PutAsync(url, content, cancellationToken);

            if (!response.Headers.TryGetValues("ETag", out var etag))
                throw new InvalidOperationException(
                    "ETag não retornado pelo S3.");

            return etag?.Single() ?? "";
        }

        private static async Task<CompleteMultipartUploadRequest?> UploadPartAsync(Stream stream, int partSize, string key, CancellationToken cancellationToken)
        {
            GeneratePresignedUrlSequentialModel? multiPart = null;
            var partETagList = new ConcurrentBag<PartETag>();

            string? contentType = null;

            var partSizeBytes = CalculateSizeFileMbToByte(partSize);
            var buffer = ArrayPool<byte>.Shared.Rent((int)partSizeBytes);

            var partNumber = 1;

            var uploadId = await InitiateMultipartUploadAsync(key, cancellationToken);

            while (true)
            {
                var bytesRead = await FileUtilities.ReadPartAsync(stream, buffer, cancellationToken);

                if (bytesRead == 0)
                    break;

                if (partNumber == 1)
                    contentType = FileUtilities.GetContentTypeFromBytes(buffer.AsSpan(0, bytesRead).ToArray());

                if (contentType == null)
                    break;

                using var content = new ByteArrayContent(buffer, 0, bytesRead);

                multiPart = await GeneratePresignedUrlOnDemandToPutAsync(partNumber, key, uploadId, contentType);

                var partEtag = await UploadPartAsync(content, multiPart.Url, partNumber, contentType, cancellationToken);

                partETagList.Add(partEtag);
                partNumber++;
            }

            if (contentType == null)
                return null;

            return new CompleteMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = multiPart?.Key ?? "key não gerado!",
                UploadId = multiPart?.UploadId ?? "uploadId não gerado!",
                PartETags = partETagList.OrderBy(x => x.PartNumber).ToList(),
            };
        }

        private static async Task<PartETag> UploadPartAsync(ByteArrayContent content, Uri? url, int partNumber, string contentType, CancellationToken cancellationToken)
        {
            content.Headers.ContentType =
                new MediaTypeHeaderValue(contentType);

            using var response = await _httpClient.PutAsync(url, content, cancellationToken);

            response.EnsureSuccessStatusCode();

            if (!response.Headers.TryGetValues("ETag", out var etag))
                throw new InvalidOperationException(
                    "ETag não retornado pelo S3.");

            return new PartETag
            {
                ETag = etag.Single(),
                PartNumber = partNumber
            };
        }

        private static async Task<UploadPart> UploadPartAsync(FileStream fileStream, string key, string uploadId, string contentType, UploadPart uploadPart, CancellationToken cancellationToken)
        {
            const int maxRetries = 3;

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    uploadPart.Attempts = attempt;

                    // Se necessário, renova a URL
                    if (attempt > 1)
                        uploadPart.Url = await S3Services.GeneratePresignedUrlMultipartToPutAsync(key, contentType, uploadPart.PartNumber, uploadId);

                    var etag = await UploadInternalPartAsync(fileStream, contentType, uploadPart.Url.AbsoluteUri, uploadPart.Offset, uploadPart.Length, cancellationToken);
                    if (etag != null)
                    {
                        uploadPart.ETag = etag;
                        return uploadPart;
                    }
                }
                catch (Exception ex)
                {
                    uploadPart.Error = ex;

                    if (attempt == maxRetries)
                        throw;
                }
            }
            throw uploadPart.Error!;
        }

        private static async Task<GeneratePresignedUrlSequentialModel> GeneratePresignedUrlOnDemandToPutAsync(int partNumber, string key, string uploadId, string contentType)
        {
            GetPreSignedUrlRequest? request;
            Uri? urlParsed = null;

            try
            {
                request = new GetPreSignedUrlRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    Verb = HttpVerb.PUT,
                    UploadId = uploadId,
                    PartNumber = partNumber,
                    Expires = DateTime.Now.AddMinutes(5),
                    ContentType = contentType
                };
                var url = await client.GetPreSignedURLAsync(request);

                if (Uri.TryCreate(url, UriKind.Absolute, out Uri? myUri))
                    urlParsed = myUri;
            }
            catch (AmazonS3Exception ex)
            {
                throw new AmazonS3Exception("Erro ao obter url pré assinada S3", ex);
            }

            return new GeneratePresignedUrlSequentialModel
            {
                UploadId = uploadId,
                Key = request!.Key,
                Url = urlParsed,
            };
        }

        private static long CalculateSizeFileMbToByte(int partSizeMb)
        {
            var kbytes = partSizeMb * 1024;
            var bytes = kbytes * 1024;
            return bytes;
        }
    }
}
