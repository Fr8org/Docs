﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Control;
using Data.Crates;
using Data.Interfaces.DataTransferObjects;
using Data.Interfaces.Manifests;
using Data.States;
using HealthMonitor.Utility;
using Hub.Managers;
using NUnit.Framework;
using UtilitiesTesting.Fixtures;

namespace terminaBaselTests.Tools.Activities
{
    public class IntegrationTestTools_terminalDocuSign
    {
        private readonly BaseHubIntegrationTest _baseHubITest;
        private Terminals.IntegrationTestTools_terminalDocuSign _terminalDocuSignTestTools;


        public IntegrationTestTools_terminalDocuSign(BaseHubIntegrationTest baseHubIntegrationTest)
        {
            _baseHubITest = baseHubIntegrationTest;
            _terminalDocuSignTestTools = new Terminals.IntegrationTestTools_terminalDocuSign(_baseHubITest);
        }

        /// <summary>
        /// Helper method for creating new Mail_Merge_Into_DocuSign solution and configure that solution with chosen data source and docuSign template.
        /// After solution is created, check for authentication and authenticate if needed to DocuSign.
        /// </summary>
        /// <param name="dataSourceValue">Value property for DataSource dropDownList</param>
        /// <param name="dataSourceSelectedKey">selectedKey property for DataSource dropdownList</param>
        /// <param name="docuSignTemplateValue">Value property for DocuSignTemplate dropDownList</param>
        /// <param name="docuSignTemplateSelectedKey">>selectedKey property for DocuSignTemplate dropdownList</param>
        /// <param name="addNewDocuSignTemplate">add provided DocuSign Template values as new ListItem in DocuSignTemplate DropDownList</param>
        /// <returns>
        ///  Objects that are created from this solution and can be reused for different scenarios
        ///   Item1 from Tuple: new created Mail_Merge_Into_DocuSign solution as ActivityDTO with all children activities inside.
        ///   Item2 from Tuple: new created Plan associated with Mail_Merge_Into_DocuSign solution.
        ///   Item3 from Tuple: authorizationTokenId returned from DocuSign authentication process 
        /// </returns>
        public async Task<Tuple<ActivityDTO, PlanDTO, Guid>> CreateAndConfigure_MailMergeIntoDocuSign_Solution(string dataSourceValue,
            string dataSourceSelectedKey, string docuSignTemplateValue, string docuSignTemplateSelectedKey, bool addNewDocuSignTemplate)
        {
            var solutionCreateUrl = _baseHubITest.GetHubApiBaseUrl() + "activities/create?solutionName=Mail_Merge_Into_DocuSign";

            //
            // Create solution
            //
            var plan = await _baseHubITest.HttpPostAsync<string, PlanDTO>(solutionCreateUrl, null);
            var solution = plan.Plan.SubPlans.FirstOrDefault().Activities.FirstOrDefault();

            //
            // Send configuration request without authentication token
            //
            solution = await _baseHubITest.HttpPostAsync<ActivityDTO, ActivityDTO>(_baseHubITest.GetHubApiBaseUrl() + "activities/configure?id=" + solution.Id, solution);
            var crateStorage = _baseHubITest.Crate.FromDto(solution.CrateStorage);
            var stAuthCrate = crateStorage.CratesOfType<StandardAuthenticationCM>().FirstOrDefault();
            bool defaultDocuSignAuthTokenExists = stAuthCrate == null;

            var tokenGuid = Guid.Empty;
            if (!defaultDocuSignAuthTokenExists)
            {
                // Authenticate with DocuSign
                tokenGuid = await _terminalDocuSignTestTools.AuthenticateDocuSignAndAssociateTokenWithAction(solution.Id, _baseHubITest.GetDocuSignCredentials(), solution.ActivityTemplate.TerminalId);
            }

            //
            // Send configuration request with authentication token
            //
            solution = await _baseHubITest.HttpPostAsync<ActivityDTO, ActivityDTO>(_baseHubITest.GetHubApiBaseUrl() + "activities/configure?id=" + solution.Id, solution);
            crateStorage = _baseHubITest.Crate.FromDto(solution.CrateStorage);
            Assert.True(crateStorage.CratesOfType<StandardConfigurationControlsCM>().Any(), "Crate StandardConfigurationControlsCM is missing in API response.");

            //
            // Followup configuration 
            //
            var controlsCrate = crateStorage.CratesOfType<StandardConfigurationControlsCM>().First();
            var controls = controlsCrate.Content.Controls;
            // 
            // Set dataSource value and Key, Example "Get_Google_Sheet_Data", "Load_Excel_File"...
            //
            var dataSource = controls.OfType<DropDownList>().FirstOrDefault(c => c.Name == "DataSource");
            dataSource.Value = dataSourceValue;
            dataSource.selectedKey = dataSourceSelectedKey; 
            //
            // Set DocuSign template value 
            //
            var template = controls.OfType<DropDownList>().FirstOrDefault(c => c.Name == "DocuSignTemplate");
            template.Value = docuSignTemplateValue;
            template.selectedKey = docuSignTemplateSelectedKey;
            if (addNewDocuSignTemplate)
            {
                template.ListItems.Add(new ListItem() { Value = docuSignTemplateValue, Key = docuSignTemplateSelectedKey });
            }

            var button = controls.OfType<Button>().FirstOrDefault();
            button.Clicked = true;

            //
            //Rename plan to include a dateTimeStamp in the name
            //
            var newName = plan.Plan.Name + " | " + DateTime.UtcNow.ToShortDateString() + " " +
                DateTime.UtcNow.ToShortTimeString();
            await _baseHubITest.HttpPostAsync<object, PlanFullDTO>(_baseHubITest.GetHubApiBaseUrl() + "plans?id=" + plan.Plan.Id,
                new { id = plan.Plan.Id, name = newName });

            //
            // Configure solution
            //
            using (var crateStorageTemp = _baseHubITest.Crate.GetUpdatableStorage(solution))
            {
                crateStorageTemp.Remove<StandardConfigurationControlsCM>();
                crateStorageTemp.Add(controlsCrate);
            }
            solution = await _baseHubITest.HttpPostAsync<ActivityDTO, ActivityDTO>(_baseHubITest.GetHubApiBaseUrl() + "activities/configure?id=" + solution.Id, solution);

            Assert.AreEqual(2, solution.ChildrenActivities.Count(), "Solution child actions failed to create.");

            return new Tuple<ActivityDTO, PlanDTO, Guid>(solution, plan, tokenGuid);
        }

