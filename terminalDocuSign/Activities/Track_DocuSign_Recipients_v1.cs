﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Interfaces;
using StructureMap;
using Data.Repositories.MultiTenant;
using Fr8.Infrastructure.Data.Control;
using Fr8.Infrastructure.Data.Crates;
using Fr8.Infrastructure.Data.DataTransferObjects;
using Fr8.Infrastructure.Data.Managers;
using Fr8.Infrastructure.Data.Manifests;
using Fr8.Infrastructure.Data.States;
using Fr8.TerminalBase.Models;
using Fr8.TerminalBase.Services;
using Hub.Services.MT;
using Newtonsoft.Json;
using terminalDocuSign.Services.New_Api;

namespace terminalDocuSign.Activities
{
    public class Track_DocuSign_Recipients_v1 : BaseDocuSignActivity
    {
        private readonly IContainer _container;

        public static ActivityTemplateDTO ActivityTemplateDTO = new ActivityTemplateDTO
        {
            Name = "Track_DocuSign_Recipients",
            Label = "Track DocuSign Recipients",
            Version = "1",
            Category = ActivityCategory.Solution,
            NeedsAuthentication = true,
            MinPaneWidth = 380,
            WebService = TerminalData.WebServiceDTO,
            Terminal = TerminalData.TerminalDTO,
            Categories = new[] { ActivityCategories.Solution }
        };
        protected override ActivityTemplateDTO MyTemplate => ActivityTemplateDTO;

        private const string SolutionName = "Track DocuSign Recipients";
        private const double SolutionVersion = 1.0;
        private const string TerminalName = "DocuSign";
        private const string SolutionBody = @"<p>Link your important outgoing envelopes to Fr8's powerful notification activities, 
                                            which allow you to receive SMS notices, emails, or receive posts to popular tracking systems like Slack and Yammer. 
                                            Get notified when recipients take too long to sign!</p>";

        private const string MessageBody = @"Fr8 Alert: The DocuSign Envelope [TemplateName] has not been [ActionBeingTracked] by [RecipientUserName] at [RecipientEmail]. You had requested a notification after a delay of [DelayTime].";

        private class ActivityUi : StandardConfigurationControlsCM
        {
            public ActivityUi()
            {
                Controls = new List<ControlDefinitionDTO>();

                Controls.Add(new RadioButtonGroup()
                {
                    Name = "Track_Which_Envelopes",
                    Label = "Track which Envelopes?",
                    Events = new List<ControlEvent> { ControlEvent.RequestConfig },
                    Radios = new List<RadioButtonOption>
                    {
                        new RadioButtonOption
                        {
                            Name = "SpecificRecipient",
                            Value = "Envelopes sent to a specific recipient",
                            Selected = true,
                            Controls = new List<ControlDefinitionDTO>
                            {
                                new TextBox()
                                {
                                    Label = "",
                                    Name = "SpecificRecipient"
                                }
                            }
                        },
                        new RadioButtonOption
                        {
                            Name = "SpecificTemplate",
                            Value = "Envelopes based on a specific template",
                            Controls = new List<ControlDefinitionDTO>
                            {
                                new DropDownList()
                                {
                                    Name = "SpecificTemplate",
                                    Source = new FieldSourceDTO
                                    {
                                        Label = "AvailableTemplates",
                                        ManifestType = CrateManifestTypes.StandardDesignTimeFields
                                }
                            }
                        }
                    }
                    }
                });
                Controls.Add(new Duration
                {
                    Label = "After you send a Tracked Envelope, Fr8 will wait.",
                    InnerLabel = "Wait this long:",
                    Name = "TimePeriod"
                });
                Controls.Add(new DropDownList
                {
                    Label = "Then Fr8 will notify you if a recipient has not",
                    Name = "RecipientEvent",
                    Source = new FieldSourceDTO
                    {
                        Label = "AvailableRecipientEvents",
                        ManifestType = CrateManifestTypes.StandardDesignTimeFields
                    }
                });
                Controls.Add(new TextBlock
                {
                    Name = "EventInfo",
                    Label = "the Envelope."
                });

                Controls.Add(new DropDownList()
                {
                    Name = "NotificationHandler",
                    Label = "How would you like to be notified?",
                    Source = new FieldSourceDTO
                    {
                        Label = "AvailableHandlers",
                        ManifestType = CrateManifestTypes.StandardDesignTimeFields
                    },
                    Events = new List<ControlEvent> { ControlEvent.RequestConfig }
                });
            }
        }

