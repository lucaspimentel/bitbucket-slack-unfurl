using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;
using Newtonsoft.Json;

namespace cc_slack_api.Controllers
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
                Task.Run(() => HandleEvent((object)args));
            }

            return Ok();
        }

        private async void HandleEvent(dynamic data)
        {
            string eventType = data.@event.type;

            if (eventType == "link_shared")
            {
                await HandleLinkSharedEvent(data);
            }

            // TODO: handle other event types
        }

        private static async Task HandleLinkSharedEvent(dynamic data)
        {
            if ((string) data.team_id != ConfigurationManager.AppSettings["slack_team_id"])
            {
                return;
            }

            if ((string) data.api_app_id != ConfigurationManager.AppSettings["slack_app_id"])
            {
                return;
            }

            var unfurls = new Dictionary<string, object>();

            foreach (var link in data.@event.links)
            {
                if ((string) link.domain == "codebase-aws.clearcompany.com")
                {
                    string linkUrl = (string) link.url;
                    object attachment = await GetBitbucketPullRequestAttachment(linkUrl);

                    if (attachment != null)
                    {
                        unfurls.Add(linkUrl, attachment);
                    }
                }
            }

            if (unfurls.Count > 0)
            {
                var postData = new Dictionary<string, string>
                               {
                                   {"token", ConfigurationManager.AppSettings["slack_oauth_token"]},
                                   {"channel", (string) data.@event.channel},
                                   {"ts", (string) data.@event.message_ts},
                                   {"unfurls", JsonConvert.SerializeObject(unfurls)}
                               };

                var slackClient = new SlackClient();
                await slackClient.PostToSlack("chat.unfurl", postData);
            }
        }

        public static async Task<object> GetBitbucketPullRequestAttachment(string linkUrl)
        {
            Match match = Regex.Match(linkUrl, @"https://codebase-aws\.clearcompany\.com/projects/(\w+)/repos/(\w+)/pull-requests/(\d+).*");

            if (!match.Success)
            {
                return null;
            }

            string projectKey = match.Groups[1].Value;
            string repositorySlug = match.Groups[2].Value;
            string pullRequestId = match.Groups[3].Value;
            string bitbucketUsername = ConfigurationManager.AppSettings["bitbucket_username"];
            string bitbucketPassword = ConfigurationManager.AppSettings["bitbucket_password"];

            dynamic pullRequestDetails = await BitbucketServerClient.GetPullRequestDetails(bitbucketUsername, bitbucketPassword, projectKey, repositorySlug, pullRequestId);

            string sourceBranch = ((string) pullRequestDetails.fromRef.id).Replace("refs/heads/", "");
            string destinationBranch = ((string) pullRequestDetails.toRef.id).Replace("refs/heads/", "");
            string originalDescription = pullRequestDetails.description;
            string[] descriptionLines = originalDescription.Split(new[] {"\r\n", "\n", "\r"}, StringSplitOptions.None);

            string description = string.Join("\n", descriptionLines.Take(3))
                                       .Replace("&", "&amp;")
                                       .Replace("<", "&lt;")
                                       .Replace(">", "&gt;");

            var attachment = new
                             {
                                 //fallback = "test fallback text",
                                 color = "#36a64f",
                                 pretext = $"From `{sourceBranch}` to `{destinationBranch}",
                                 author_name = (string) pullRequestDetails.author.user.displayName,
                                 author_link = $"https://codebase-aws.clearcompany.com/users/{pullRequestDetails.author.user.slug}",
                                 //author_icon = "",
                                 title = (string) pullRequestDetails.title,
                                 title_link = (string) pullRequestDetails.links.self[0].href,
                                 text = description,
                                 fields = new[]
                                          {
                                              new
                                              {
                                                  title = "State",
                                                  value = $"{pullRequestDetails.state}",
                                                  @short = true
                                              }
                                          },
                                 //image_url = "http=//my-website.com/path/to/image.jpg",
                                 //thumb_url = "http=//example.com/path/to/thumb.png",
                                 footer = "Bitbucket Server",
                                 //footer_icon = "https=//platform.slack-edge.com/img/default_application_icon.png",
                                 //ts = 123456789,
                                 mrkdwn_in = new[] {"text", "pretext"}
                             };

            return attachment;
        }
    }
}