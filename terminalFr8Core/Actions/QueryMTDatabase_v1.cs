using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.Internal;
using Newtonsoft.Json;
using Data.Interfaces;
using Data.Interfaces.DataTransferObjects;
using Data.Interfaces.Manifests;
using TerminalBase.Infrastructure;
using TerminalBase.BaseClasses;
using Data.Entities;
using StructureMap;
using Hub.Managers;
using Data.Control;
using Data.Crates;
using Data.States;
using terminalFr8Core.Infrastructure;
using TerminalBase.Services;

namespace terminalFr8Core.Actions
{
    public class QueryMTDatabase_v1 : BaseTerminalActivity
    {
        public class ActionUi : StandardConfigurationControlsCM
        {
            [JsonIgnore]
            public DropDownList AvailableObjects { get; set; }

            [JsonIgnore]
            public FilterPane Filter { get; set; }

            public ActionUi()
            {
                Controls = new List<ControlDefinitionDTO>();

                Controls.Add(AvailableObjects = new DropDownList
                {
                    Label = "Object List",
                    Name = "AvailableObjects",
                    Value = null,
                    Events = new List<ControlEvent> { ControlEvent.RequestConfig },
                    Source = new FieldSourceDTO
                    {
                        Label = "Queryable Objects",
                        ManifestType = CrateManifestTypes.StandardDesignTimeFields
                    }
                });

                Controls.Add(Filter = new FilterPane
                {
                    Label = "Find all Fields where:",
                    Name = "Filter",
                    Required = true,
                    Source = new FieldSourceDTO
                    {
                        Label = "Queryable Criteria",
                        ManifestType = CrateManifestTypes.StandardDesignTimeFields
                    }
                });
            }
        }

        public override async Task<ActivityDO> Configure(ActivityDO curActionDataPackageDO, AuthorizationTokenDO authTokenDO)
        {
            return await ProcessConfigurationRequest(curActionDataPackageDO, ConfigurationEvaluator, authTokenDO);
        }

        public override ConfigurationRequestType ConfigurationEvaluator(ActivityDO curActivityDO)
        {
            if (Crate.IsStorageEmpty(curActivityDO))
            {
                return ConfigurationRequestType.Initial;
            }

            return ConfigurationRequestType.Followup;
        }

        protected override Task<ActivityDO> InitialConfigurationResponse(ActivityDO curActivityDO, AuthorizationTokenDO authTokenDO)
        {
            var objectList = GetObjects();

            using (var updater = Crate.UpdateStorage(curActivityDO))
            {
                updater.CrateStorage.Add(PackControls(new ActionUi()));
                updater.CrateStorage.Add(Crate.CreateDesignTimeFieldsCrate("Queryable Objects", objectList.ToArray()));
                updater.CrateStorage.Add(Data.Crates.Crate.FromContent("Found MT Objects", new StandardDesignTimeFieldsCM(new FieldDTO
                {
                    Key = "Found MT Objects",
                    Value = "Table",
                    Availability = AvailabilityType.RunTime
                })));
            }

            return Task.FromResult(curActivityDO);
        }

        protected override Task<ActivityDO> FollowupConfigurationResponse(ActivityDO curActivityDO, AuthorizationTokenDO authTokenDO)
        {
            using (var updater = Crate.UpdateStorage(curActivityDO))
            {
                var ui = Crate.GetStorage(curActivityDO).CrateContentsOfType<StandardConfigurationControlsCM>().SingleOrDefault();

                if (ui == null)
                {
                    throw new InvalidOperationException("Action was not configured correctly");
                }

                var config = new ActionUi();
                config.ClonePropertiesFrom(ui);
                int selectedObjectId;

                updater.CrateStorage.RemoveByLabel("Queryable Criteria");

                if (int.TryParse(config.AvailableObjects.Value, out selectedObjectId))
                {
                    updater.CrateStorage.Add(Crate.CreateDesignTimeFieldsCrate("Queryable Criteria", GetFieldsByObjectId(selectedObjectId).ToArray()));
                }
            }
            return Task.FromResult(curActivityDO);
        }

