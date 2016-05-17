﻿using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using StructureMap;
using Data.Entities;
using Data.Interfaces;
using Data.States;
using Fr8Data.Crates;
using Fr8Data.DataTransferObjects;
using Fr8Data.Managers;
using Fr8Data.Manifests;
using Fr8Data.States;
using Hub.Interfaces;
using Hub.Managers;

namespace Hub.Services
{
    public class PlanNode : IPlanNode
    {
        #region Fields

        private readonly ICrateManager _crate;
        private readonly IActivityTemplate _activityTemplate;
        private const string ValidationErrorsLabel = "Validation Errors";

        #endregion

        public PlanNode()
        {
            _activityTemplate = ObjectFactory.GetInstance<IActivityTemplate>();
            _crate = ObjectFactory.GetInstance<ICrateManager>();
        }

        public List<PlanNodeDO> GetUpstreamActivities(IUnitOfWork uow, PlanNodeDO curActivityDO)
        {
            if (curActivityDO == null)
                throw new ArgumentNullException("curActivityDO");
            if (curActivityDO.ParentPlanNodeId == null)
                return new List<PlanNodeDO>();

            List<PlanNodeDO> planNodes = new List<PlanNodeDO>();
            var node = curActivityDO;

            do
            {
                var currentNode = node;

                if (node.ParentPlanNode != null)
                {
                    foreach (var predcessors in node.ParentPlanNode.ChildNodes.Where(x => x.Ordering < currentNode.Ordering && x != currentNode).OrderByDescending(x => x.Ordering))
                    {
                        GetDownstreamRecusive(predcessors, planNodes);
                    }
                }

                node = node.ParentPlanNode;

                if (node != null)
                {
                    planNodes.Add(node);
                }

            } while (node != null);

            return planNodes;
        }

        private void GetDownstreamRecusive(PlanNodeDO root, List<PlanNodeDO> items)
        {
            items.Add(root);

            foreach (var child in root.ChildNodes.OrderBy(x => x.Ordering))
            {
                GetDownstreamRecusive(child, items);
            }
        }

        public IncomingCratesDTO GetIncomingData(Guid activityId, CrateDirection direction, AvailabilityType availability)
        {
            var crates = GetCrateManifestsByDirection(activityId, direction, AvailabilityType.NotSet);
            var availableData = new IncomingCratesDTO();
            availableData.AvailableCrates.AddRange(crates.SelectMany(x => x.CrateDescriptions).Where(x => availability == AvailabilityType.NotSet || (x.Availability & availability) != 0));

            return availableData;
        }
        
        public List<CrateDescriptionCM> GetCrateManifestsByDirection(
            Guid activityId,
            CrateDirection direction,
            AvailabilityType availability,
            bool includeCratesFromActivity = true
                ) 
        {
            Func<Crate<CrateDescriptionCM>, bool> cratePredicate;

            if (availability == AvailabilityType.NotSet)
            {
                //validation errors don't need to be present as available data, so remove Validation Errors
                cratePredicate = f => f.Label != ValidationErrorsLabel && f.Availability != AvailabilityType.Configuration;
            }
            else
            {
                cratePredicate = f => (f.Availability & availability) != 0;
            }

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var activityDO = uow.PlanRepository.GetById<ActivityDO>(activityId);
                var activities = GetActivitiesByDirection(uow, direction, activityDO)
                    .OfType<ActivityDO>();

                if (!includeCratesFromActivity)
                {
                    activities = activities.Where(x => x.Id != activityId);
                }
                // else
                // {
                //     if (!activities.Any(x => x.Id == activityId))
                //     {
                //         var activitiesToAdd = activities.ToList();
                //         activitiesToAdd.Insert(0, activityDO);
                //         activities = activitiesToAdd;
                //     }
                // }

                List<FieldDescriptionsCM> fields = new List<FieldDescriptionsCM>();
                var result = activities
                    .SelectMany(x =>
                    {
                        fields.AddRange(
                            _crate.GetStorage(x)
                                .CratesOfType<FieldDescriptionsCM>()
                                .Where(f => f.Label != ValidationErrorsLabel && f.Availability != AvailabilityType.Configuration)
                                .Select(y=>y.Content)
                                .ToList()
                        );
                        return _crate.GetStorage(x).CratesOfType<CrateDescriptionCM>().Where(cratePredicate);
                    })
                    .Select(x =>
                    {
                        if (x.Content.CrateDescriptions.Count > 0)
                        {
                            x.Content.CrateDescriptions[0].Fields.AddRange(fields.SelectMany(f => f.Fields).Where(f => availability == AvailabilityType.NotSet || (f.Availability & availability) != 0));
                            foreach (var field in x.Content.CrateDescriptions[0].Fields)
                            {
                                if (field.SourceCrateLabel == null)
                                {
                                    if (x.Content.CrateDescriptions[0].Label == null)
                                    {
                                        field.SourceCrateLabel = x.Content.CrateDescriptions[0].ProducedBy;
                                    }
                                    else
                                    {
                                        field.SourceCrateLabel = x.Content.CrateDescriptions[0].Label;
                                    }
                                }
                            }
                        }
                        return x.Content;
                    })
                    .ToList();
                            
                return result;
            }
        }
        
