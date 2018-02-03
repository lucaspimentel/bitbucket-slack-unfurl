using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using CcSlack.Shared.Slack;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace CcSlack.Functions
{
    public static class SlackEvent
    {
        [FunctionName("SlackEvent")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slack/event")]
                                                          HttpRequestMessage req,
                                                          TraceWriter log)
        {
            /*
            log.Info("C# HTTP trigger function processed a request.");

            // parse query parameter
            string name = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "name", true) == 0)
                .Value;

            // Get request body
            dynamic data = await req.Content.ReadAsAsync<object>();

            // Set name to query string or body data
            name = name ?? data?.name;

            return name == null
                ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a name on the query string or in the request body")
                : req.CreateResponse(HttpStatusCode.OK, "Hello " + name);
                */

            dynamic args = await req.Content.ReadAsAsync<object>();

            if (args == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "No data");
            }

            string token = args.token;
            string type = args.type;

            if (token == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "No token");
            }

            if (token != ConfigurationManager.AppSettings["slack_verification_token"])
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Wrong token");
            }

            if (type == "url_verification")
            {
                string challenge = args.challenge;

                if (string.IsNullOrWhiteSpace(challenge))
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, "No challenge");
                }

                return req.CreateResponse(HttpStatusCode.OK, challenge);
            }

            if (type == "event_callback")
            {
                Task.Run(() => SlackEventHandler.HandleEvent((object)args));
            }

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}