using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Br.MetanoTech.Aws.Sdk.Helpers;
using Br.MetanoTech.Aws.Sdk.Models;
using System.Data;

namespace Br.MetanoTech.Aws.Sdk
{
    public static class S3Services
    {
        private static readonly string bucketName = "dev-lab-local";
        private static readonly AmazonS3Client client = new(RegionEndpoint.USEast1);

        public static async Task<GeneratePresignedUrlModel> GeneratePresignedUrlToPutAsync()
        {
            GetPreSignedUrlRequest? request;
            string url;

            try
            {
                request = new GetPreSignedUrlRequest
                {
                    BucketName = bucketName,
                    Key = CreateRandomKey(),
                    Verb = HttpVerb.PUT,
                    Expires = DateTime.Now.AddMinutes(5)
                };
                url = await client.GetPreSignedURLAsync(request);
            }
            catch (AmazonS3Exception ex)
            {
                Console.WriteLine($"Error:'{ex.Message}'");
                throw;
            }

            return new GeneratePresignedUrlModel
            {
                Key = request!.Key,
                Url = url
            };
        }

        public static async Task<GeneratePresignedUrlMultiPartUploadModel> GeneratePresignedUrlToPutAsync(int totalParts, string contentType)
        {
            GetPreSignedUrlRequest? request = null;
            List<Uri>? urls = [];
            string uploadId;

            var key = CreateRandomKey();

            try
            {
                uploadId = await S3UploadService.InitiateMultipartUploadAsync(key);

                for (int partNumberId = 1; partNumberId <= totalParts; partNumberId++)
                {
                    request = new GetPreSignedUrlRequest
                    {
                        BucketName = bucketName,
                        Key = key,
                        Verb = HttpVerb.PUT,
                        UploadId = uploadId,
                        PartNumber = partNumberId,
                        Expires = DateTime.Now.AddMinutes(5),
                        ContentType = contentType
                    };
                    var url = await client.GetPreSignedURLAsync(request);

                    if (Uri.TryCreate(url, UriKind.Absolute, out Uri myUri))
                        urls.Add(myUri);
                }
            }
            catch (AmazonS3Exception ex)
            {
                Console.WriteLine($"Error:'{ex.Message}'");
                throw;
            }

            return new GeneratePresignedUrlMultiPartUploadModel
            {
                UploadId = uploadId,
                Key = request!.Key,
                Urls = urls
            };
        }

        public static async Task<UploadMultipartResponse> UploadFileAsync(string filePath, int partSize, int maxConcurrency = 30)
            => await S3UploadService.UploadAsync(filePath, partSize, maxConcurrency);

        public static async Task<UploadMultipartResponse> UploadFileStreamAsync(Stream stream, int partSize)
            => await S3UploadService.UploadFileStreamAsync(stream, partSize, CreateRandomKey());

        public static async Task<UploadResponse> UploadViaPresignedUrl(string url, string filePath, string key)
            => await S3UploadService.UploadViaPresignedUrl(url, filePath, key);

        public static async Task<UploadResponse> UploadBase64ViaPresignedUrl(string url, string base64Content, string key)
            => await S3UploadService.UploadBase64ViaPresignedUrl(url, base64Content, key);

        public static async Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(CompleteMultipartUploadRequest request)
            => await S3UploadService.CompleteMultipartUploadAsync(request);

        public static async Task<AbortMultipartResponseModel> AbortMultipartUploadAsync(string key, string uploadId)
            => await S3UploadService.AbortMultipartUploadAsync(key, uploadId);

        internal static string CreateRandomKey()
        {
            var guid = string.Concat(RandomNumber(), Guid.NewGuid().ToString());

            var newGuid = Shuffle(guid);

            return newGuid;
        }

        internal static string RandomNumber()
        {
            Random random = new();

            char[] chars = new char[6];

            for (int i = 0; i < 6; i++)
            {
                chars[i] = (char)('0' + random.Next(0, 6));
            }
            return new string(chars);
        }

        internal static string Shuffle(string value)
        {
            Random random = new();

            if (string.IsNullOrEmpty(value)) return value;

            var array = value.ToCharArray();
            int n = array.Length;

            for (int i = n - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
            return new string(array);
        }

        //Client
        public static async Task<GeneratePresignedUrlClientResponseModel> GeneratePresignedUrlToPutClientAsync(GenerateUploadUrlClientRequest request)
        {
            var documents = new List<PreSignedUrl>();
            try
            {
                foreach (var type in request.Documents.Select(x => x.DocumenType))
                {
                    _ = FileUtilities.GetRuleByDocumentType(type)
                        ?? throw new ArgumentException($"Regra não cadastrada para o tipo de documento {type}");
                }

                foreach (var item in request.Documents)
                {
                    var contenType = FileUtilities.GetContentType(item.DocumenType);
                    var rule = FileUtilities.GetRuleByDocumentType(item.DocumenType);

                    var key = CreateRandomKey();
                    var preSignedUrlRequest = new GetPreSignedUrlRequest
                    {
                        BucketName = bucketName,
                        Key = key,
                        Verb = HttpVerb.PUT,
                        Expires = DateTime.UtcNow.AddMinutes(rule!.ExpiresInMinutes),
                        ContentType = contenType
                    };
                    var url = await client.GetPreSignedURLAsync(preSignedUrlRequest);

                    documents.Add(new PreSignedUrl
                    {
                        Key = key,
                        FileName = item.FileName,
                        Url = url,
                        Rule = rule
                    });
                }
            }
            catch (AmazonS3Exception ex)
            {
                Console.WriteLine($"Error:'{ex.Message}'");
                throw;
            }

            return new GeneratePresignedUrlClientResponseModel
            {
                PreSignedUrls = documents
            };
        }

        public static async Task<IReadOnlyList<UploadResponse>> UploadViaPresignedUrlClient(UploadFileViaUrlClientRequest request)
            => await S3UploadService.UploadViaPresignedUrlClient(request);

        public static async Task<ConfirmUploadResponse> ConfirmUpload(ConfirmUploadClientRequest request)
            => await S3UploadService.ConfirmUpload(request);
    }
}