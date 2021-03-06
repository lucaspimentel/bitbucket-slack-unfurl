using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace CcSlack.Shared.Slack
{
    public class SlackClient
    {
        private const string BaseUrl = "https://slack.com/api";

        /*
        public async Task Authorize()
        {
        }
        */

        public async Task<dynamic> PostToSlack(string endpoint, IDictionary<string, string> data)
        {
            StringContent urlEncodedData = CreateUrlEncodedContent(data);

            using (var httpClient = new HttpClient())
            {
                HttpResponseMessage postResponse = await httpClient.PostAsync($"{BaseUrl}/{endpoint}", urlEncodedData);
                postResponse.EnsureSuccessStatusCode();

                dynamic result = await postResponse.Content.ReadAsAsync<dynamic>();
                return result;
            }
        }

        public static StringContent CreateUrlEncodedContent(IEnumerable<KeyValuePair<string, string>> data)
        {
            string urlEncodedData = UrlEncode(data);
            return new StringContent(urlEncodedData, Encoding.UTF8, "application/x-www-form-urlencoded");
        }

        public static string UrlEncode(IEnumerable<KeyValuePair<string, string>> data)
        {
            return string.Join("&", data.Select(kv => $"{kv.Key}={HttpUtility.UrlEncode(kv.Value)}"));
        }
    }
}