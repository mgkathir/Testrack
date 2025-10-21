using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BureauAdaptor.Helper
{
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.netaccess.transunion.com/namespace")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.netaccess.transunion.com/namespace", IsNullable = false)]
    public class xmlrequest
    {
        public string? systemId { get; set; }
        public string? systemPassword { get; set; }
        public string? EventDescription { get; set; }
        public string? processingEnvironment { get; set; }
        public string? productrequest { get; set; }
    }
}
