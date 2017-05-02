using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Newtonsoft.Json;

namespace cc_slack_api.Controllers
{
    public class SlackController : ApiController
    {
        [Route("slack/event")]
        public async Task<IHttpActionResult> UrlVerification(dynamic data)
        {
            if ((string) data.token == ConfigurationManager.AppSettings["slack_token"])
            {
                return Unauthorized();
            }

            if (data.type == "url_verification" && !string.IsNullOrWhiteSpace(data.challenge))
            {
                return Ok(data.challenge);
            }

            if (data.type == "event_callback")
            {
                string eventType = data.@event?.type;
                string eventChannel = data.@event?.channel;
                string allowedEventChannel = ConfigurationManager.AppSettings["event_channel"];

                if (!string.IsNullOrEmpty(allowedEventChannel) && allowedEventChannel != eventChannel)
                {
                    // ignore events from other channels
                    return Ok();
                }

                if (eventType == "link_shared")
                {
                    var unfurls = new Dictionary<string, object>();
                    var client = new HttpClient();

                    foreach (var link in data.@event.links)
                    {
                        if (link.domain == "codebase-aws.clearcompany.com")
                        {
                            string linkUrl = (string) link.url;
                            Match match = Regex.Match(linkUrl, @"https://codebase-aws\.clearcompany\.com/projects/(\w+)/repos/(\w+)/pull-requests/(\d+).*");

                            if (match.Success)
                            {
                                string projectKey = match.Groups[1].Value;
                                string repositorySlug = match.Groups[2].Value;
                                string pullRequestId = match.Groups[3].Value;

                                HttpResponseMessage responseMessage = await client.GetAsync($@"https://codebase-aws.clearcompany.com/rest/api/1.0/projects/{projectKey}/repos/{repositorySlug}/pull-requests/{pullRequestId}");
                                dynamic response = await responseMessage.Content.ReadAsAsync<dynamic>();

                                var attachment = new
                                                 {
                                                     fallback = "Required plain-text summary of the attachment.",
                                                     color = "#36a64f",
                                                     pretext = "Optional text that appears above the attachment block",
                                                     author_name = response.author.user.displayName,
                                                     author_link = $"https://codebase-aws.clearcompany.com/users/{response.author.user.slug}",
                                                     //author_icon = "",
                                                     title = response.title,
                                                     title_link = response.links.self[0].href,
                                                     text = response.description + "\n" + response.fromRef.id + " -> " + response.toRef.id,
                                                     fields = new[]
                                                              {
                                                                  new
                                                                  {
                                                                      title = "State",
                                                                      value = $"{response.state}",
                                                                      @short = false
                                                                  }
                                                              },
                                                     //image_url = "http=//my-website.com/path/to/image.jpg",
                                                     //thumb_url = "http=//example.com/path/to/thumb.png",
                                                     footer = "Bitbucket Server",
                                                     //footer_icon = "https=//platform.slack-edge.com/img/default_application_icon.png",
                                                     ts = 123456789
                                                 };

                                unfurls.Add(linkUrl, new[] {attachment});
                            }
                        }
                    }

                    var postData = new
                                   {
                                       token = data.@event.token,
                                       channel = eventChannel,
                                       ts = data.@event.message_ts,
                                       unfurls = HttpUtility.UrlEncode(JsonConvert.SerializeObject(unfurls))
                                   };

                    HttpResponseMessage postResponse = await client.PostAsJsonAsync(@"https://slack.com/api/chat.unfurl", postData);
                }
            }

            return BadRequest();
        }
    }
}