﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Data.Entities;
using Data.States;
using Fr8.Infrastructure.Data.Control;
using Fr8.Infrastructure.Data.Crates;
using Fr8.Infrastructure.Data.DataTransferObjects;
using Fr8.Infrastructure.Data.Managers;
using Fr8.Infrastructure.Data.Manifests;
using Fr8.Infrastructure.Data.States;
using Fr8.Infrastructure.Interfaces;
using Fr8.Infrastructure.Utilities;
using Fr8.Infrastructure.Utilities.Configuration;
using Fr8.TerminalBase.Interfaces;
using Fr8.TerminalBase.Models;
using Fr8.TerminalBase.Services;
using log4net;
using StructureMap;
using terminalDocuSign.Interfaces;
using terminalDocuSign.Services.New_Api;

namespace terminalDocuSign.Services
{
    /// <summary>
    /// Service to create DocuSign related plans in Hub
    /// </summary>
    public class DocuSignPlan : IDocuSignPlan
    {
        private static readonly ILog Logger = LogManager.GetLogger(DocuSignManager.DocusignTerminalName);

        private readonly ICrateManager _crateManager;
        private readonly IDocuSignManager _docuSignManager;
        private readonly IDocuSignConnect _docuSignConnect;
        private readonly IHubEventReporter _eventReporter;

        private readonly string DevConnectName = "(dev) Fr8 Company DocuSign integration";
        private readonly string DemoConnectName = "(demo) Fr8 Company DocuSign integration";
        private readonly string ProdConnectName = "Fr8 Company DocuSign integration";
        private readonly string TemporaryConnectName = "int-tests-Fr8";


        public DocuSignPlan(ICrateManager crateManager, IDocuSignManager docuSignManager, IDocuSignConnect docuSignConnect, IHubEventReporter eventReporter)
        {
            _crateManager = crateManager;
            _docuSignManager = docuSignManager;
            _docuSignConnect = docuSignConnect;
            _eventReporter = eventReporter;
        }

        /// <summary>
        /// Creates Monitor All DocuSign Events plan with Record DocuSign Events and Store MT Data actions.
        /// 
        /// https://maginot.atlassian.net/wiki/display/DDW/Rework+of+DocuSign+connect+management
        /// </summary>
        public async Task CreatePlan_MonitorAllDocuSignEvents(IHubCommunicator hubCommunicator, AuthorizationToken authToken)
        {
            Logger.Info($"Create MADSE called {authToken.UserId}");
            string currentPlanId = await FindAndActivateExistingPlan(hubCommunicator, "MonitorAllDocuSignEvents", authToken);

            if (string.IsNullOrEmpty(currentPlanId))
            {
                await CreateAndActivateNewMADSEPlan(hubCommunicator, authToken);
            }
        }

        //only create a connect when running on dev/production
        public void CreateConnect(IHubCommunicator hubCommunicator, AuthorizationToken authToken)
        {
            Logger.Info($"CreateConnect called {authToken.UserId}");
            var authTokenDO = new AuthorizationTokenDO() { Token = authToken.Token, ExternalAccountId = authToken.ExternalAccountId };
            var config = _docuSignManager.SetUp(authToken);
            string terminalUrl = CloudConfigurationManager.GetSetting("terminalDocuSign.TerminalEndpoint");
            string prodUrl = CloudConfigurationManager.GetSetting("terminalDocuSign.DefaultProductionUrl");
            string devUrl = CloudConfigurationManager.GetSetting("terminalDocuSign.DefaultDevUrl");
            string demoUrl = CloudConfigurationManager.GetSetting("terminalDocuSign.DefaultDemoUrl");
            bool isSelfHosting = CloudConfigurationManager.GetSetting("terminalDocusign.NotSelfHosting") == null;
            string connectName = "";
            string connectId = "";

            Logger.Info($"CreateConnect terminalUrl {terminalUrl}");
            if (!isSelfHosting)
            {
                if (terminalUrl.Contains(devUrl, StringComparison.InvariantCultureIgnoreCase))
                {
                    connectName = DevConnectName;
                    // DS doesn't accept port in url, so instead of
                    //http://dev-terminals.fr8.co:53234/terminals/terminalDocuSign/events
                    // we should use
                    //http://dev-terminaldocusign.fr8.co/terminals/terminalDocuSign/events
                    terminalUrl = CloudConfigurationManager.GetSetting("terminalDocuSign.OverrideDevUrl");
                }
                else
                    if (terminalUrl.Contains(prodUrl, StringComparison.InvariantCultureIgnoreCase))
                {
                    connectName = ProdConnectName;
                }
                else
                    if (terminalUrl.Contains(demoUrl, StringComparison.InvariantCultureIgnoreCase))
                {
                    connectName = DemoConnectName;
                }
                else
                {
                    Logger.Info($"Unable to set connectName from {terminalUrl}");
                }

                string publishUrl = terminalUrl + "/terminals/terminalDocuSign/events";

                Logger.Info("Connect creation: publishUrl = {0}");

                if (!string.IsNullOrEmpty(connectName))
                {
                    connectId = _docuSignConnect.CreateOrActivateConnect(config, connectName, publishUrl);
                    Logger.Info($"Created connect named {connectName} pointing to {publishUrl} with id {connectId}");
                }
                else
                {
                    // terminal has a temporary url
                    var connectsInfo = _docuSignConnect.ListConnects(config);
                    var connects = connectsInfo.Where(a => a.name == TemporaryConnectName).ToList();
                    foreach (var connect in connects)
                    {
                        _docuSignConnect.DeleteConnect(config, connect.connectId);
                    }

                    connectId = _docuSignConnect.CreateConnect(config, TemporaryConnectName, publishUrl);
                    Logger.Info($"Created connect named {TemporaryConnectName} pointing to {publishUrl} with id {connectId}");
                }
            }
            else
            {
                Logger.Info($"terminalUrl is empty, no work has been done in DocuSignPlan.CreateConnect: prodUrl -> {prodUrl}, devUrl -> {devUrl}, demoUrl -> {demoUrl}");
            }
        }

