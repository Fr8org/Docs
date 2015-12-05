﻿using System;
using Data.Interfaces.DataTransferObjects;
using Data.Entities;
using Data.Crates;
using Data.Control;
using Data.Interfaces.Manifests;
using System.Collections.Generic;
using Hub.Managers;
using System.Linq;
using System.Runtime.InteropServices;

namespace terminalGoogleTests.Unit
{
    public class HealthMonitor_FixtureData
    {

        protected ICrateManager CrateManager;
        public HealthMonitor_FixtureData()
        {

            CrateManager = new CrateManager();
        }

        public static AuthorizationTokenDTO Google_AuthToken()
        {
            return new AuthorizationTokenDTO()
            {
                Token = @"{""AccessToken"":""ya29.PwJez2aHwjGxsxcho6TfaFseWjPbi1ThgINsgiawOKLlzyIgFJHkRdq76YrnuiGT3jhr"",""RefreshToken"":""1/HVhoZXzxFrPyC0JVlbEIF_VOBDm_IhrKoLKnt6QpyFRIgOrJDtdun6zK6XiATCKT"",""Expires"":""2015-12-03T11:12:43.0496208+08:00""}"
            };
        }

        protected Crate PackControls(StandardConfigurationControlsCM page)
        {
            return PackControlsCrate(page.Controls.ToArray());
        }

        protected Crate<StandardConfigurationControlsCM> PackControlsCrate(params ControlDefinitionDTO[] controlsList)
        {
            return Crate<StandardConfigurationControlsCM>.FromContent("Configuration_Controls", new StandardConfigurationControlsCM(controlsList));
        }

        private Crate PackCrate_GoogleForms()
        {
            Crate crate;

            var curFields = new List<FieldDTO>() { new FieldDTO() { Key = "Survey Form", Value = "1z7mIQdHeFIpxBm92sIFB52B7SwyEO3IT5LiUcmojzn8" } }.ToArray();
            crate = CrateManager.CreateDesignTimeFieldsCrate("Available Forms", curFields);

            return crate;
        }

        private Crate CreateEventSubscriptionCrate()
        {
            var subscriptions = new string[] {
                "Google Form Response"
            };

            return CrateManager.CreateStandardEventSubscriptionsCrate(
                "Standard Event Subscriptions",
                subscriptions.ToArray()
                );
        }

        public static ActivityTemplateDTO Receive_Google_Form_v1_ActivityTemplate()
        {
            return new ActivityTemplateDTO()
            {
                Id = 1,
                Name = "Receive_Google_Form_TEST",
                Version = "1"
            };
        }

        private Crate PackCrate_ConfigurationControls()
        {
            var fieldSelectTemplate = new DropDownList()
            {
                Label = "Select Google Form",
                Name = "Selected_Google_Form",
                Required = true,
                selectedKey = "Survey Form",
                Value = "1z7mIQdHeFIpxBm92sIFB52B7SwyEO3IT5LiUcmojzn8",
                Source = new FieldSourceDTO
                {
                    Label = "Available Forms",
                    ManifestType = CrateManifestTypes.StandardDesignTimeFields
                }
            };

            var controls = PackControlsCrate(fieldSelectTemplate);
            return controls;
        }

        public void ActivateCrateStorage(ActionDTO curActionDO)
        {
            var configurationControlsCrate = PackCrate_ConfigurationControls();
            var crateDesignTimeFields = PackCrate_GoogleForms();
            var eventCrate = CreateEventSubscriptionCrate();

            using (var updater = CrateManager.UpdateStorage(curActionDO))
            {
                updater.CrateStorage.Add(configurationControlsCrate);
                updater.CrateStorage.Add(crateDesignTimeFields);
                updater.CrateStorage.Add(eventCrate);
            }
        }

        public static ActionDTO Receive_Google_Form_v1_InitialConfiguration_ActionDTO()
        {
            var activityTemplate = Receive_Google_Form_v1_ActivityTemplate();

            return new ActionDTO()
            {
                Id = Guid.NewGuid(),
                Name = "Receive_Google_Form",
                Label = "Receive Google Form Response",
                AuthToken = Google_AuthToken(),
                ActivityTemplate = activityTemplate,
                ActivityTemplateId = activityTemplate.Id
            };
        }

        public ActionDTO Receive_Google_Form_v1_ActivateDeactivate_ActionDTO()
        {
            var activityTemplate = Receive_Google_Form_v1_ActivityTemplate();

            var action = new ActionDTO()
            {
                Id = Guid.NewGuid(),
                Name = "Receive_Google_Form",
                Label = "Receive Google Form Response",
                AuthToken = Google_AuthToken(),
                ActivityTemplate = activityTemplate,
                ActivityTemplateId = activityTemplate.Id,
                ParentRouteNodeId = Guid.NewGuid(),
            };

            ActivateCrateStorage(action);
            return action;
        }

        private CrateStorage WrapPayloadDataCrate(List<FieldDTO> payloadFields)
        {
            return new CrateStorage(Data.Crates.Crate.FromContent("Payload Data", new StandardPayloadDataCM(payloadFields)));
        }

        private Crate PayloadRaw()
        {
            List<FieldDTO> payloadFields = new List<FieldDTO>();
            payloadFields.Add(new FieldDTO() { Key = "user_id", Value = "g_admin@dockyard.company" });
            payloadFields.Add(new FieldDTO() { Key = "response", Value = "What is your pets name=cat&What is your favorite book?=book&Who is your favorite superhero?=hero&" });
            var eventReportContent = new EventReportCM
            {
                EventNames = "Google Form Response",
                ContainerDoId = "",
                EventPayload = WrapPayloadDataCrate(payloadFields),
                ExternalAccountId = "g_admin@dockyard.company"
            };

            //prepare the event report
            var curEventReport = Crate.FromContent("Standard Event Report", eventReportContent);
            return curEventReport;
        }