        private List<PlanNodeDO> GetActivitiesByDirection(IUnitOfWork uow, CrateDirection direction, PlanNodeDO curActivityDO)
        {
            switch (direction)
            {
                case CrateDirection.Downstream:
                    return GetDownstreamActivities(uow, curActivityDO);

                case CrateDirection.Upstream:
                    return GetUpstreamActivities(uow, curActivityDO);

                default:
                    return GetDownstreamActivities(uow, curActivityDO).Concat(GetUpstreamActivities(uow, curActivityDO)).ToList();
            }
        }

        public List<PlanNodeDO> GetDownstreamActivities(IUnitOfWork uow, PlanNodeDO curActivityDO)
        {
            if (curActivityDO == null)
                throw new ArgumentNullException("curActivity");
            if (curActivityDO.ParentPlanNodeId == null)
                return new List<PlanNodeDO>();

            List<PlanNodeDO> nodes = new List<PlanNodeDO>();

            foreach (var planNodeDo in curActivityDO.ChildNodes)
            {
                GetDownstreamRecusive(planNodeDo, nodes);
            }


            while (curActivityDO != null)
            {
                if (curActivityDO.ParentPlanNode != null)
                {
                    foreach (var sibling in curActivityDO.ParentPlanNode.ChildNodes.Where(x => x.Ordering > curActivityDO.Ordering))
                    {
                        GetDownstreamRecusive(sibling, nodes);
                    }
                }

                curActivityDO = curActivityDO.ParentPlanNode;
            }

            return nodes;
        }

        public PlanNodeDO GetParent(PlanNodeDO currentActivity)
        {
            return currentActivity.ParentPlanNode;
        }

        public PlanNodeDO GetNextSibling(PlanNodeDO currentActivity)
        {
            // Move to the next activity of the current activity's parent
            if (currentActivity.ParentPlanNode == null)
            {
                // We are at the root of activity tree. Next activity can be only among children.
                return null;
            }

            return currentActivity.ParentPlanNode.GetOrderedChildren().FirstOrDefault(x => x.Runnable && x.Ordering > currentActivity.Ordering);
        }

        public PlanNodeDO GetFirstChild(PlanNodeDO currentActivity)
        {
            if (currentActivity.ChildNodes.Count != 0)
            {
                return currentActivity.ChildNodes.OrderBy(x => x.Ordering).FirstOrDefault(x => x.Runnable);
            }

            return null;
        }

        public PlanNodeDO GetNextActivity(PlanNodeDO currentActivity, PlanNodeDO root)
        {
            return GetNextActivity(currentActivity, true, root);
        }

        public bool HasChildren(PlanNodeDO currentActivity)
        {
            return currentActivity.ChildNodes.Count > 0;
        }

        private PlanNodeDO GetNextActivity(PlanNodeDO currentActivity, bool depthFirst, PlanNodeDO root)
        {
            // Move to the first child if current activity has nested ones
            if (depthFirst && currentActivity.ChildNodes.Count != 0)
            {
                return currentActivity.ChildNodes.OrderBy(x => x.Ordering).FirstOrDefault();
            }

            // Move to the next activity of the current activity's parent
            if (currentActivity.ParentPlanNode == null)
            {
                // We are at the root of activity tree. Next activity can be only among children.
                return null;
            }

            var prev = currentActivity;
            var nextCandidate = currentActivity.ParentPlanNode.ChildNodes
                .OrderBy(x => x.Ordering)
                .FirstOrDefault(x => x.Ordering > currentActivity.Ordering);

            /* No more activities in the current branch
                *          a
                *       b     c 
                *     d   E  f  g  
                * 
                * We are at E. Get next activity as if current activity is b. (move to c)
                */

            if (nextCandidate == null)
            {
                // Someone doesn't want us to go higher this node
                if (prev == root)
                {
                    return null;
                }
                nextCandidate = GetNextActivity(prev.ParentPlanNode, false, root);
            }

            return nextCandidate;
        }

