﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fr8.Infrastructure.Data.Control;
using Fr8.Infrastructure.Data.Crates;
using Fr8.Infrastructure.Data.DataTransferObjects;
using Fr8.Infrastructure.Data.Managers;
using Fr8.Infrastructure.Data.Manifests;
using Fr8.Infrastructure.Data.States;
using Fr8.TerminalBase.BaseClasses;
using Fr8.TerminalBase.Errors;
using terminalSlack.Interfaces;
using terminalSlack.Services;

namespace terminalSlack.Activities
{

    public class Publish_To_Slack_v1 : ExplicitTerminalActivity
    {
        private readonly ISlackIntegration _slackIntegration;

        public static ActivityTemplateDTO ActivityTemplateDTO = new ActivityTemplateDTO
        {
            Name = "Publish_To_Slack",
            Label = "Publish To Slack",
            Tags = "Notifier",
            Category = ActivityCategory.Forwarders,
            Terminal = TerminalData.TerminalDTO,
            NeedsAuthentication = true,
            Version = "1",
            WebService = TerminalData.WebServiceDTO,
            MinPaneWidth = 330,
            Categories = new[]
            {
                ActivityCategories.Forward,
                new ActivityCategoryDTO(TerminalData.WebServiceDTO.Name, TerminalData.WebServiceDTO.IconPath)
            }
        };
        protected override ActivityTemplateDTO MyTemplate => ActivityTemplateDTO;


        protected override Task Validate()
        {
            var messageField = GetControl<TextSource>("Select_Message_Field");
            var actionChannelId = GetControl<DropDownList>("Selected_Slack_Channel").Value;

            if (string.IsNullOrEmpty(actionChannelId))
            {
                ValidationManager.SetError("Channel or user is not specified", "Selected_Slack_Channel");
            }

            if (messageField.CanGetValue(ValidationManager.Payload) && string.IsNullOrWhiteSpace(messageField.GetValue(ValidationManager.Payload)))
            {
                ValidationManager.SetError("Can't post empty message to Slack", messageField);
            }

            return Task.FromResult(0);
        }

        public override async Task Run()
        {
            string message;

            var actionChannelId = GetControl<DropDownList>("Selected_Slack_Channel")?.Value;
            if (string.IsNullOrEmpty(actionChannelId))
            {
                RaiseError("No selected channelId found in activity.");
            }

            var messageField = GetControl<TextSource>("Select_Message_Field");
            try
            {
                message = messageField.GetValue(Payload);
            }
            catch (ApplicationException ex)
            {
                RaiseError("Cannot get selected field value from TextSource control in activity. Detailed information: " + ex.Message);
            }

            try
            {
                await _slackIntegration.PostMessageToChat(AuthorizationToken.Token,
                    actionChannelId, StripHTML(messageField.GetValue(Payload)));
            }
            catch (AuthorizationTokenExpiredOrInvalidException)
            {
                RaiseInvalidTokenError();
            }
            Success();
        }

        public override async Task Initialize()
        {
            var oauthToken = AuthorizationToken.Token;
            var configurationCrate = PackCrate_ConfigurationControls();
            await FillSlackChannelsSource(configurationCrate, "Selected_Slack_Channel", oauthToken);

            Storage.Clear();
            Storage.Add(configurationCrate);
        }

        public Publish_To_Slack_v1(ICrateManager crateManager, ISlackIntegration slackIntegration)
            : base(crateManager)
        {
            _slackIntegration = slackIntegration;
        }

        public static string StripHTML(string input)
        {
            return Regex.Replace(input, "<.*?>", String.Empty);
        }

        private Crate PackCrate_ConfigurationControls()
        {
            var fieldSelectChannel = new DropDownList()
            {
                Label = "Select Slack Channel",
                Name = "Selected_Slack_Channel",
                Required = true,
                Source = null
            };

            var fieldSelect = ControlHelper.CreateSpecificOrUpstreamValueChooser(
                "Select Message Field",
                "Select_Message_Field",
                addRequestConfigEvent: true,
                requestUpstream: true
            );

            var fieldsDTO = new List<ControlDefinitionDTO>()
            {
                fieldSelectChannel,
                fieldSelect
            };

            return CrateManager.CreateStandardConfigurationControlsCrate("Configuration_Controls", fieldsDTO.ToArray());
        }

        // TODO: finish that later.
        /*
        public object Execute(SlackPayloadDTO curSlackPayload)
        {
            string responseText = string.Empty;
            Encoding encoding = new UTF8Encoding();

            const string webhookUrl = "WebhookUrl";
            Uri uri = new Uri(ConfigurationManager.AppSettings[webhookUrl]);

            string payloadJson = JsonConvert.SerializeObject(curSlackPayload);

            using (WebClient client = new WebClient())
            {
                NameValueCollection data = new NameValueCollection();
                data["payload"] = payloadJson;

                var response = client.UploadValues(uri, "POST", data);

                responseText = encoding.GetString(response);
            }
            return responseText;
        }
        */

        #region Fill Source
        private async Task FillSlackChannelsSource(Crate configurationCrate, string controlName, string oAuthToken)
        {
            var configurationControl = configurationCrate.Get<StandardConfigurationControlsCM>();
            var control = configurationControl.FindByNameNested<DropDownList>(controlName);
            if (control != null)
            {
                control.ListItems = await GetAllChannelList(oAuthToken);
            }
        }
        private async Task<List<ListItem>> GetAllChannelList(string oAuthToken)
        {
            var channels = await _slackIntegration.GetAllChannelList(oAuthToken);
            return channels.Select(x => new ListItem() { Key = x.Key, Value = x.Value }).ToList();
        }

        public override Task FollowUp()
        {
            return Task.FromResult(0);
        }




        #endregion
    }
}