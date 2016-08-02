﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.Internal;
using Newtonsoft.Json;
using StructureMap;
using Data.Interfaces;
using Fr8.Infrastructure.Data.Control;
using Fr8.Infrastructure.Data.Crates;
using Fr8.Infrastructure.Data.DataTransferObjects;
using Fr8.Infrastructure.Data.Managers;
using Fr8.Infrastructure.Data.Manifests;
using Fr8.Infrastructure.Data.States;
using Fr8.TerminalBase.BaseClasses;
using Hub.Services;
using Hub.Services.MT;

namespace terminalFr8Core.Actions
{
    public class Get_Data_From_Fr8_Warehouse_v1
        : TerminalActivity<Get_Data_From_Fr8_Warehouse_v1.ActivityUi>
    {
        private readonly IContainer _container;

        public static ActivityTemplateDTO ActivityTemplateDTO = new ActivityTemplateDTO
        {
            Id = new Guid("826bf794-7608-4194-8d5e-7350df9adf65"),
            Name = "Get_Data_From_Fr8_Warehouse",
            Label = "Get Data From Fr8 Warehouse",
            Category = Fr8.Infrastructure.Data.States.ActivityCategory.Processors,
            Version = "1",
            MinPaneWidth = 550,
            WebService = TerminalData.WebServiceDTO,
            Categories = new[]
            {
                ActivityCategories.Process,
                new ActivityCategoryDTO(TerminalData.WebServiceDTO.Name, TerminalData.WebServiceDTO.IconPath)
            }
        };
        protected override ActivityTemplateDTO MyTemplate => ActivityTemplateDTO;

        public class ActivityUi : StandardConfigurationControlsCM
        {
            public DropDownList AvailableObjects { get; set; }

            public TextBlock SelectObjectLabel { get; set; }

            public QueryBuilder QueryBuilder { get; set; }

            public ActivityUi()
            {
                AvailableObjects = new DropDownList()
                {
                    Label = "Object List",
                    Name = "AvailableObjects",
                    Value = null,
                    Events = new List<ControlEvent> { ControlEvent.RequestConfig },
                    Source = null
                };
                Controls.Add(AvailableObjects);

                SelectObjectLabel = new TextBlock()
                {
                    Value = "Please select object before specifying the query.",
                    Name = "SelectObjectLabel",
                    CssClass = "well well-lg",
                    IsHidden = false
                };
                Controls.Add(SelectObjectLabel);

                QueryBuilder = new QueryBuilder()
                {
                    Label = "Find all Fields where:",
                    Name = "QueryBuilder",
                    Required = true,
                    Source = new FieldSourceDTO
                    {
                        Label = "Queryable Criteria",
                        ManifestType = CrateManifestTypes.StandardDesignTimeFields
                    },
                    IsHidden = true
                };
                Controls.Add(QueryBuilder);
            }
        }


        private const string RunTimeCrateLabel = "Table Generated by Get Data From Fr8 Warehouse";

        public Get_Data_From_Fr8_Warehouse_v1(ICrateManager crateManager, IContainer container)
            : base(crateManager)
        {
            _container = container;
        }

        public override async Task Initialize()
        {
            ActivityUI.AvailableObjects.ListItems = GetObjects();
            CrateSignaller.MarkAvailableAtRuntime<StandardTableDataCM>(RunTimeCrateLabel);
            await Task.Yield();
        }

        public override async Task FollowUp()
        {
            var selectedObject = ActivityUI.AvailableObjects.Value;
            var hasSelectedObject = !string.IsNullOrEmpty(selectedObject);
            if (hasSelectedObject)
            {
                Guid selectedObjectId;
                if (Guid.TryParse(ActivityUI.AvailableObjects.Value, out selectedObjectId))
                {
                    using (var uow = _container.GetInstance<IUnitOfWork>())
                    {
                        Storage.ReplaceByLabel(
                            Crate.FromContent("Queryable Criteria", new FieldDescriptionsCM(MTTypesHelper.GetFieldsByTypeId(uow, selectedObjectId, AvailabilityType.RunTime))));
                    }
                }
            }

            ActivityUI.QueryBuilder.IsHidden = !hasSelectedObject;
            ActivityUI.SelectObjectLabel.IsHidden = hasSelectedObject;
            CrateSignaller.MarkAvailableAtRuntime<StandardTableDataCM>(RunTimeCrateLabel);

            await Task.Yield();
        }

        public override async Task Run()
        {
            using (var uow = _container.GetInstance<IUnitOfWork>())
            {
                var selectedObjectId = Guid.Parse(ActivityUI.AvailableObjects.Value);
                var mtType = uow.MultiTenantObjectRepository.FindTypeReference(selectedObjectId);
                if (mtType == null)
                {
                    throw new ApplicationException("Invalid object selected.");
                }

                var conditions = JsonConvert.DeserializeObject<List<FilterConditionDTO>>(
                    ActivityUI.QueryBuilder.Value
                );

                var manifestType = mtType.ClrType;
                var queryBuilder = MTSearchHelper.CreateQueryProvider(manifestType);
                var converter = CrateManifestToRowConverter(manifestType);

                var foundObjects = queryBuilder
                    .Query(
                        uow,
                        CurrentUserId,
                        conditions
                    )
                    .ToArray();

                var searchResult = new StandardTableDataCM();

                if (foundObjects.Length > 0)
                {
                    searchResult.FirstRowHeaders = true;

                    var headerRow = new TableRowDTO();

                    var properties = uow.MultiTenantObjectRepository.ListTypePropertyReferences(mtType.Id);
                    foreach (var mtTypeProp in properties)
                    {
                        headerRow.Row.Add(
                            new TableCellDTO()
                            {
                                Cell = new KeyValueDTO(mtTypeProp.Name, mtTypeProp.Name)
                            });
                    }

                    searchResult.Table.Add(headerRow);
                }

                foreach (var foundObject in foundObjects)
                {
                    searchResult.Table.Add(converter(foundObject));
                }

                Payload.Add(
                    Crate.FromContent(
                        RunTimeCrateLabel,
                        searchResult
                    )
                );
            }

            await Task.Yield();
        }

        private Func<object, TableRowDTO> CrateManifestToRowConverter(Type manifestType)
        {
            var accessors = new List<KeyValuePair<string, IMemberAccessor>>();

            foreach (var member in manifestType.GetMembers(BindingFlags.Instance | BindingFlags.Public).OrderBy(x => x.Name))
            {
                IMemberAccessor accessor;

                if (member is FieldInfo)
                {
                    accessor = ((FieldInfo)member).ToMemberAccessor();
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
                var row = new TableRowDTO();

                foreach (var accessor in accessors)
                {
                    row.Row.Add(
                        new TableCellDTO()
                        {
                            Cell = new KeyValueDTO(accessor.Key, string.Format(CultureInfo.InvariantCulture, "{0}", accessor.Value.GetValue(x)))
                        }
                    );
                }

                return row;
            };
        }

        private List<ListItem> GetObjects()
        {
            using (var uow = _container.GetInstance<IUnitOfWork>())
            {
                var listTypeReferences = uow.MultiTenantObjectRepository.ListTypeReferences();
                return listTypeReferences
                    .Select(c =>
                        new ListItem()
                        {
                            Key = c.Alias,
                            Value = c.Id.ToString("N")
                        })
                    .ToList();
            }
        }
    }
}