        public Track_DocuSign_Recipients_v1(ICrateManager crateManager, IDocuSignManager docuSignManager, IContainer container)
            : base(crateManager, docuSignManager)
        {
            _container = container;
        }

        public override async Task Initialize()
        {
            Storage.Clear();
            Storage.Add(PackControls(new ActivityUi()));
            Storage.Add(PackAvailableTemplates());
            Storage.Add(await PackAvailableHandlers());
            Storage.Add(PackAvailableRecipientEvents());
            Storage.Add(PackAvailableRunTimeDataFields());
        }

        public override async Task FollowUp()
        {
            var specificRecipientOption = ((RadioButtonGroup)ConfigurationControls.Controls[0]).Radios[0];
            var specificTemplateOption = ((RadioButtonGroup)ConfigurationControls.Controls[0]).Radios[1];
            var howToBeNotifiedDdl = (DropDownList)ConfigurationControls.FindByName("NotificationHandler");
            //let's don't add child actions to solution until how to be notified option is selected
            //FR-1873
            if (string.IsNullOrEmpty(howToBeNotifiedDdl.Value))
            {
                return;
            }
            var specificRecipient = specificRecipientOption.Controls.Single();
            if (specificRecipientOption.Selected && string.IsNullOrEmpty(specificRecipient.Value))
            {
                return;
            }

            var specificTemplate = specificTemplateOption.Controls.Single();
            if (specificTemplateOption.Selected && string.IsNullOrEmpty(specificTemplate.Value))
            {
                return;
            }

            bool hasChildren = false;
            if (ActivityPayload.ChildrenActivities.Any())
            {
                hasChildren = true;
                ActivityPayload.ChildrenActivities.Clear();
            }

            var monitorDocusignAT = await HubCommunicator.GetActivityTemplate("terminalDocuSign", "Monitor_DocuSign_Envelope_Activity");
            var setDelayAT = await HubCommunicator.GetActivityTemplate("terminalFr8Core", "Set_Delay");
            var queryFr8WareHouseAT = await HubCommunicator.GetActivityTemplate("terminalFr8Core", "Query_Fr8_Warehouse");
            var testIncomingDataAT = await HubCommunicator.GetActivityTemplate("terminalFr8Core", "Test_Incoming_Data");
            var buildMessageAT = await HubCommunicator.GetActivityTemplate("terminalFr8Core", "Build_Message");
           
            //DocuSign
            var tasks = new List<Task>();
            var monitorDocuSignActionTask = HubCommunicator.AddAndConfigureChildActivity(ActivityPayload, monitorDocusignAT, "Monitor Docusign Envelope Activity", "Monitor Docusign Envelope Activity", 1);
            tasks.Add(monitorDocuSignActionTask);
            var setDelayActionTask = HubCommunicator.AddAndConfigureChildActivity(ActivityPayload, setDelayAT, "Set Delay", "Set Delay", 2);
            tasks.Add(setDelayActionTask);
            var queryFr8WarehouseActionTask = HubCommunicator.AddAndConfigureChildActivity(ActivityPayload, queryFr8WareHouseAT, "Query Fr8 Warehouse", "Query Fr8 Warehouse", 3);
            tasks.Add(queryFr8WarehouseActionTask);
            var filterActionTask = HubCommunicator.AddAndConfigureChildActivity(ActivityPayload, testIncomingDataAT, "Test Incoming Data", "Test Incoming Data", 4);
            tasks.Add(filterActionTask);
            Task<ActivityPayload> buildMessageActivityTask = null;
            if (!hasChildren)
            {
                buildMessageActivityTask = HubCommunicator.AddAndConfigureChildActivity((Guid)ActivityPayload.ParentPlanNodeId, buildMessageAT, "Build a Message", "Build a Message", 2);
                tasks.Add(buildMessageActivityTask);
            }

            await Task.WhenAll(tasks);

            var monitorDocuSignAction = monitorDocuSignActionTask.Result;
            var setDelayAction = setDelayActionTask.Result;
            var queryFr8WarehouseAction = queryFr8WarehouseActionTask.Result;
            var filterAction = filterActionTask.Result;
            // var notifierActivity = notifierActivityTask.Result;
            if (specificRecipientOption.Selected)
            {
                ControlHelper.SetControlValue(monitorDocuSignAction, "TemplateRecipientPicker.recipient.RecipientValue", specificRecipientOption.Controls[0].Value);
            }
            else if (specificTemplateOption.Selected)
            {
                var ddlbTemplate = (specificTemplateOption.Controls[0] as DropDownList);
                ControlHelper.SetControlValue(monitorDocuSignAction, "TemplateRecipientPicker.template.UpstreamCrate",
                   ddlbTemplate.ListItems.Single(a => a.Key == ddlbTemplate.selectedKey));
            }

            if (buildMessageActivityTask != null)
            {
                var buildMessageActivity = buildMessageActivityTask.Result;

                ControlHelper.SetControlValue(buildMessageActivity, "Body", MessageBody);
                ControlHelper.SetControlValue(buildMessageActivity, "Name", "NotificationMessage");

                buildMessageActivity = await HubCommunicator.ConfigureActivity(buildMessageActivity);
            }

            if (!hasChildren)
            {
                var notifierAT = await HubCommunicator.GetActivityTemplate(Guid.Parse(howToBeNotifiedDdl.Value));
                var notifierActivity = await HubCommunicator.AddAndConfigureChildActivity((Guid)ActivityPayload.ParentPlanNodeId, notifierAT, howToBeNotifiedDdl.selectedKey, howToBeNotifiedDdl.selectedKey, 3);
                SetNotifierActivityBody(notifierActivity);
                await HubCommunicator.ConfigureActivity(notifierActivity);
            }

            ControlHelper.SetControlValue(monitorDocuSignAction, "EnvelopeSent", "true");
            //let's make followup configuration for monitorDocuSignEventAction
            //followup call places EventSubscription crate in storage
            var configureMonitorDocusignTask = HubCommunicator.ConfigureActivity(monitorDocuSignAction);


            var durationControl = (Duration)ConfigurationControls.FindByName("TimePeriod");
            ControlHelper.SetControlValue(setDelayAction, "Delay_Duration", durationControl.Value);
            await SetQueryFr8WarehouseActivityFields(queryFr8WarehouseAction, specificRecipientOption.Controls[0].Value);
            //let's make a followup configuration to fill criteria fields
            var configureQueryMTTask = HubCommunicator.ConfigureActivity(queryFr8WarehouseAction);
            var recipientEventStatus = (DropDownList)ConfigurationControls.FindByName("RecipientEvent");
            SetFilterUsingRunTimeActivityFields(filterAction, recipientEventStatus.Value);

            await Task.WhenAll(configureMonitorDocusignTask, configureQueryMTTask);

            monitorDocuSignAction = configureMonitorDocusignTask.Result;
            ActivityPayload.ChildrenActivities = ActivityPayload.ChildrenActivities.OrderBy(a => a.Ordering).ToList();
            ActivityPayload.ChildrenActivities[0] = monitorDocuSignAction;
        }

