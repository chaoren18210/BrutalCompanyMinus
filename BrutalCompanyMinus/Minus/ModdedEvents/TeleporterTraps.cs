﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace BrutalCompanyMinus.Minus.Events
{
    internal class TeleporterTraps : MEvent
    {
        public override string Name() => nameof(TeleporterTraps);

        public static TeleporterTraps Instance;

        public override void Initalize()
        {
            Instance = this;

            Weight = 3;
            Descriptions = new List<string>() { "Teleporter Traps!", "These will teleport enemies" };
            ColorHex = "#FF0000";
            Type = EventType.Bad;

            ScaleList.Add(ScaleType.MinAmount, new Scale(8.0f, 0.267f, 8.0f, 24.0f));
            ScaleList.Add(ScaleType.MaxAmount, new Scale(10.0f, 0.34f, 10.0f, 30.0f));
        }

        public override bool AddEventIfOnly() => RoundManager.Instance.currentLevel.spawnableMapObjects.ToList().Exists(x => x.prefabToSpawn.name == "TeleporterTrap");

        public override void Execute()
        {
            RoundManager.Instance.currentLevel.spawnableMapObjects = RoundManager.Instance.currentLevel.spawnableMapObjects.Add(new SpawnableMapObject()
            {
                prefabToSpawn = Assets.GetObject("TeleporterTrap"),
                numberToSpawn = new AnimationCurve(new Keyframe(0f, Get(ScaleType.MinAmount)), new Keyframe(1f, Get(ScaleType.MaxAmount))),
                spawnFacingAwayFromWall = false,
                spawnFacingWall = false,
                spawnWithBackToWall = false,
                spawnWithBackFlushAgainstWall = false,
                requireDistanceBetweenSpawns = false,
                disallowSpawningNearEntrances = false
            });
        }
    }
}