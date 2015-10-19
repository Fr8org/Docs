﻿using System;
using System.Collections.Generic;
using System.Linq;
using Data.Entities;
using Data.Interfaces;
using Data.Interfaces.DataTransferObjects;
using Data.States;
using Newtonsoft.Json;

namespace Data.Migrations
{
    partial class MigrationConfiguration
    {
        private class RouteBuilder
        {
            private readonly string _name;
            
            

            private readonly List<CrateDTO> _crates = new List<CrateDTO>();
            private int _ptId;

            
            

            public RouteBuilder(string name)
            {
                _name = name;
            }

            

            public RouteBuilder AddCrate(CrateDTO crateDto)
            {
                _crates.Add(crateDto);
                return this;
            }

            

            public void Store(IUnitOfWork uow)
            {
                StoreTemplate(uow);

                var container = uow.ContainerRepository.GetQuery().FirstOrDefault(x => x.Name == _name);

                var add = container == null;
                
                if (add)
                {
                    container = new ContainerDO();
                }

                ConfigureProcess(container);

                if (add)
                {
                    uow.ContainerRepository.Add(container);
                }
            }

            

            private void StoreTemplate(IUnitOfWork uow)
            {
                var route = uow.RouteRepository.GetQuery().FirstOrDefault(x => x.Name == _name);
                bool add = route == null;

                if (add)
                {
                    route = new RouteDO();
                }

                route.Name = _name;
                route.Description = "Template for testing";
                route.CreateDate = DateTime.Now;
                route.LastUpdated = DateTime.Now;
                route.RouteState = RouteState.Inactive; // we don't want this process template can be executed ouside of tests

                if (add)
                {
                    uow.RouteRepository.Add(route);
                    uow.SaveChanges();
                }

                _ptId = route.Id;
            }

            

            private void ConfigureProcess(ContainerDO container)
            {
                container.Name = _name;
                container.RouteId = _ptId;
                container.ContainerState = ContainerState.Executing;

                container.CrateStorage = JsonConvert.SerializeObject(new
                {
                    crates = _crates
                });
            }

            
        }
    }
}
