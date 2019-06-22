﻿using Pandaros.Settlers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pandaros.Settlers.Items
{
    public class ConnectedBlock
    {
        public string BlockType { get; set; }
        public List<BlockSides> Connections { get; set; }
        public CalculateRotations CalculateRotations { get; set; }
    }

    public class CalculateRotations
    {
        public string CalculationType { get; set; }
    }
}
