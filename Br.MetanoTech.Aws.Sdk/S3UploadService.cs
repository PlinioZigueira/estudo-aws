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

        internal static async Task<UploadMultipartResponse> UploadAsync(string filePath, int partSize, int maxConcurrency, CancellationToken cancellationToken = default)
        {
            try
            {
                var multiPartFile = await InitializeMultipartAsync(filePath, partSize, cancellationToken);

                var uploadPending = await UploadPartsAsync(multiPartFile, maxConcurrency, cancellationToken);

                var completedMultiparUploadResult = await CompleteMultipartUploadAsync(uploadPending);

                if (completedMultiparUploadResult.HttpStatusCode != HttpStatusCode.OK)
                {
                    var abortSuccess = await AbortMultipartUploadAsync(uploadPending.Key, uploadPending.UploadId);

                    if (!abortSuccess.Success)
                        return new UploadMultipartResponse("Ocorreu erro ao realizar e cancelar a solicitação de upload do arquivo.", HttpStatusCode.InternalServerError, multiPartFile.Key);
                    else
                        return new UploadMultipartResponse("Erro ao realizar Upload do arquivo. Upload cancelado com sucesso.", HttpStatusCode.BadRequest, multiPartFile.Key);
                }
                else
                    return new UploadMultipartResponse("Upload realizado com sucesso.", HttpStatusCode.OK, uploadPending.Key);
            }
            catch (AmazonS3Exception ex)
            {
                throw new AmazonS3Exception(ex);
            }
        }

        internal static async Task<UploadMultipartResponse> UploadFileStreamAsync(Stream stream, int partSize, string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var uploadPending = await UploadPartAsync(stream, partSize, key, cancellationToken);

                if (uploadPending is null)
                    return new UploadMultipartResponse("Erro  ao relizar upload  do arquivo.", HttpStatusCode.BadRequest, key);

                var completedMultiparUploadResult = await CompleteMultipartUploadAsync(uploadPending);

                if (completedMultiparUploadResult.HttpStatusCode != HttpStatusCode.OK)
                {
                    var abortSuccess = await AbortMultipartUploadAsync(uploadPending.Key, uploadPending.UploadId);

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

        internal static async Task<UploadResponse> UploadViaPresignedUrl(string url, string filePath, string key)
        {
            using var streamContent = await FileUtilities.ConvertFileToStreamAsync(filePath);
            var httpClientResult = await _httpClient.PutAsync(url, streamContent);

            if (httpClientResult.StatusCode != HttpStatusCode.OK)
            {
                var message = await httpClientResult.Content.ReadAsStringAsync();

                if (message is null)
                    return new UploadResponse("Erro ao realizar upload para S3. Detalhes não informado", HttpStatusCode.BadRequest.ToString(), key);

                XmlSerializer serializer = new(typeof(ErrorUploadResponse));
                using StringReader reader = new(message);

                var resultHttpClientS3 = (ErrorUploadResponse?)serializer.Deserialize(reader);

                return new UploadResponse
                {
                    Message = resultHttpClientS3?.Message,
                    //Url = url,
                    Error = resultHttpClientS3,
                    StatusCode = resultHttpClientS3?.Code ?? "",
                    Key = key
                };
            }
            else
                return new UploadResponse("Upload realizado com sucesso!", httpClientResult.StatusCode.ToString(), key);
        }

        internal static async Task<UploadResponse> UploadBase64ViaPresignedUrl(string url, string base64Content, string key)
        {
            var contentType = FileUtilities.GetContentTypeFromBase64(base64Content)
                ?? throw new ArgumentException("Formato do arquivo (MIME) não reconhecido.");

            var contentBodyBytes = FileUtilities.DecodeBase64(base64Content);

            using var memoryStream = new MemoryStream(contentBodyBytes);
            using var streamContent = new StreamContent(memoryStream);

            streamContent.Headers.ContentType =
                new MediaTypeHeaderValue(contentType);

            var httpClientResult = await _httpClient.PutAsync(url, streamContent);

            if (httpClientResult.StatusCode != HttpStatusCode.OK)
            {
                var message = await httpClientResult.Content.ReadAsStringAsync();

                if (message is null)
                    return new UploadResponse("Erro ao realizar upload para S3. Detalhes não informado", HttpStatusCode.BadRequest.ToString(), key);

                XmlSerializer serializer = new(typeof(ErrorUploadResponse));
                using StringReader reader = new(message);

                var resultHttpClientS3 = (ErrorUploadResponse?)serializer.Deserialize(reader);

                return new UploadResponse
                {
                    Message = resultHttpClientS3?.Message,
                    //Url = url,
                    Error = resultHttpClientS3,
                    StatusCode = resultHttpClientS3?.Code ?? "",
                    Key = key
                };
            }
            else
                return new UploadResponse("Upload realizado com sucesso!", httpClientResult.StatusCode.ToString(), key);
        }

        internal static async Task<string> InitiateMultipartUploadAsync(string key)
        {
            var response = await client.InitiateMultipartUploadAsync(
                    new InitiateMultipartUploadRequest
                    {
                        BucketName = bucketName,
                        Key = key
                    }
            );
            return response.UploadId;
        }

        internal static async Task<ConfirmUploadResponse> ConfirmUpload(ConfirmUploadClientRequest request)
        {   
            var confirmUpload = new ConfirmUploadResponse();
            var messages = new List<Information>();

            try
            {
                foreach (var key in request.DocumentKeys.Select(x => x.Key))
                {
                    var metadata = await client.GetObjectMetadataAsync(bucketName, key);
                    if (metadata != null)
                    {
                        var rule = FileUtilities.GetRuleByDocumentType(metadata.ContentType);

                        if (rule is null)
                        {
                            messages.Add(new Information {
                                Message = $"Regra não cadastrada para o tipo de arquivo {metadata.ContentType}"
                            });
                        }
                        else
                        {
                            if ((metadata.ContentLength > rule.MaxFileSizeBytes) || (metadata.Headers.ContentLength > rule.MaxFileSizeBytes))
                            {
                                messages.Add(new Information {
                                    Message = $"Regra violada. Arquivo chave: '{key}' negado. Tamanho do arquivo: {metadata.ContentLength} / Tamanho permitido: {rule.MaxFileSizeBytes}"
                                });
                            }

                            if (metadata.ContentType != rule.ContentType)
                            {
                                messages.Add(new Information {
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

        internal static async Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(CompleteMultipartUploadRequest request)
        {
            try
            {
                return await client.CompleteMultipartUploadAsync(request);
            }
            catch (AmazonS3Exception ex)
            {
                throw new AmazonS3Exception(ex);
            }
        }

        internal static async Task<AbortMultipartResponseModel> AbortMultipartUploadAsync(string key, string uploadId)
        {
            try
            {
                var response = await client.AbortMultipartUploadAsync(bucketName, key, uploadId);

                if (response.HttpStatusCode != HttpStatusCode.NoContent)
                    return new AbortMultipartResponseModel($"Erro ao cancelar solicitação Upload. Key: {key}", false);

                return new AbortMultipartResponseModel($"Cancelamento Upload efetuado com sucesso. Key: {key}", true);
            }
            catch (AmazonS3Exception ex)
            {
                throw new AmazonS3Exception(ex);
            }
        }

        internal static async Task<IReadOnlyList<UploadResponse>> UploadViaPresignedUrlClient(UploadFileViaUrlClientRequest request)
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
                    response = await UploadViaPresignedUrl(document.Url, document.FilePath, document.Key);
                    uploadResponseList.Add(response);
                });

            return uploadResponseList.ToList();
        }

        private static async Task<MultipartUploadRequestModel> InitializeMultipartAsync(string filePath, int partSize, CancellationToken cancellationToken)
        {
            var fileInfo = new FileInfo(filePath);

            var partSizeBytes = CalculateSizeFileMbToByte(partSize);

            // Divide o arquivo na quantidade necessária de partes.
            var totalParts = (int)Math.Ceiling((double)fileInfo.Length / partSizeBytes);

            using var stream = File.OpenRead(filePath);

            // Lê apenas o cabeçalho do arquivo para identificar o Content-DocumenType.
            var header = new byte[32];
            await stream.ReadExactlyAsync(header, cancellationToken);

            var contentType = FileUtilities.GetContentTypeFromBytes(header)
                ?? throw new ArgumentException("Formato do arquivo (MIME) não reconhecido.");

            // Solicita ao serviço as URLs pré-assinadas.
            var multipart = await S3Services.GeneratePresignedUrlToPutAsync(totalParts, contentType);

            return new MultipartUploadRequestModel
            {
                UploadId = multipart.UploadId,
                Key = multipart.Key,
                Urls = multipart.Urls,
                PartSize = partSizeBytes,
                ContentType = contentType,
                FilePath = filePath,
                TotalParts = totalParts,
            };
        }

        private static async Task<CompleteMultipartUploadRequest> UploadPartsAsync(MultipartUploadRequestModel request, int maxConcurrency, CancellationToken cancellationToken)
        {
            var partETagList = new ConcurrentBag<PartETag>();

            await Parallel.ForEachAsync(
                Enumerable.Range(1, request.TotalParts),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxConcurrency,
                    CancellationToken = cancellationToken
                },
                async (partNumber, ct) =>
                {
                    var result = await UploadPartAsync(request, partNumber, ct);
                    partETagList.Add(result);
                });

            return new CompleteMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = request.Key,
                UploadId = request.UploadId,
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

        private static async Task<PartETag> UploadPartAsync(MultipartUploadRequestModel request, int partNumber, CancellationToken cancellationToken)
        {
            using var fileStream = File.OpenRead(request.FilePath);

            // Calcula o ponto inicial da parte no arquivo.
            var offset = (long)(partNumber - 1) * request.PartSize;

            // Posiciona o ponteiro do arquivo no início da parte.
            fileStream.Seek(offset, SeekOrigin.Begin);

            // Calcula quantos bytes ainda restam.
            var remaining = fileStream.Length - offset;

            // Define o tamanho da leitura (a última parte pode ser menor).
            var size = (int)Math.Min(request.PartSize, remaining);

            // Cria um buffer para armazenar a parte.
            var buffer = new byte[size];

            // Lê exatamente a quantidade de bytes da parte.
            await fileStream.ReadExactlyAsync(buffer, cancellationToken);

            // Cria o conteúdo HTTP enviado ao S3.
            using var content = new ByteArrayContent(buffer);

            if (!string.IsNullOrWhiteSpace(request.ContentType))
                content.Headers.ContentType = new MediaTypeHeaderValue(request.ContentType);

            // Obtém a URL correspondente a parte.
            var url = request.Urls[partNumber - 1];

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

        private static async Task<CompleteMultipartUploadRequest?> UploadPartAsync(Stream stream, int partSize, string key, CancellationToken cancellationToken)
        {
            GeneratePresignedUrlSequentialModel? multiPart = null;
            var partETagList = new ConcurrentBag<PartETag>();

            string? contentType = null;

            var partSizeBytes = CalculateSizeFileMbToByte(partSize);
            var buffer = ArrayPool<byte>.Shared.Rent(partSizeBytes);

            var partNumber = 1;

            var uploadId = await InitiateMultipartUploadAsync(key);

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

                multiPart = await GeneratePresignedUrlOnDemandToPutAsync(partNumber, key, uploadId);

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

        private static async Task<GeneratePresignedUrlSequentialModel> GeneratePresignedUrlOnDemandToPutAsync(int partNumber, string key, string uploadId)
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
                    //ContentType 
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

        private static int CalculateSizeFileMbToByte(int partSizeMb)
        {
            var kbytes = partSizeMb * 1024;
            var bytes = kbytes * 1024;
            return bytes;
        }
    }
}
