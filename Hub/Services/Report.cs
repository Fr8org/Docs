﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Data.Entities;
using Data.Infrastructure.StructureMap;
using Data.Interfaces;
using Data.States;
using Data.Utility;
using Hub.Interfaces;
using StructureMap;
using Utilities;

namespace Hub.Services
{
    public class Report : IReport
    {
        private readonly ISecurityServices _security;
        private readonly IFact _fact;
        public Report()
        {
            _security = ObjectFactory.GetInstance<ISecurityServices>();
            _fact = ObjectFactory.GetInstance<IFact>();
        }
        /// <summary>
        /// Returns List of Fact
        /// </summary>
        /// <param name="uow">unit of work</param>
        /// <returns>List of Incident</returns>
        public IList<FactDO> GetAllFacts(IUnitOfWork uow)
        {
            //get the current account
            var curAccount = _security.GetCurrentAccount(uow);
            //get the roles to check if the account has admin role
            var curAccountRoles = curAccount.Roles;
            var curFacts = _fact.GetAll(uow, curAccountRoles);
            return curFacts;
        }
        /// <summary>
        /// This method returns Incidents for Report
        /// </summary>
        /// <param name="uow"></param>
        /// <param name="page">The page number</param>
        /// <param name="pageSize">Number of incidents to show per page</param>
        /// <param name="allIncidents">This marks if all incidents should be returned or only having current user Id</param>
        /// <returns></returns>
        public List<IncidentDO> GetTopIncidents(IUnitOfWork uow, int page, int pageSize, bool getCurrentUserIncidents, int numOfIncidents)
        {
            //get the current account
            var curAccount = _security.GetCurrentAccount(uow);
            //get the roles to check if the account has admin role
            var curAccountRoles = curAccount.Roles;
            //prepare variable for incidents
            var curIncidents = new List<IncidentDO>();
            var adminRoleId = uow.AspNetRolesRepository.GetQuery().Single(r => r.Name == "Admin").Id;
            //if this user does not have Admin role return empty list
            if(curAccountRoles != null && curAccountRoles.All(x => x.RoleId != adminRoleId))
                return curIncidents;
            //if user has Admin role and asked for only current user incidents
            if (getCurrentUserIncidents)
                curIncidents = uow.IncidentRepository.GetQuery()
                    .Where(i => i.CustomerId == curAccount.Id)
                    .OrderByDescending(i => i.CreateDate)
                    .Take(numOfIncidents)
                    .Page(page, pageSize)
                    .ToList();
            //in the other case return incidents generated by all users
            else
            {
                curIncidents = uow.IncidentRepository.GetQuery()
                    .OrderByDescending(i => i.CreateDate)
                    .Take(numOfIncidents)
                    .Page(page, pageSize)
                    .ToList();
            }
            return curIncidents;
        }
    }
}
