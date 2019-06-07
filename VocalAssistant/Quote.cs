using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Runtime.Serialization;

namespace VocalAssistant
{
    public class QuoteMapProxy
    {
        public async static Task<QuoteObject> GetQuote()
        {
            var http = new HttpClient();
            var response = await http.GetAsync("https://quotes.rest/qod.json");
            var result = await response.Content.ReadAsStringAsync();
            var serializer = new DataContractJsonSerializer(typeof(QuoteObject));
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(result));
            var data = (QuoteObject)serializer.ReadObject(ms);

            return data;
        }
    }

    [DataContract]
    public class Success
    {
        [DataMember]
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
