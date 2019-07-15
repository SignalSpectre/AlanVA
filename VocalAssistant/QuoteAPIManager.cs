using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.IO;


namespace VocalAssistant
{
    public class QuoteAPIManager
    {
        public async static Task<QuoteObject> GetQuote()
        {
            var http = new HttpClient();
            var response = await http.GetAsync("https://quotes.rest/qod.json");
            string response_str = await response.Content.ReadAsStringAsync();
            var serializer = new DataContractJsonSerializer(typeof(QuoteObject)); // JSON object to C# class transformer
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(response_str));
            return (QuoteObject)serializer.ReadObject(stream);
        }
    }

    [DataContract] // Says to serializer: treat this element as class
    public class Success
    {
        [DataMember] // Says to serializer: treat this element as class's field
        public int total { get; set; }
    }

    [DataContract]
    public class Quote
    {
        [DataMember]
        public string quote { get; set; }

        [DataMember]
        public string length { get; set; }

        [DataMember]
        public string author { get; set; }

        [DataMember]
        public List<string> tags { get; set; }

        [DataMember]
        public string category { get; set; }

        [DataMember]
        public string date { get; set; }

        [DataMember]
        public string title { get; set; }

        [DataMember]
        public string background { get; set; }

        [DataMember]
        public string id { get; set; }
    }

    [DataContract]
    public class Contents
    {
        [DataMember]
        public List<Quote> quotes { get; set; }
    }

    [DataContract]
    public class QuoteObject
    {
        [DataMember]
        public Success success { get; set; }

        [DataMember]
        public Contents contents { get; set; }
    }
}