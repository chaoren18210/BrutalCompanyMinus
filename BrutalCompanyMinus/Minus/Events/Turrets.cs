﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace BrutalCompanyMinus.Minus.Events
{
    internal class Turrets : MEvent
    {
        public override string Name() => nameof(Turrets);

        public static Turrets Instance;

        public override void Initalize()
        {
            Instance = this;

            Weight = 3;
            Descriptions = new List<string>() { "Turrets!!", "Home defense systems", "Panic and scream", "+Turrets" };
            ColorHex = "#FF0000";
            Type = EventType.Bad;

            ScaleList.Add(ScaleType.MinAmount, new Scale(5.0f, 0.167f, 5.0f, 12.0f));
            ScaleList.Add(ScaleType.MaxAmount, new Scale(6.0f, 0.2f, 6.0f, 18.0f));
        }

        public override bool AddEventIfOnly() => RoundManager.Instance.currentLevel.spawnableMapObjects.ToList().Exists(x => x.prefabToSpawn.name == Assets.ObjectNameList[Assets.ObjectName.Turret]);

        public override void Execute()
        {
            RoundManager.Instance.currentLevel.spawnableMapObjects = RoundManager.Instance.currentLevel.spawnableMapObjects.Add(new SpawnableMapObject()
            {
                prefabToSpawn = Assets.GetObject(Assets.ObjectName.Turret),
                numberToSpawn = new AnimationCurve(new Keyframe(0f, Get(ScaleType.MinAmount)), new Keyframe(1f, Get(ScaleType.MaxAmount))),
                spawnFacingAwayFromWall = true,
                spawnFacingWall = false,
                spawnWithBackToWall = false,
                spawnWithBackFlushAgainstWall = false,
                requireDistanceBetweenSpawns = false,
                disallowSpawningNearEntrances = false
            });
        }
    }
}
