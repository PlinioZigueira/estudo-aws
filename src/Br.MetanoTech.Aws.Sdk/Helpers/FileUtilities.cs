using Br.MetanoTech.Aws.Sdk.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Br.MetanoTech.Aws.Sdk.Helpers
{
    public static class FileUtilities
    {
        /// <summary>
        /// Validate and convert bytes to base64.
        /// </summary>
        /// <param name="data"></param>
        /// <returns>Returns base64</returns>
        public static string? NormalizeToBase64(byte[]? data)
        {
            if (data == null)
                return null;

            string text = Encoding.UTF8.GetString(data).Trim();

            if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var commaIndex = text.IndexOf(',');
                if (commaIndex > 0)
                {
                    string base64Part = text[(commaIndex + 1)..]
                        .Replace("\n", "")
                        .Replace("\r", "")
                        .Replace(" ", "")
                        .Replace("\t", "");

                    if (Convert.TryFromBase64String(base64Part, new Span<byte>(new byte[base64Part.Length]), out _))
                        return base64Part;
                }
            }

            string cleaned = text
                .Replace("\n", "")
                .Replace("\r", "")
                .Replace(" ", "")
                .Replace("\t", "");

            if (Convert.TryFromBase64String(cleaned, new Span<byte>(new byte[cleaned.Length]), out _))
                return cleaned;

            return Convert.ToBase64String(data);
        }

        /// <summary>
        /// Detects the MIME type from the file header bytes.
        /// </summary>
        /// <param name="base64">The file base64.</param>
        /// <returns>
        /// The detected MIME type, or <c>null</c> if it cannot be determined
        /// </returns>
        public static string? GetContentTypeFromBase64(string? base64)
        {
            if (base64 == null)
                return null;

            base64 = RemoveDataUriPrefix(base64);

            byte[] bytes;

            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch
            {
                return null; // invalid base64
            }
            string header = BitConverter.ToString(bytes.Take(12).ToArray()).Replace("-", " ");

            return GetImageContentType(header)
                ?? GetDocumentContentType(header, bytes)
                ?? GetOfficeContentType(header, bytes)
                ?? GetAudioVideoContentType(header, bytes)
                ?? GetCompressedContentType(header);
        }

        /// <summary>
        /// Detects the MIME type from the file header bytes.
        /// </summary>
        /// <param name="bytes">The file header bytes.</param>
        /// <returns>
        /// The detected MIME type, or <c>null</c> if it cannot be determined
        /// </returns>
        public static string? GetContentTypeFromBytes(byte[]? bytes)
        {
            if (bytes == null)
                return null;

            string header = BitConverter.ToString(bytes.Take(12).ToArray()).Replace("-", " ");

            return GetImageContentType(header)
                ?? GetDocumentContentType(header, bytes)
                ?? GetOfficeContentType(header, bytes)
                ?? GetAudioVideoContentType(header, bytes)
                ?? GetCompressedContentType(header);
        }

        /// <summary>
        /// Detects the MIME type from the document type
        /// </summary>
        /// <param name="documenType">The  document type.</param>
        /// <returns>
        /// The detected MIME type, or <c>null</c> if it cannot be determined
        /// </returns>
        public static string GetContentType(UploadDocumenType documenType)
        {
            return documenType switch
            {
                // Images
                UploadDocumenType.Png => "image/png",
                UploadDocumenType.Jpeg => "image/jpeg",
                UploadDocumenType.Gif => "image/gif",
                UploadDocumenType.Bmp => "image/bmp",
                UploadDocumenType.Tiff => "image/tiff",

                // DocumentKeys
                UploadDocumenType.Pdf => "application/pdf",
                UploadDocumenType.Rtf => "application/rtf",
                UploadDocumenType.Xml => "application/xml",
                UploadDocumenType.Json => "application/json",
                UploadDocumenType.Csv => "text/csv",
                UploadDocumenType.Txt => "text/plain",

                // Microsoft Office (OpenXML)
                UploadDocumenType.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                UploadDocumenType.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                UploadDocumenType.Pptx => "application/vnd.openxmlformats-officedocument.presentationml.presentation",

                // Compressed
                UploadDocumenType.Zip => "application/zip",
                UploadDocumenType.Rar => "application/x-rar-compressed",
                UploadDocumenType.SevenZip => "application/x-7z-compressed",

                // Audio
                UploadDocumenType.Mp3 => "audio/mpeg",
                UploadDocumenType.Wav => "audio/wav",

                // Video
                UploadDocumenType.Mp4 => "video/mp4",
                UploadDocumenType.Webm => "video/webm",

                _ => throw new ArgumentOutOfRangeException(nameof(documenType), documenType, "Tipo de documento não suportado.")
            };
        }

        /// <summary>
        /// Detects the MIME type from the document type
        /// </summary>
        /// <param name="documenType">The  document type.</param>
        /// <returns>
        /// The detected MIME type, or <c>null</c> if it cannot be determined
        /// </returns>
        public static UploadRule? GetRuleByDocumentType(UploadDocumenType documenType)
        {
            string jsonRules;

            switch (documenType)
            {
                case UploadDocumenType.Png:
                    jsonRules = """{"ContentType":"image/png", "MaxFileSizeBytes":20971520,"ExpiresInMinutes":5}"""; //json
                    break;
                case UploadDocumenType.Jpeg:
                    jsonRules = """{"ContentType":"image/jpeg", "MaxFileSizeBytes":20971520,"ExpiresInMinutes":5}"""; //json
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(documenType), documenType, "Tipo de documento não suportado.");
            }
            return JsonSerializer.Deserialize<UploadRule?>(jsonRules) ?? null;
        }

        public static UploadRule? GetRuleByDocumentType(string documenType)
        {
            string jsonRules;

            switch (documenType)
            {
                case "image/png":
                    jsonRules = """{"ContentType":"image/png", "MaxFileSizeBytes":20971520,"ExpiresInMinutes":5}""";
                    break;
                case "image/jpeg":
                    jsonRules = """{"ContentType":"image/jpeg", "MaxFileSizeBytes":20971520,"ExpiresInMinutes":5}""";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(documenType), documenType, "Tipo de documento não suportado.");
            }
            return JsonSerializer.Deserialize<UploadRule?>(jsonRules) ?? null;
        }


        /// <summary>
        /// Decode base64 to bytes
        /// </summary>
        /// <param name="base64"></param>
        /// <returns>return base64 to byte</returns>
        public static byte[] DecodeBase64(string base64)
        {
            byte[] bytes;

            try
            {
                base64 = RemoveDataUriPrefix(base64);
                bytes = Convert.FromBase64String(base64);
            }
            catch
            {
                base64 = base64
                    .Replace("\r", "")
                    .Replace("\n", "")
                    .Replace(" ", "");

                int padding = 4 - (base64.Length % 4);

                if (padding < 4)
                    base64 = base64.PadRight(base64.Length + padding, '=');

                bytes = Convert.FromBase64String(base64);
            }
            return bytes;
        }

        /// <summary>
        ///  Removes prefix Uri.
        /// </summary>
        /// <param name="base64"></param>
        /// <returns>Returns the clean base64</returns>
        private static string RemoveDataUriPrefix(string base64)
        {
            int index = base64.IndexOf(',');
            if (index >= 0)
                base64 = base64[(index + 1)..].Trim();

            return base64;
        }

        private static bool IsMostlyText(byte[] bytes)
        {
            int printable = bytes.Count(b => b == 9 || b == 10 || b == 13 || (b >= 32 && b <= 126));
            double ratio = (double)printable / bytes.Length;
            return ratio > 0.9; // 90% text
        }

        public static async Task<StreamContent> ConvertFileToStreamAsync(string filePath)
        {
            var fileStream = File.OpenRead(filePath);
            var header = await FileUtilities.ReadHeaderBytesAsync(fileStream);

            fileStream.Seek(0, SeekOrigin.Begin);

            var content = new StreamContent(fileStream);

            var contentType = FileUtilities.GetContentTypeFromBytes(header)
            ?? throw new ArgumentException("Formato do arquivo (MIME) não reconhecido.");

            content.Headers.ContentType =
                new MediaTypeHeaderValue(contentType);

            return content;
        }

        public static async Task<byte[]> ReadHeaderBytesAsync(Stream stream, int tamanho = 256)
        {
            byte[] buffer = new byte[tamanho];

            // Lê no máximo 256 bytes do início do fluxo
            int bytesLidos = await stream.ReadAsync(buffer, 0, buffer.Length);

            // Se o arquivo for menor que 256 bytes, ajusta o array para o tamanho real
            if (bytesLidos < buffer.Length)
                Array.Resize(ref buffer, bytesLidos);

            stream.Seek(0, SeekOrigin.Begin);

            return buffer;
        }

        public static async Task<int> ReadPartAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var bytesRead = await stream.ReadAsync(
                    buffer[totalRead..],
                    cancellationToken);

                if (bytesRead == 0)
                    break;

                totalRead += bytesRead;
            }
            return totalRead;
        }

        private static string? GetImageContentType(string header)
        {
            foreach (var (signature, contentType) in ImageHeaders)
            {
                if (header.StartsWith(signature))
                    return contentType;
            }
            return null;
        }

        private static string? GetDocumentContentType(string header, byte[] bytes)
        {
            string ascii = Encoding.ASCII.GetString(bytes);
            string asciitrimmed = ascii.TrimStart();
            string utf8 = Encoding.UTF8.GetString(bytes);
            bool isText = IsMostlyText(bytes);

            if (header.StartsWith("25 50 44 46"))
                return "application/pdf";

            if (ascii.StartsWith(@"{\rtf"))
                return "application/rtf";

            // XML
            if (asciitrimmed.StartsWith('<'))
                return "application/xml";

            // JSON
            if (asciitrimmed.StartsWith('{') ||
                asciitrimmed.StartsWith('['))
                return "application/json";

            // CSV
            if (isText && utf8.Contains(','))
                return "text/csv";

            // TXT
            if (isText)
                return "text/plain";

            return null;
        }

        private static string? GetOfficeContentType(string header, byte[] bytes)
        {
            if (header.StartsWith("50 4B 03 04"))
            {
                string content = Encoding.ASCII.GetString(bytes);

                if (content.Contains("word/"))
                    return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";  // DOCX

                if (content.Contains("xl/"))
                    return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";        // XLSX

                if (content.Contains("ppt/"))
                    return "application/vnd.openxmlformats-officedocument.presentationml.presentation"; // PPTX

                return "application/zip";
            }
            else
                return null;
        }

        private static string? GetAudioVideoContentType(string header, byte[] bytes)
        {
            if (header.StartsWith("49 44 33"))
                return "audio/mpeg";

            if (header.StartsWith("1A 45 DF A3"))
                return "video/webm";

            if (bytes.Length >= 12 && header.StartsWith("52 49 46 46") && Encoding.ASCII.GetString(bytes, 8, 4) == "WAVE")
                return "audio/wav";

            if (bytes.Length >= 8 && header.StartsWith("00 00 00") && Encoding.ASCII.GetString(bytes, 4, 4) == "ftyp")
                return "video/mp4";

            return null;
        }

        private static string? GetCompressedContentType(string header)
        {
            foreach (var (signature, contentType) in CompressedHeaders)
            {
                if (header.StartsWith(signature))
                    return contentType;
            }
            return null;
        }

        private static readonly Dictionary<string, string> ImageHeaders = new()
        {
            ["89 50 4E 47"] = "image/png",
            ["FF D8 FF"] = "image/jpeg",
            ["47 49 46 38"] = "image/gif",
            ["42 4D"] = "image/bmp",
            ["49 49 2A 00"] = "image/tiff",
            ["4D 4D 00 2A"] = "image/tiff"
        };

        private static readonly Dictionary<string, string> CompressedHeaders = new()
        {
            ["52 61 72 21"] = "application/x-rar-compressed",
            ["37 7A BC AF 27 1C"] = "application/x-7z-compressed",
        };
    }
}