        public async Task<PayloadDTO> Run(ActivityDO curActivityDO, Guid containerId, AuthorizationTokenDO authTokenDO)
        {
            var payload = await GetPayload(curActivityDO, containerId);
            var payloadCrateStorage = Crate.GetStorage(payload);
            var ui = Crate.GetStorage(curActivityDO).CrateContentsOfType<StandardConfigurationControlsCM>().SingleOrDefault();

            if (ui == null)
            {
                return Error(payload, "Action was not configured correctly");
            }

            var config = new ActionUi();

            config.ClonePropertiesFrom(ui);

            var criteria = JsonConvert.DeserializeObject<FilterDataDTO>(config.Filter.Value);
            int selectedObjectId;

            if (!int.TryParse(config.AvailableObjects.Value, out selectedObjectId))
            {
                return Error(payload, "Invalid object selected");
            }

            //STARTING NASTY CODE
            //TODO discuss this with Alex (bahadir)
            var envIdCondition = criteria.Conditions.FirstOrDefault(c => c.Field == "EnvelopeId");
            if (envIdCondition != null && envIdCondition.Value == "FromPayload")
            {
                envIdCondition.Value = GetCurrentEnvelopeId(payloadCrateStorage);
            }
            //END OF NASTY CODE

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var obj = uow.MTObjectRepository.GetQuery().FirstOrDefault(x => x.Id == selectedObjectId);
                if (obj == null)
                {
                    return Error(payload, "Invalid object selected");
                }

                Type manifestType;
                if (!ManifestDiscovery.Default.TryResolveType(new CrateManifestType(null, obj.ManifestId), out manifestType))
                {
                    return Error(payload, string.Format("Unknown manifest id: {0}", obj.ManifestId));
                }

                var queryBuilder = MTSearchHelper.CreateQueryProvider(manifestType);
                var converter = CrateManifestToRowConverter(manifestType);
                var foundObjects = queryBuilder.Query(uow, authTokenDO.UserID, 
                    criteria.ExecutionType == FilterExecutionType.WithoutFilter ? new List<FilterConditionDTO>() : criteria.Conditions).ToArray();

                var searchResult = new StandardPayloadDataCM();

                foreach (var foundObject in foundObjects)
                {
                    searchResult.PayloadObjects.Add(converter(foundObject));
                }
                
                using (var updater = Crate.UpdateStorage(payload))
                {
                    updater.CrateStorage.Add(Data.Crates.Crate.FromContent("Found MT Objects", searchResult));
                }
            }

            return Success(payload);
        }

        private string GetCurrentEnvelopeId(CrateStorage storage)
        {
            var envelopePayloadCrate = storage.CrateContentsOfType<StandardPayloadDataCM>(c => c.Label == "DocuSign Envelope Payload Data").Single();
            var envelopeId = envelopePayloadCrate.PayloadObjects.SelectMany(o => o.PayloadObject).Single(po => po.Key == "EnvelopeId").Value;
            return envelopeId;
        }

        private Func<object, PayloadObjectDTO> CrateManifestToRowConverter(Type manifestType)
        {
            var accessors = new List<KeyValuePair<string, IMemberAccessor>>();

            foreach (var member in manifestType.GetMembers(BindingFlags.Instance | BindingFlags.Public).OrderBy(x => x.Name))
            {
                IMemberAccessor accessor;

                if (member is FieldInfo)
                {
                    accessor = ((FieldInfo) member).ToMemberAccessor();
                }
                else if (member is PropertyInfo && !((PropertyInfo)member).IsSpecialName)
                {
                    accessor = ((PropertyInfo)member).ToMemberAccessor();
                }
                else
                {
                    continue;
                }

                accessors.Add(new KeyValuePair<string, IMemberAccessor>(member.Name, accessor));    
            }

            return x =>
            {
                var row = new PayloadObjectDTO();

                foreach (var accessor in accessors)
                {
                    row.PayloadObject.Add(new FieldDTO(accessor.Key, string.Format(CultureInfo.InvariantCulture, "{0}", accessor.Value.GetValue(x))));
                }

                return row;
            };
        }

        private IEnumerable<FieldDTO> GetFieldsByObjectId(int objectId)
        {
            var fields = new Dictionary<string, string>();

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                foreach (var field in uow.MTFieldRepository.GetQuery().Where(x => x.MT_ObjectId == objectId))
                {
                    var alias = "Value" + field.FieldColumnOffset;
                    string existingAlias;

                    if (fields.TryGetValue(field.Name, out existingAlias))
                    {
                        if (existingAlias != alias)
                        {
                            throw new InvalidOperationException(string.Format("Duplicate field definition. MT object type: {0}. Field {1} is mapped to {2} and {3}", objectId, field.Name, existingAlias, alias));
                        }
                    }
                    else
                    {
                        fields[field.Name] = alias;
                    }
                }
            }

            return fields.OrderBy(x => x.Key).Select(x => new FieldDTO(x.Key, x.Key));
        }
        
        private IEnumerable<FieldDTO> GetObjects()
        {
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                return uow.MTObjectRepository.GetAll().Select(c => new FieldDTO(c.Name, c.Id.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }
}