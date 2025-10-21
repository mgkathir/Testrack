
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BureauAdaptor.Helper
{
    public class JsonRequestResponse
    {
        public JsonRequestResponse(String rawdata)
        {
            payload = rawdata;
        }
        public String payload { get; set; }
    }
}
