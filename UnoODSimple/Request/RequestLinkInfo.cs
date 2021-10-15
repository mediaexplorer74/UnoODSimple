using Newtonsoft.Json;

namespace UnoODSimple.Request
{
    public class RequestLinkInfo
    {
        [JsonProperty("type")]
        public string Type
        {
            get;
            set;
        }
    }
}