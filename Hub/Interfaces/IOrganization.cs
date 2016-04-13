﻿using Data.Entities;
using Data.Interfaces;

namespace Hub.Interfaces
{
    public interface IOrganization
    {
        /// <summary>
        /// Check if already exists some organization with the same name and create new if not
        /// </summary>
        /// <param name="uow"></param>
        /// <param name="organizationName"></param>
        /// <param name="isNewOrganization"></param>
        /// <returns></returns>
        OrganizationDO GetOrCreateOrganization(IUnitOfWork uow, string organizationName, out bool isNewOrganization);
    }
}
