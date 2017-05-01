using System.Configuration;
using System.Web.Http;

namespace cc_slack_api.Controllers
{
    public class SlackController : ApiController
    {
        [Route("slack/url_verification")]
        public IHttpActionResult UrlVerification(dynamic data)
        {
            if (data.token == ConfigurationManager.AppSettings["slack_token"] && data.type == "url_verification")
            {
                return Ok(data.challenge);
            }

            return BadRequest();
        }
    }
}