        public async Task<ActivityDTO> AddAndConfigure_QueryDocuSign(PlanDTO plan, int ordering)
        {
            var queryDocuSignActivity = FixtureData.Query_DocuSign_v1_InitialConfiguration();

            var activityCategoryParam = new ActivityCategory[] { ActivityCategory.Receivers };
            var activityTemplates = await _baseHubITest.HttpPostAsync<ActivityCategory[], List<WebServiceActivitySetDTO>>(_baseHubITest.GetHubApiBaseUrl() + "webservices/activities", activityCategoryParam);
            var apmActivityTemplate = activityTemplates.SelectMany(a => a.Activities).Single(a => a.Name == "Query_DocuSign");

            queryDocuSignActivity.ActivityTemplate = apmActivityTemplate;

            //connect current activity with a plan
            var subPlan = plan.Plan.SubPlans.FirstOrDefault();
            queryDocuSignActivity.ParentPlanNodeId = subPlan.SubPlanId;
            queryDocuSignActivity.RootPlanNodeId = plan.Plan.Id;
            queryDocuSignActivity.Ordering = ordering;

            //call initial configuration to server
            queryDocuSignActivity = await _baseHubITest.HttpPostAsync<ActivityDTO, ActivityDTO>(_baseHubITest.GetHubApiBaseUrl() + "activities/save", queryDocuSignActivity);
            //this call is without authtoken
            queryDocuSignActivity = await _baseHubITest.HttpPostAsync<ActivityDTO, ActivityDTO>(_baseHubITest.GetHubApiBaseUrl() + "activities/configure", queryDocuSignActivity);

            var initialcrateStorage = _baseHubITest.Crate.FromDto(queryDocuSignActivity.CrateStorage);

            var stAuthCrate = initialcrateStorage.CratesOfType<StandardAuthenticationCM>().FirstOrDefault();
            bool defaultDocuSignAuthTokenExists = stAuthCrate == null;

            //if (!defaultDocuSignAuthTokenExists)
            //{
            var terminalDocuSignTools = new Terminals.IntegrationTestTools_terminalDocuSign(_baseHubITest);
            queryDocuSignActivity.AuthToken = await terminalDocuSignTools.GenerateAuthToken("fr8test@gmail.com", "fr8mesomething", queryDocuSignActivity.ActivityTemplate.TerminalId);

            var applyToken = new ManageAuthToken_Apply()
            {
                ActivityId = queryDocuSignActivity.Id,
                AuthTokenId = Guid.Parse(queryDocuSignActivity.AuthToken.Token),
            };
            await _baseHubITest.HttpPostAsync<ManageAuthToken_Apply[], string>( _baseHubITest.GetHubApiBaseUrl() + "ManageAuthToken/apply", new ManageAuthToken_Apply[] { applyToken });
            //}

            //send configure with the auth token
            queryDocuSignActivity = await _baseHubITest.HttpPostAsync<ActivityDTO, ActivityDTO>(_baseHubITest.GetHubApiBaseUrl() + "activities/save", queryDocuSignActivity);
            queryDocuSignActivity = await _baseHubITest.HttpPostAsync<ActivityDTO, ActivityDTO>(_baseHubITest.GetHubApiBaseUrl() + "activities/configure", queryDocuSignActivity);

            initialcrateStorage = _baseHubITest.Crate.FromDto(queryDocuSignActivity.CrateStorage);

            Assert.True(initialcrateStorage.CratesOfType<StandardConfigurationControlsCM>().Any(),
                "Query_DocuSign: Crate StandardConfigurationControlsCM is missing in API response.");

            var controlsCrate = initialcrateStorage.CratesOfType<StandardConfigurationControlsCM>().First();
            //set the value of folder to drafts and 
            var controls = controlsCrate.Content.Controls;
            var folderControl = controls.OfType<DropDownList>().FirstOrDefault(c => c.Name == "Folder");
            Assert.IsNotNull(folderControl, "Query_DocuSign: DropDownList control for Folder value selection was not found");
            folderControl.Value = "Draft";
            folderControl.selectedKey = "Draft";

            //set the value of status to any
            var statusControl = controls.OfType<DropDownList>().FirstOrDefault(c => c.Name == "Status");
            Assert.IsNotNull(folderControl, "Query_DocuSign: DropDownList control for Status value selection was not found");
            statusControl.Value = null;
            statusControl.selectedKey = null;

            //call followup configuration
            using (var crateStorage = _baseHubITest.Crate.GetUpdatableStorage(queryDocuSignActivity))
            {
                crateStorage.Remove<StandardConfigurationControlsCM>();
                crateStorage.Add(controlsCrate);
            }
            queryDocuSignActivity = await _baseHubITest.HttpPostAsync<ActivityDTO, ActivityDTO>(_baseHubITest.GetHubApiBaseUrl() + "activities/save", queryDocuSignActivity);
            queryDocuSignActivity = await _baseHubITest.HttpPostAsync<ActivityDTO, ActivityDTO>(_baseHubITest.GetHubApiBaseUrl() + "activities/configure", queryDocuSignActivity);

            return await Task.FromResult(queryDocuSignActivity);
        }
    }
}
