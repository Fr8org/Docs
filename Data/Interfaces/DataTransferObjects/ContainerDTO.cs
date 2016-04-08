﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Data.Constants;
using Data.Entities;

namespace Data.Interfaces.DataTransferObjects
{
    public class ContainerDTO
    {
        public ContainerDTO()
        {
        }

        [Required]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Guid PlanId { get; set; }
        public int State;

        public Guid? CurrentActivityId { get; set; }
        public Guid? NextActivityId { get; set; }

        public DateTimeOffset LastUpdated { get; set; }

        public DateTimeOffset CreateDate { get; set; }

        public ActivityResponse? CurrentActivityResponse { get; set; }
        public string CurrentClientActivityName { get; set; }
        public PlanType? CurrentPlanType { get; set; }
    }
}
