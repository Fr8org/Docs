﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fr8.Infrastructure.Data.Control;
using Fr8.Infrastructure.Data.DataTransferObjects;
using Fr8.Infrastructure.Data.Managers;
using Fr8.Infrastructure.Data.Manifests;
using Fr8.Infrastructure.Data.States;
using Fr8.Infrastructure.Utilities.Logging;
using Fr8.TerminalBase.BaseClasses;
using Fr8.TerminalBase.Infrastructure;
using terminalBasecamp.Infrastructure;

namespace terminalBasecamp.Activities
{
    public class Create_Message_v1 : TerminalActivity<Create_Message_v1.ActivityUi>
    {
        private readonly IBasecampApiClient _basecampApiClient;

        public static ActivityTemplateDTO ActivityTemplate = new ActivityTemplateDTO
        {
            Name = "Create_Message",
            Label = "Create Message",
            Category = ActivityCategory.Forwarders,
            Version = "1",
            MinPaneWidth = 330,
            WebService = TerminalData.WebServiceDTO,
            Terminal = TerminalData.TerminalDTO,
            NeedsAuthentication = true
        };

        public class ActivityUi : StandardConfigurationControlsCM
        {
            public DropDownList AccountSelector { get; set; }

            public DropDownList ProjectSelector { get; set; }

            public TextSource MessageSubject { get; set; }

            public TextSource MessageContent { get; set; }

            public ActivityUi()
            {
                AccountSelector = new DropDownList
                {
                    Name = nameof(AccountSelector),
                    Label = "Select Account",
                    Events = new List<ControlEvent> { ControlEvent.RequestConfig }
                };
                Controls.Add(AccountSelector);
                ProjectSelector = new DropDownList
                {
                    Name = nameof(ProjectSelector),
                    Label = "Select Project",
                    IsHidden = true
                };
                Controls.Add(ProjectSelector);
                MessageSubject = new TextSource
                {
                    InitialLabel = "Subject",
                    Name = nameof(MessageSubject),
                    IsHidden = true
                };
                Controls.Add(MessageSubject);
                MessageContent = new TextSource
                {
                    InitialLabel = "Content",
                    Name = nameof(MessageContent),
                    IsHidden = true
                };
                Controls.Add(MessageContent);
            }
        }

        protected override ActivityTemplateDTO MyTemplate => ActivityTemplate;

        public Create_Message_v1(ICrateManager crateManager, IBasecampApiClient basecampApiClient) : base(crateManager)
        {
            if (basecampApiClient == null)
            {
                throw new ArgumentNullException(nameof(basecampApiClient));
            }
            _basecampApiClient = basecampApiClient;
        }

        protected override Task Validate()
        {
            if (string.IsNullOrEmpty(ActivityUI.AccountSelector.selectedKey))
            {
                ValidationManager.SetError("Account is not selected", ActivityUI.AccountSelector);
            }
            if (ActivityUI.ProjectSelector.ListItems?.Count == 0)
            {
                ValidationManager.SetError("Your account doesn't contain Basecamp2 projects. Please reauthenticate with a different Basecamp account", ActivityUI.ProjectSelector);
            }
            else if (string.IsNullOrEmpty(ActivityUI.ProjectSelector.selectedKey))
            {
                ValidationManager.SetError("Project is not selected", ActivityUI.ProjectSelector);
            }
            ValidationManager.ValidateTextSourceNotEmpty(ActivityUI.MessageSubject, "Can't create message with empty subject");
            ValidationManager.ValidateTextSourceNotEmpty(ActivityUI.MessageContent, "Can't create message with empty content");
            return base.Validate();
        }

        public override async Task FollowUp()
        {
            var selectedAccount = ActivityUI.AccountSelector.Value;
            var previousSelectedAccount = PreviousSelectedAccount;
            if (string.IsNullOrEmpty(previousSelectedAccount) || selectedAccount != previousSelectedAccount)
            {
                await LoadProjectsAndSelectTheOnlyOne().ConfigureAwait(false);
                PreviousSelectedAccount = selectedAccount;
            }
        }

        public override async Task Initialize()
        {
            await LoadAccountAndSelectTheOnlyOne();
            if (ActivityUI.AccountSelector.ListItems.Count == 1)
            {
                await LoadProjectsAndSelectTheOnlyOne().ConfigureAwait(false);
            }
        }

        public override async Task Run()
        {
            try
            {
                await _basecampApiClient.CreateMessage(
                                                       ActivityUI.AccountSelector.Value,
                                                       ActivityUI.ProjectSelector.Value,
                                                       ActivityUI.MessageSubject.GetValue(Payload),
                                                       ActivityUI.MessageContent.GetValue(Payload),
                                                       AuthorizationToken)
                                        .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create new message. Basecamp user - {AuthorizationToken.ExternalAccountName}, Fr8 User Id - {CurrentUserId}, Details - {ex}");
                throw;
            }
        }

        #region Implementation details

        private string PreviousSelectedAccount
        {
            get { return this["Account"]; }
            set { this["Account"] = value; }
        }

        private string PreviousSelectedProject
        {
            get { return this["Project"]; }
            set { this["Project"] = value; }
        }

        private async Task LoadProjectsAndSelectTheOnlyOne()
        {
            var selectedAccount = ActivityUI.AccountSelector.Value;
            if (string.IsNullOrEmpty(selectedAccount))
            {
                ActivityUI.AccountSelector.IsHidden = false;
                ActivityUI.ProjectSelector.IsHidden = true;
                ActivityUI.MessageSubject.IsHidden = true;
                ActivityUI.MessageContent.IsHidden = true;
            }
            else
            {
                ActivityUI.ProjectSelector.IsHidden = false;
                ActivityUI.MessageSubject.IsHidden = false;
                ActivityUI.MessageContent.IsHidden = false;
                var projects = await _basecampApiClient.GetProjects(selectedAccount, AuthorizationToken).ConfigureAwait(false);
                ActivityUI.ProjectSelector.ListItems = projects.Select(x => new ListItem { Key = x.Name, Value = x.Id.ToString() }).ToList();
                if (ActivityUI.ProjectSelector.ListItems.Count == 1)
                {
                    ActivityUI.ProjectSelector.SelectByKey(ActivityUI.ProjectSelector.ListItems[0].Key);
                    ActivityUI.ProjectSelector.IsHidden = true;
                    PreviousSelectedProject = ActivityUI.ProjectSelector.Value;
                }
            }
        }

        private async Task LoadAccountAndSelectTheOnlyOne()
        {
            var accounts = await _basecampApiClient.GetAccounts(AuthorizationToken).ConfigureAwait(false);
            ActivityUI.AccountSelector.ListItems = accounts.Select(x => new ListItem { Key = x.Name, Value = x.ApiUrl }).ToList();
            if (ActivityUI.AccountSelector.ListItems.Count == 1)
            {
                ActivityUI.AccountSelector.SelectByKey(ActivityUI.AccountSelector.ListItems[0].Key);
                ActivityUI.AccountSelector.IsHidden = true;
                PreviousSelectedAccount = ActivityUI.AccountSelector.Value;
            }
        }

        #endregion
    }
}