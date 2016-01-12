﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StructureMap;
using Data.Crates;
using Data.Entities;
using Data.Interfaces.DataTransferObjects;
using Data.States;
using Hub.Interfaces;
using Hub.Managers.APIManagers.Transmitters.Restful;
using Utilities.Configuration.Azure;
using Data.Constants;

namespace TerminalBase.Infrastructure
{
    public class DefaultHubCommunicator : IHubCommunicator
    {
        private readonly IRouteNode _routeNode;
        private readonly IRestfulServiceClient _restfulServiceClient;

        public DefaultHubCommunicator()
        {
            _routeNode = ObjectFactory.GetInstance<IRouteNode>();
            _restfulServiceClient = ObjectFactory.GetNamedInstance<IRestfulServiceClient>("HMACRestfulServiceClient");
        }

        public Dictionary<string, string> GetUserIdHeader(string userId)
        {
            return new Dictionary<string, string>()
            {
                { "fr8UserId" , userId}
            };
        }

        public Task<PayloadDTO> GetPayload(ActionDO actionDO, Guid containerId, string userId)
        {
            var url = CloudConfigurationManager.GetSetting("CoreWebServerUrl")
                + "api/" + CloudConfigurationManager.GetSetting("HubApiVersion") + "/containers?id="
                + containerId.ToString("D");

            var payloadDTOTask = _restfulServiceClient.GetAsync<PayloadDTO>(new Uri(url, UriKind.Absolute), containerId.ToString(), GetUserIdHeader(userId));

            return payloadDTOTask;
        }

        public Task<List<Crate<TManifest>>> GetCratesByDirection<TManifest>(ActionDO actionDO, CrateDirection direction, string userId)
        {
            return _routeNode.GetCratesByDirection<TManifest>(actionDO.Id, direction);
        }

        public Task<List<Crate>> GetCratesByDirection(ActionDO actionDO, CrateDirection direction, string userId)
        {
            return _routeNode.GetCratesByDirection(actionDO.Id, direction);
        }

        public async Task CreateAlarm(AlarmDTO alarmDTO, string userId)
        {
            var hubAlarmsUrl = CloudConfigurationManager.GetSetting("CoreWebServerUrl")
                + "api/" + CloudConfigurationManager.GetSetting("HubApiVersion") + "/alarms";

            await _restfulServiceClient.PostAsync(new Uri(hubAlarmsUrl), alarmDTO, null, GetUserIdHeader(userId));
        }

        public async Task<List<ActivityTemplateDTO>> GetActivityTemplates(ActionDO actionDO, string userId)
        {
            var hubUrl = CloudConfigurationManager.GetSetting("CoreWebServerUrl") 
                + "api/" + CloudConfigurationManager.GetSetting("HubApiVersion") + "/routenodes/available";

            var allCategories = await _restfulServiceClient.GetAsync<IEnumerable<ActivityTemplateCategoryDTO>>(new Uri(hubUrl), null, GetUserIdHeader(userId));

            var templates = allCategories.SelectMany(x => x.Activities);
            return templates.ToList();
        }

        public async Task<List<ActivityTemplateDTO>> GetActivityTemplates(ActionDO actionDO, ActivityCategory category, string userId)
        {
            var allTemplates = await GetActivityTemplates(actionDO, userId);
            var templates = allTemplates.Where(x => x.Category == category);

            return templates.ToList();
        }

        public async Task<List<ActivityTemplateDTO>> GetActivityTemplates(ActionDO actionDO, string tag, string userId)
        {
            var hubUrl = CloudConfigurationManager.GetSetting("CoreWebServerUrl")
                + "api/" + CloudConfigurationManager.GetSetting("HubApiVersion") + "/routenodes/available?tag=";

            if (string.IsNullOrEmpty(tag))
            {
                hubUrl += "[all]";
            }
            else
            {
                hubUrl += tag;
            }

            var templates = await _restfulServiceClient.GetAsync<List<ActivityTemplateDTO>>(new Uri(hubUrl), null, GetUserIdHeader(userId));

            return templates;
        }

        public Task<List<FieldValidationResult>> ValidateFields(List<FieldValidationDTO> fields, string userId)
        {
            var url = CloudConfigurationManager.GetSetting("CoreWebServerUrl")
                      + "api/" + CloudConfigurationManager.GetSetting("HubApiVersion") + "/field/exists";

            return _restfulServiceClient.PostAsync<List<FieldValidationDTO>, List<FieldValidationResult>>(new Uri(url), fields, null, GetUserIdHeader(userId));
        }
    }
}
