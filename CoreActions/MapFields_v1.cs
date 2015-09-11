﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using Core.Interfaces;
using Data.Entities;
using Data.Interfaces.DataTransferObjects;
using PluginBase.BaseClasses;
using PluginBase.Infrastructure;
using StructureMap;

namespace CoreActions
{
    public class MapFields_v1 : BasePluginAction
    {
        private class CrateConfigurationDTO
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("label")]
            public string Label { get; set; }
        }

        /// <summary>
        /// Action processing infrastructure.
        /// </summary>
        public ActionProcessResultDTO Execute(ActionDO actionDO)
        {
            // ((ActionListDO)actionDO.ParentActivity).Process.Payload;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Configure infrastructure.
        /// </summary>
        public CrateStorageDTO Configure(ActionDO actionDO)
        {
            return ProcessConfigurationRequest(actionDO, ConfigurationEvaluator);
        }

        private void FillCrateConfigureList(IEnumerable<ActionDO> actions,
            List<CrateConfigurationDTO> crateConfigList)
        {
            foreach (var curAction in actions)
            {
                var curCrateStorage = curAction.CrateStorageDTO();
                foreach (var curCrate in curCrateStorage.CratesDTO)
                {
                    crateConfigList.Add(new CrateConfigurationDTO()
                    {
                        Id = curCrate.Id,
                        Label = curCrate.Label
                    });
                }
            }
        }

        /// <summary>
        /// Looks for upstream and downstream Creates.
        /// </summary>
        protected override CrateStorageDTO InitialConfigurationResponse(ActionDO actionDO)
        {
            var curActivityService = ObjectFactory.GetInstance<IActivity>();

            var curUpstreamActivities = curActivityService.GetUpstreamActivities(actionDO);
            var curDownstreamActivities = curActivityService.GetDownstreamActivities(actionDO);

            var curUpstreamFields = new List<CrateConfigurationDTO>();
            FillCrateConfigureList(curUpstreamActivities.OfType<ActionDO>(), curUpstreamFields);

            var curDownstreamFields = new List<CrateConfigurationDTO>();
            FillCrateConfigureList(curUpstreamActivities.OfType<ActionDO>(), curDownstreamFields);

            if (curUpstreamFields.Count == 0 || curDownstreamFields.Count == 0)
            {
                throw new ApplicationException("This action couldn't find either source fields or target fields (or both). "
                    + "Try configuring some Actions first, then try this page again.");
            }

            var curUpstreamJson = JsonConvert.SerializeObject(curUpstreamFields);
            var curDownstreamJson = JsonConvert.SerializeObject(curDownstreamFields);

            var curResultDTO = new CrateStorageDTO()
            {
                CratesDTO = new List<CrateDTO>()
                {
                    new CrateDTO()
                    {
                        Id = "Upstream Plugin-Provided Fields",
                        Label = "Upstream Plugin-Provided Fields",
                        Contents = curUpstreamJson
                    },

                    new CrateDTO()
                    {
                        Id = "Downstream Plugin-Provided Fields",
                        Label = "Downstream Plugin-Provided Fields",
                        Contents = curDownstreamJson
                    }
                }
            };

            return curResultDTO;
        }

        /// <summary>
        /// ConfigurationEvaluator always returns Initial,
        /// since Initial and FollowUp phases are the same for current action.
        /// </summary>
        private ConfigurationRequestType ConfigurationEvaluator(ActionDO curActionDO)
        {
            return ConfigurationRequestType.Initial;
        }
    }
}