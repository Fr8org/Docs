﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Interfaces.DataTransferObjects
{
    public class ActionNameListDTO
    {
        public ActionNameListDTO()
        {
            ActionNames = new List<ActionNameDTO>();
        }
        public List<ActionNameDTO> ActionNames { get; set; }
    }
}
