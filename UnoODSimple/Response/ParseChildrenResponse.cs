using System.Collections.Generic;
using Newtonsoft.Json;

namespace UnoODSimple.Response
{
    public class ParseChildrenResponse
    {
        public IList<ItemInfoResponse> Value
        {
            get;
            set;
        }

        [JsonProperty("@odata.nextLink")]
        public string NextLink
        {
            get;
            set;
        }
    }
}