        public void CreateOrUpdatePolling(IHubCommunicator hubCommunicator, AuthorizationToken authToken)
        {
            DocuSignPolling polling = new DocuSignPolling(_docuSignManager, _eventReporter);
            polling.SchedulePolling(hubCommunicator, authToken.ExternalAccountId);
        }


        private bool CheckIfSaveToFr8WarehouseConfiguredWithOldManifest(PlanDTO val)
        {
            return (_crateManager.GetStorage(val.SubPlans.ElementAt(0).Activities[1]).CrateContentsOfType<StandardConfigurationControlsCM>()
                     .First().FindByName("UpstreamCrateChooser") as UpstreamCrateChooser).SelectedCrates.Count > 1;
        }

        private async Task<string> FindAndActivateExistingPlan(IHubCommunicator hubCommunicator, string plan_name, AuthorizationToken authToken)
        {
            try
            {
                var existingPlans = (await hubCommunicator.GetPlansByName(plan_name, PlanVisibility.Internal)).ToList();
                if (existingPlans.Count > 0)
                {
                    //search for existing MADSE plan for this DS account and updating it

                    //1 Split existing plans in obsolete/malformed and new
                    var plans = existingPlans.GroupBy
                        (val =>
                        //first condition
                        val.SubPlans.Any() &&
                        //second condition
                        val.SubPlans.ElementAt(0).Activities.Any() &&
                        //third condtion
                        _crateManager.GetStorage(val.SubPlans.ElementAt(0).Activities[0]).FirstOrDefault(t => t.Label == "DocuSignUserCrate") != null &&
                        //fourth condition -> check if SaveToFr8Warehouse configured with old manifests
                        !(plan_name == "MonitorAllDocuSignEvents" && CheckIfSaveToFr8WarehouseConfiguredWithOldManifest(val))

                      ).ToDictionary(g => g.Key, g => g.ToList());

                    //trying to find an existing plan for this DS account
                    if (plans.ContainsKey(true))
                    {
                        List<PlanDTO> newPlans = plans[true];

                        existingPlans = newPlans.Where(
                              a => a.SubPlans.Any(b =>
                                 _crateManager.GetStorage(b.Activities[0])
                                  .FirstOrDefault(t => t.Label == "DocuSignUserCrate").Get<StandardPayloadDataCM>().GetValues("DocuSignUserEmail").FirstOrDefault() == authToken.ExternalAccountId)).ToList();

                        if (existingPlans.Count > 1)
                        {
                            Logger.Error($"Multiple Monitor_All_DocuSign_Events plans were created for one DocuSign account: {authToken.ExternalAccountId}") ;
                        }

                        var existingPlan = existingPlans.FirstOrDefault();

                        if (existingPlan != null)
                        {
                            var firstActivity = existingPlan.SubPlans.FirstOrDefault(a => a.Activities.Count > 0).Activities[0];

                            if (firstActivity != null)
                            {
                                await hubCommunicator.ApplyNewToken(firstActivity.Id, Guid.Parse(authToken.Id));
                                await hubCommunicator.RunPlan(existingPlan.Id, null);
                                Logger.Info($"#### Existing MADSE plan activated with planId: {existingPlan.Id}");
                                return existingPlan.Id.to_S();
                            }
                        }
                    }

                    //removing obsolete/malformed plans
                    if (plans.ContainsKey(false))
                    {
                        List<PlanDTO> obsoletePlans = plans[false];
                        Logger.Info($"#### Found {obsoletePlans.Count} obsolete MADSE plans");
                        foreach (var obsoletePlan in obsoletePlans)
                        {
                            await hubCommunicator.DeletePlan(obsoletePlan.Id);
                        }
                    }
                }
            }
            // if anything bad happens we would like not to create a new MADSE plan and fail loudly
            catch (Exception exc) { throw new ApplicationException("Couldn't update an existing Monitor_All_DocuSign_Events plan", exc); };

            return null;
        }

