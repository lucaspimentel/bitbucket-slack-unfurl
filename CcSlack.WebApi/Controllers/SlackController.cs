using System.Configuration;
using System.Threading.Tasks;
using System.Web.Http;
using CcSlack.Shared.Slack;

namespace CcSlack.WebApi.Controllers
{
    public class SlackController : ApiController
    {
        /*
        [Route("slack/authorize/start")]
        public IHttpActionResult Authorize()
        {
            var data = new
                       {
                           client_id = "",
                           scope = "",
                           // redirect_uri = "",
                           // state = "",
                           // team = ""
                       };
            // https://slack.com/oauth/authorize
            //SlackClient.UrlEncode()
        }

        [Route("slack/authorize/complete")]
        public IHttpActionResult Authorize()
        {

        }
        */

        /*
        public class SlashCommandArguments
        {
            public string token { get; set; }
            public string team_id { get; set; }
            public string team_domain { get; set; }
            public string channel_id { get; set; }
            public string channel_name { get; set; }
            public string user_id { get; set; }
            public string user_name { get; set; }
            public string command { get; set; }
            public string text { get; set; }
            public string response_url { get; set; }
        }

        [Route("slack/slash-command")]
        [HttpPost]
        public IHttpActionResult PostSlashCommand(SlashCommandArguments args)
        {
            if (args == null)
            {
                return BadRequest("No data");
            }

            if (args.token == null)
            {
                return BadRequest("No token");
            }

            if (args.token != ConfigurationManager.AppSettings["slack_verification_token"])
            {
                return BadRequest("Wrong token");
            }

            if (args.command == "/pull-request")
            {
            }

            return Ok();
        }
        */

        [Route("slack/event")]
        [HttpPost]
        public IHttpActionResult PostEvent(dynamic args)
        {
            if (args == null)
            {
                return BadRequest("No data");
            }

            string token = args.token;
            string type = args.type;

            if (token == null)
            {
                return BadRequest("No token");
            }

            if (token != ConfigurationManager.AppSettings["slack_verification_token"])
            {
                return BadRequest("Wrong token");
            }

            if (type == "url_verification")
            {
                string challenge = args.challenge;

                if (string.IsNullOrWhiteSpace(challenge))
                {
                    return BadRequest("No challenge");
                }

                return Ok(challenge);
            }

            if (type == "event_callback")
            {
                Task.Run(() => SlackEventHandler.HandleEvent((object)args));
            }

            return Ok();
        }
    }
}