        private void SetNotifierActivityBody(ActivityPayload notifierActivity)
        {
            if (notifierActivity.ActivityTemplate.Name == "Send_Email_Via_SendGrid")
            {
                var configControls = ControlHelper.GetConfigurationControls(notifierActivity.CrateStorage);
                var emailBodyField = ControlHelper.GetControl<TextSource>(configControls, "EmailBody", ControlTypes.TextSource);
                emailBodyField.ValueSource = "upstream";
                emailBodyField.Value = "NotificationMessage";
                emailBodyField.selectedKey = "NotificationMessage";
                var emailSubjectField = ControlHelper.GetControl<TextSource>(configControls, "EmailSubject", ControlTypes.TextSource);
                emailSubjectField.ValueSource = "specific";
                emailSubjectField.TextValue = "Fr8 Notification Message";
            }
            else if (notifierActivity.ActivityTemplate.Name == "Send_Via_Twilio")
            {
                var configControls = ControlHelper.GetConfigurationControls(notifierActivity.CrateStorage);
                var emailBodyField = ControlHelper.GetControl<TextSource>(configControls, "SMS_Body", ControlTypes.TextSource);
                emailBodyField.ValueSource = "upstream";
                emailBodyField.Value = "NotificationMessage";
                emailBodyField.selectedKey = "NotificationMessage";
            }
            else if (notifierActivity.ActivityTemplate.Name == "Publish_To_Slack")
            {
                var configControls = ControlHelper.GetConfigurationControls(notifierActivity.CrateStorage);
                if (configControls == null)
                {
                    //user is not authenticated yet - there is nothing we can do now
                    return;
                }
                var messageField = ControlHelper.GetControl<TextSource>(configControls, "Select_Message_Field", ControlTypes.TextSource);
                if (messageField == null)
                {
                    //user is not authenticated yet - there is nothing we can do now
                    return;
                }
                messageField.ValueSource = "upstream";
                messageField.Value = "NotificationMessage";
                messageField.selectedKey = "NotificationMessage";
            }
        }