        private async Task CreateAndActivateNewMADSEPlan(IHubCommunicator hubCommunicator, AuthorizationToken authToken)
        {
            var emptyMonitorPlan = new PlanNoChildrenDTO
            {
                Name = "MonitorAllDocuSignEvents",
                Description = "MonitorAllDocuSignEvents",
                PlanState = "Active",
                Visibility = new PlanVisibilityDTO() { Hidden = true }
            };

            var monitorDocusignPlan = await hubCommunicator.CreatePlan(emptyMonitorPlan);
            var activityTemplates = await hubCommunicator.GetActivityTemplates(null);
            var recordDocusignEventsTemplate = GetActivityTemplate(activityTemplates, "Prepare_DocuSign_Events_For_Storage");
            var storeMTDataTemplate = GetActivityTemplate(activityTemplates, "Save_To_Fr8_Warehouse");
            Debug.WriteLine($"Calling create and configure with params {recordDocusignEventsTemplate} {authToken.UserId} {monitorDocusignPlan}");
            await hubCommunicator.CreateAndConfigureActivity(recordDocusignEventsTemplate.Id, "Record DocuSign Events", 1, monitorDocusignPlan.StartingSubPlanId, false, new Guid(authToken.Id));
            var storeMTDataActivity = await hubCommunicator.CreateAndConfigureActivity(storeMTDataTemplate.Id, "Save To Fr8 Warehouse", 2, monitorDocusignPlan.StartingSubPlanId);
            SetSelectedCrates(storeMTDataActivity);
            //save this
            await hubCommunicator.ConfigureActivity(storeMTDataActivity);
            await hubCommunicator.RunPlan(monitorDocusignPlan.Id, null);

            Logger.Info($"#### New MADSE plan activated with planId: {monitorDocusignPlan.Id}");
        }

        private void SetSelectedCrates(ActivityPayload storeMTDataActivity)
        {
            var crateStorage = storeMTDataActivity.CrateStorage;
            var configControlCM = crateStorage
                .CrateContentsOfType<StandardConfigurationControlsCM>()
                .First();
            var upstreamCrateChooser = (UpstreamCrateChooser)configControlCM.FindByName("UpstreamCrateChooser");
            var existingDdlbSource = upstreamCrateChooser.SelectedCrates[0].ManifestType.Source;
            var existingLabelDdlb = upstreamCrateChooser.SelectedCrates[0].Label;
            var docusignEnvelope = new DropDownList
            {
                selectedKey = Fr8.Infrastructure.Data.Constants.MT.DocuSignEnvelope_v2.ToString(),
                Value = ((int)Fr8.Infrastructure.Data.Constants.MT.DocuSignEnvelope_v2).ToString(),
                Name = "UpstreamCrateChooser_mnfst_dropdown_0",
                Source = existingDdlbSource
            };

            upstreamCrateChooser.SelectedCrates = new List<CrateDetails>
            {
                new CrateDetails { ManifestType = docusignEnvelope, Label = existingLabelDdlb }
            };
        }

        private ActivityTemplateDTO GetActivityTemplate(IEnumerable<ActivityTemplateDTO> activityList, string activityTemplateName)
        {
            var template = activityList.FirstOrDefault(x => x.Name == activityTemplateName);
            if (template == null)
            {
                throw new Exception(string.Format("ActivityTemplate {0} was not found", activityTemplateName));
            }

            return template;
        }
    }
}