        public void Delete(IUnitOfWork uow, PlanNodeDO activity)
        {
            var activities = new List<PlanNodeDO>();

            TraverseActivity(activity, activities.Add);

            activities.Reverse();

            activities.ForEach(x =>
            {
                // TODO: it is not very smart solution. Activity service should not knon about anything except Activities
                // But we have to support correct deletion of any activity types and any level of hierarchy
                // May be other services should register some kind of callback to get notifed when activity is being deleted.
                if (x is SubPlanDO)
                {
                    foreach (var criteria in uow.CriteriaRepository.GetQuery().Where(y => y.SubPlanId == x.Id).ToArray())
                    {
                        uow.CriteriaRepository.Remove(criteria);
                    }
                }
            });

            activity.RemoveFromParent();
        }

        private static void TraverseActivity(PlanNodeDO parent, Action<PlanNodeDO> visitAction)
        {
            visitAction(parent);
            foreach (PlanNodeDO child in parent.ChildNodes)
                TraverseActivity(child, visitAction);
        }
       
        public IEnumerable<ActivityTemplateDTO> GetAvailableActivities(IUnitOfWork uow, IFr8AccountDO curAccount)
        {
            IEnumerable<ActivityTemplateDTO> curActivityTemplates;

            curActivityTemplates = _activityTemplate
                .GetAll()
                .OrderBy(t => t.Category)
                .Select(Mapper.Map<ActivityTemplateDTO>)
                .ToList();


            //we're currently bypassing the subscription logic until we need it
            //we're bypassing the pluginregistration logic here because it's going away in V2

            //var plugins = _subscription.GetAuthorizedPlugins(curAccount);
            //var plugins = _plugin.GetAll();
            // var curActionTemplates = plugins
            //    .SelectMany(p => p.AvailableActions)
            //    .OrderBy(s => s.ActionType);

            return curActivityTemplates;
        }

        /// <summary>
        /// Returns ActivityTemplates while filtering them by the supplied predicate
        /// </summary>
        public IEnumerable<ActivityTemplateDTO> GetAvailableActivities(IUnitOfWork uow, Func<ActivityTemplateDO, bool> predicate)
        {
            return _activityTemplate
                .GetAll()
                .Where(predicate)
                .Where(at => at.ActivityTemplateState == Data.States.ActivityTemplateState.Active)
                .OrderBy(t => t.Category)
                .Select(Mapper.Map<ActivityTemplateDTO>)
                .ToList();
        }

        public IEnumerable<ActivityTemplateDTO> GetSolutions(IUnitOfWork uow)
        {
            IEnumerable<ActivityTemplateDTO> curActivityTemplates;
            curActivityTemplates = _activityTemplate
                .GetAll()
                .Where(at => at.Category == ActivityCategory.Solution
                    && at.ActivityTemplateState == Data.States.ActivityTemplateState.Active)
                .OrderBy(t => t.Category)
                .Select(Mapper.Map<ActivityTemplateDTO>)
                .ToList();

            //we're currently bypassing the subscription logic until we need it
            //we're bypassing the pluginregistration logic here because it's going away in V2

            //var plugins = _subscription.GetAuthorizedPlugins(curAccount);
            //var plugins = _plugin.GetAll();
            // var curActionTemplates = plugins
            //    .SelectMany(p => p.AvailableActions)
            //    .OrderBy(s => s.ActionType);

            return curActivityTemplates;
        }


        public IEnumerable<ActivityTemplateCategoryDTO> GetAvailableActivityGroups()
        {
            var curActivityTemplates = _activityTemplate
                .GetQuery()
                .Where(at => at.ActivityTemplateState == ActivityTemplateState.Active).AsEnumerable().ToArray()
                .GroupBy(t => t.Category)
                .OrderBy(c => c.Key)
                .Select(c => new ActivityTemplateCategoryDTO
                {
                    Activities = c.Select(Mapper.Map<ActivityTemplateDTO>).ToList(),
                    Name = c.Key.ToString()
                })
                .ToList();


            return curActivityTemplates;
        }

    }
}