        private Crate PayloadEmptyRaw()
        {
            List<FieldDTO> payloadFields = new List<FieldDTO>();
            var eventReportContent = new EventReportCM
            {
                EventNames = "Google Form Response",
                ContainerDoId = "",
                EventPayload = WrapPayloadDataCrate(payloadFields),
                ExternalAccountId = "g_admin@dockyard.company"
            };

            //prepare the event report
            var curEventReport = Crate.FromContent("Standard Event Report", eventReportContent);
            return curEventReport;
        }

        public ActionDTO Receive_Google_Form_v1_Run_ActionDTO()
        {
            var activityTemplate = Receive_Google_Form_v1_ActivityTemplate();

            var action = new ActionDTO()
            {
                Id = Guid.NewGuid(),
                Name = "Receive_Google_Form",
                Label = "Receive Google Form Response",
                AuthToken = Google_AuthToken(),
                ActivityTemplate = activityTemplate,
                ActivityTemplateId = activityTemplate.Id
            };
            using (var updater = CrateManager.UpdateStorage(action))
            {
                updater.CrateStorage.Add(PayloadRaw());
            }
            return action;
        }

        public ActionDTO Receive_Google_Form_v1_Run_EmptyPayload()
        {
            var activityTemplate = Receive_Google_Form_v1_ActivityTemplate();

            var action = new ActionDTO()
            {
                Id = Guid.NewGuid(),
                Name = "Receive_Google_Form",
                Label = "Receive Google Form Response",
                AuthToken = Google_AuthToken(),
                ActivityTemplate = activityTemplate,
                ActivityTemplateId = activityTemplate.Id
            };
            using (var updater = CrateManager.UpdateStorage(action))
            {
                updater.CrateStorage.Add(PayloadEmptyRaw());
            }
            return action;
        }

        public static ActivityTemplateDTO Extract_Spreadsheet_Data_v1_ActivityTemplate()
        {
            return new ActivityTemplateDTO()
            {
                Id = 1,
                Name = "Extract_Spreadsheet_Data_TEST",
                Version = "1"
            };
        }
        public static ActionDTO Extract_Spreadsheet_Data_v1_InitialConfiguration_ActionDTO()
        {
            var activityTemplate = Extract_Spreadsheet_Data_v1_ActivityTemplate();

            return new ActionDTO()
            {
                Id = Guid.NewGuid(),
                Name = "Extract_Spreadsheet_Data",
                Label = "Extract Spreadsheet Data",
                AuthToken = Google_AuthToken(),
                ActivityTemplate = activityTemplate,
                ActivityTemplateId = activityTemplate.Id
            };
        }

        public ActionDTO Extract_Spreadsheet_Data_v1_Followup_Configuration_Request_ActionDTO_With_Crates()
        {

            var activityTemplate = Extract_Spreadsheet_Data_v1_ActivityTemplate();

            var curActionDto = new ActionDTO()
            {
                Id = Guid.NewGuid(),
                Name = "Extract_Spreadsheet_Data",
                Label = "Extract Spreadsheet Data",
                AuthToken = Google_AuthToken(),
                ActivityTemplate = activityTemplate,
                ActivityTemplateId = activityTemplate.Id,
            };
            using (var updater = CrateManager.UpdateStorage(curActionDto))
            {
                updater.CrateStorage.;
            }
            return curActionDto;

        }

        private static StandardTableDataCM Standard_Table_Data_Column_Only()
        {
            return new StandardTableDataCM()
            {
                FirstRowHeaders = true,
                Table = new List<TableRowDTO>()
                {
                    new TableRowDTO()
                    {
                       Row = new List<TableCellDTO>()
                       {
                           TableCellDTO.Create("(1,1)","_cn6ca")
                       }
                    },
                    new TableRowDTO()
                    {
                       Row = new List<TableCellDTO>()
                       {
                          TableCellDTO.Create("(2,1)","_cokwr")
                       }

                    }
                }
            };

        }
        private Crate PackCrate_GoogleSpreadsheets()
        {
            Crate crate;

            var curFields = new List<FieldDTO>()
            {
                new FieldDTO() { Key = "Column_Only", Value = @"https://spreadsheets.google.com/feeds/spreadsheets/private/full/1L2TxytQKnYLtHlB3fZ4lb91FKSmmoFk6FJipuDW0gWo" },
                new FieldDTO() { Key = "Row_Only", Value = "https://spreadsheets.google.com/feeds/spreadsheets/private/full/126yxCJDSZHJoR6d8BYk0wW7tZpl2pcl29F8QXIYVGMQ"},
                new FieldDTO() {Key = "Row_And_Column", Value = "https://spreadsheets.google.com/feeds/spreadsheets/private/full/1v67fCdV9NItrKRgLHPlp3CS2ia9duUkwKQOAUcQciJ0"}
            }.ToArray();
            crate = CrateManager.CreateDesignTimeFieldsCrate("Select a Google Spreadsheet", curFields);

            return crate;
        }

        public StandardFileHandleMS GetUpstreamCrate()
        {
            return new StandardFileHandleMS
            {
                DockyardStorageUrl = "https://spreadsheets.google.com/feeds/spreadsheets/private/full/1L2TxytQKnYLtHlB3fZ4lb91FKSmmoFk6FJipuDW0gWo",
                Filename = "Column_Only",
                Filetype = "Google_Spreadsheet"
            };
        }


    }
}