        private void SetFilterUsingRunTimeActivityFields(ActivityPayload filterUsingRunTimeAction, string status)
        {
            var crateStorage = filterUsingRunTimeAction.CrateStorage;
            var configControlCM = crateStorage
                .CrateContentsOfType<StandardConfigurationControlsCM>()
                .First();
            var filterPane = (FilterPane)configControlCM.Controls.First(c => c.Name == "Selected_Filter");

            var conditions = new List<FilterConditionDTO>
            {
                new FilterConditionDTO{ Field = "Status", Operator = "neq", Value = status}
            };

            filterPane.Value = JsonConvert.SerializeObject(new FilterDataDTO
            {
                ExecutionType = FilterExecutionType.WithFilter,
                Conditions = conditions
            });

            var queryableCriteria = new FieldDescriptionsCM(
                new FieldDTO()
                {
                    Name = "Status",
                    Label = "Status",
                    FieldType = FieldType.String
                });
            var queryFieldsCrate = Crate.FromContent("Queryable Criteria", queryableCriteria);
            crateStorage.RemoveByLabel("Queryable Criteria");
            crateStorage.Add(queryFieldsCrate);
        }

        private async Task SetQueryFr8WarehouseActivityFields(ActivityPayload queryFr8Warehouse, string recipientEmail)
        {
            //update action's duration value
            var crateStorage = queryFr8Warehouse.CrateStorage;
            var configControlCM = ControlHelper.GetConfigurationControls(crateStorage);
            var radioButtonGroup = (configControlCM.Controls.First() as RadioButtonGroup);
            radioButtonGroup.Radios[0].Selected = false;
            radioButtonGroup.Radios[1].Selected = true;
            var objectList = (DropDownList)(radioButtonGroup.Radios[1].Controls.FirstOrDefault(c => c.Name == "AvailableObjects"));
            MtTypeReference selectedObject;

            if (string.IsNullOrEmpty(recipientEmail))
            {
                selectedObject = GetMtType(typeof(DocuSignEnvelopeCM_v2));
            }
            else
            {
                selectedObject = GetMtType(typeof(DocuSignRecipientStatus));
            }

            if (selectedObject == null)
            {
                return;
            }

            objectList.Value = selectedObject.Id.ToString("N");
            objectList.selectedKey = selectedObject.Alias;

            var filterPane = (FilterPane)radioButtonGroup.Radios[1].Controls.First(c => c.Name == "Filter");

            var conditions = new List<FilterConditionDTO>
                            {
                                new FilterConditionDTO{ Field = "EnvelopeId", Operator = "eq", Value = "FromPayload"}
                            };

            if (recipientEmail != null)
            {
                conditions.Add(new FilterConditionDTO { Field = "Email", Operator = "eq", Value = recipientEmail });
            }

            filterPane.Value = JsonConvert.SerializeObject(new FilterDataDTO
            {
                ExecutionType = FilterExecutionType.WithFilter,
                Conditions = conditions
            });

            using (var uow = _container.GetInstance<IUnitOfWork>())
            {
                var queryCriteria = Crate.FromContent("Queryable Criteria", new FieldDescriptionsCM(MTTypesHelper.GetFieldsByTypeId(uow, selectedObject.Id)));
                crateStorage.Add(queryCriteria);
            }
        }

        private MtTypeReference GetMtType(Type clrType)
        {
            using (var uow = _container.GetInstance<IUnitOfWork>())
            {
                return uow.MultiTenantObjectRepository.FindTypeReference(clrType);
            }
        }


        private Crate PackAvailableTemplates()
        {
            var conf = DocuSignManager.SetUp(AuthorizationToken);
            var fields = DocuSignManager.GetTemplatesList(conf);
            var crate = CrateManager.CreateDesignTimeFieldsCrate(
                "AvailableTemplates",
               fields.ToArray());
            return crate;
        }

