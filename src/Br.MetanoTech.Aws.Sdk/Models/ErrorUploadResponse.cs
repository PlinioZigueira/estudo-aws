using System.Xml.Serialization;

namespace Br.MetanoTech.Aws.Sdk.Models
{
    [XmlRoot(ElementName = "Error")]
    public class ErrorUploadResponse
    {
        [XmlElement(ElementName = "Code")]
        public string? Code { get; set; }

        [XmlElement(ElementName = "Information")]
        public string? Message { get; set; }

        [XmlElement(ElementName = "X-Amz-Expires")]
        public int XAmzExpires { get; set; }

        [XmlElement(ElementName = "Expires")]
        public DateTime Expires { get; set; }

        [XmlElement(ElementName = "ServerTime")]
        public DateTime ServerTime { get; set; }

        [XmlElement(ElementName = "RequestId")]
        public string? RequestId { get; set; }

        [XmlElement(ElementName = "HostId")]
        public string? HostId { get; set; }
    }
}