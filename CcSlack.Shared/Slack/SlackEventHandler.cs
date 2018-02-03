using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CcSlack.Shared.Bitbucket;
using Newtonsoft.Json;

namespace CcSlack.Shared.Slack
{
    public static class SlackEventHandler
    {
        public static async void HandleEvent(dynamic data)
        {
            string eventType = data.@event.type;

            if (eventType == "link_shared")
            {
                await HandleLinkSharedEvent(data);
            }

            // TODO: handle other event types
        }

        public static async Task HandleLinkSharedEvent(dynamic data)
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
            string[] descriptionLines = String.IsNullOrEmpty(originalDescription) ?
                                            new[] {"(no description)"} :
                                            originalDescription.Split(new[] {"\r\n", "\n", "\r"}, StringSplitOptions.None);

            string description = String.Join("\n", descriptionLines.Take(3))
                                       .Replace("&", "&amp;")
                                       .Replace("<", "&lt;")
                                       .Replace(">", "&gt;");

            var attachment = new
                             {
                                 fallback = $@"Pull Request: from ""{sourceBranch}"" to ""{destinationBranch}""",
                                 //color = "#36a64f",
                                 pretext = $"Pull Request: from `{sourceBranch}` to `{destinationBranch}`",
                                 author_name = (string) pullRequestDetails.author.user.displayName,
                                 author_link = $"https://codebase-aws.clearcompany.com/users/{pullRequestDetails.author.user.slug}",
                                 author_icon = BitbucketServerClient.GetGravatarImageUrl((string)pullRequestDetails.author.user.emailAddress),
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
                                 footer_icon = "http://cc-slack-api.azurewebsites.net/content/images/bitbucket_16x16.png",
                                 //ts = 123456789,
                                 mrkdwn_in = new[] {"text", "pretext"}
                             };

            return attachment;
        }
    }
}