﻿using System;
using System.Threading.Tasks;
using Data.Control;
using Data.Crates;
using Data.Entities;
using Data.Interfaces.DataTransferObjects;
using Hub.Managers;
using TerminalBase.BaseClasses;
using TerminalBase.Infrastructure;
using terminalFr8Core.Infrastructure;

namespace terminalFr8Core.Actions
{

    public class ManageRoute_v1 : BaseTerminalActivity

    {
        private readonly FindObjectHelper _findObjectHelper = new FindObjectHelper();


        #region Configuration.

        public override ConfigurationRequestType ConfigurationEvaluator(ActivityDO curActivityDO)
        {
            if (Crate.IsStorageEmpty(curActivityDO))

            {
                return ConfigurationRequestType.Initial;
            }
            else
            {
                return ConfigurationRequestType.Followup;
            }
        }

        protected override Task<ActivityDO> InitialConfigurationResponse(ActivityDO curActivityDO, AuthorizationTokenDO authTokenDO)
        {
            using (var updater = Crate.UpdateStorage(curActivityDO))

            {
                var crateStorage = updater.CrateStorage;
                AddRunNowButton(crateStorage);
            }

            return Task.FromResult(curActivityDO);

        }

        private void AddRunNowButton(CrateStorage crateStorage)
        {
            AddControl(crateStorage,
                new RunRouteButton()
                {
                    Name = "RunRoute",
                    Label = "Run Route",
                });

            AddControl(crateStorage,
                new ControlDefinitionDTO(ControlTypes.ManageRoute)
                {
                    Name = "ManageRoute",
                    Label = "Manage Route"
                });
        }

        #endregion Configuration.


        #region Execution.

        public async Task<PayloadDTO> Run(ActivityDO curActionDTO, Guid containerId, AuthorizationTokenDO authTokenDO)
        {
            return Success(await GetPayload(curActionDTO, containerId));
        }

        #endregion Execution.
    }
}