        private Crate PackAvailableRecipientEvents()
        {
            var events = new[] { "Taken Delivery", "Signed" };

            var availableRecipientEventsCrate =
                CrateManager.CreateDesignTimeFieldsCrate(
                    "AvailableRecipientEvents", events.Select(x => new KeyValueDTO(x, x)).ToArray()
                );

            return availableRecipientEventsCrate;
        }

        private Crate PackAvailableRunTimeDataFields()
        {
            var events = new[] { "ActionBeingTracked", "DelayTime" };

            var availableRecipientEventsCrate =
                CrateManager.CreateDesignTimeFieldsCrate(
                    "AvailableRunTimeDataFields", events.Select(x => new KeyValueDTO(x, x)).ToArray()
                );

            return availableRecipientEventsCrate;
        }

        private async Task<Crate> PackAvailableHandlers()
        {
            var templates = await HubCommunicator.GetActivityTemplates(true);
            var taggedTemplates = templates.Where(x => x.Tags != null && x.Tags.Contains("Notifier"));

            var availableHandlersCrate =
                CrateManager.CreateDesignTimeFieldsCrate(
                    "AvailableHandlers",
                    taggedTemplates.Select(x => new KeyValueDTO(x.Label, x.Id.ToString())).ToArray()
                );

            return availableHandlersCrate;
        }

        protected override string ActivityUserFriendlyName => SolutionName;

        private void GetAllDescendants(ActivityPayload root, List<ActivityPayload> activties)
        {
            activties.AddRange(root.ChildrenActivities);

            foreach (var childrenActivity in root.ChildrenActivities)
            {
                GetAllDescendants(childrenActivity, activties);
            }
        }

        public override async Task Run()
        {
            var descendants = new List<ActivityPayload>();

            GetAllDescendants(ActivityPayload, descendants);

            var configControls = descendants.SelectMany(c => c.CrateStorage.FirstCrateOrDefault<StandardConfigurationControlsCM>()?.Content?.Controls).Where(x => x != null).ToArray();
            var delayValue = (Duration)configControls.Single(c => c.Name == "Delay_Duration" && c.Type == ControlTypes.Duration);
            var runTimePayloadData = new List<KeyValueDTO>();
            var delayTimeString = delayValue.Days + " days, " + delayValue.Hours + " hours and " + delayValue.Minutes + " minutes";
            runTimePayloadData.Add(new KeyValueDTO("DelayTime", delayTimeString));
            var filterPane = (FilterPane)configControls.Single(c => c.Name == "Selected_Filter" && c.Type == ControlTypes.FilterPane);
            var conditions = JsonConvert.DeserializeObject<FilterDataDTO>(filterPane.Value);
            var statusField = conditions.Conditions.FirstOrDefault(c => c.Field == "Status");
            if (statusField != null)
            {
                runTimePayloadData.Add(new KeyValueDTO("ActionBeingTracked", statusField.Value));
            }
            Payload.Add(Crate.FromContent("Track DocuSign Recipients Payload Data", new StandardPayloadDataCM(runTimePayloadData)));
            Success();
        }
        /// <summary>
        /// This method provides documentation in two forms:
        /// SolutionPageDTO for general information and 
        /// ActivityResponseDTO for specific Help on minicon
        /// </summary>
        /// <param name="activityDO"></param>
        /// <param name="curDocumentation"></param>
        /// <returns></returns>
        protected override Task<DocumentationResponseDTO> GetDocumentation(string curDocumentation)
        {
            if (curDocumentation.Contains("MainPage"))
            {
                var curSolutionPage = new DocumentationResponseDTO(SolutionName, SolutionVersion, TerminalName, SolutionBody);
                return Task.FromResult(curSolutionPage);
            }
            if (curDocumentation.Contains("HelpMenu"))
            {
                if (curDocumentation.Contains("TrackDocuSignRecipients"))
                {
                    return Task.FromResult(new DocumentationResponseDTO(@"This solution work with notifications"));
                }
                if (curDocumentation.Contains("ExplainService"))
                {
                    return Task.FromResult(new DocumentationResponseDTO(@"This solution works and DocuSign service and uses Fr8 infrastructure"));
                }
                return Task.FromResult(new DocumentationResponseDTO("Unknown contentPath"));
            }
            return
                Task.FromResult(
                    new DocumentationResponseDTO("Unknown displayMechanism: we currently support MainPage and HelpMenu cases"));
        }
    }
}