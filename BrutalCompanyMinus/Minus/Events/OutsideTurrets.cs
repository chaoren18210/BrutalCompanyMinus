﻿using BrutalCompanyMinus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace BrutalCompanyMinus.Minus.Events
{
    internal class OutsideTurrets : MEvent
    {
        public override string Name() => nameof(OutsideTurrets);

        public override void Initalize()
        {
            Weight = 3;
            Description = "The turrets blend in with the trees...";
            ColorHex = "#FF0000";
            Type = EventType.Bad;

            EventsToSpawnWith = new List<string>() { nameof(Trees) };
            EventsToRemove = new List<string>() { nameof(LeaflessBrownTrees), nameof(LeaflessTrees) };

            ScaleList.Add(ScaleType.MinDensity, new Scale(0.0008f, 0.000027f, 0.0008f, 0.0024f));
            ScaleList.Add(ScaleType.MaxDensity, new Scale(0.0012f, 0.00004f, 0.0012f, 0.0036f));
        }

        public override void Execute()
        {
            Manager.insideObjectsToSpawnOutside.Add(new Manager.ObjectInfo(Assets.GetObject(Assets.ObjectName.Turret), UnityEngine.Random.Range(Getf(ScaleType.MinDensity), Getf(ScaleType.MaxDensity))));
        }
    }
}
