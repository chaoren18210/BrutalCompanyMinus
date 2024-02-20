﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace BrutalCompanyMinus.Minus.Events
{
    internal class SnareFleas : MEvent
    {
        public override string Name() => nameof(SnareFleas);

        public override void Initalize()
        {
            Weight = 3;
            Description = "Ceiling campers!";
            ColorHex = "#FF0000";
            Type = EventType.Bad;

            ScaleList.Add(ScaleType.InsideEnemyRarity, new Scale(25.0f, 0.84f, 25.0f, 75.0f));
            ScaleList.Add(ScaleType.MinInsideEnemy, new Scale(2.0f, 0.067f, 2.0f, 6.0f));
            ScaleList.Add(ScaleType.MaxInsideEnemy, new Scale(3.0f, 0.1f, 3.0f, 9.0f));
        }

        public override void Execute()
        {
            EnemyType SnareFlea = Assets.GetEnemy(Assets.EnemyName.SnareFlea);

            Manager.AddEnemyToPoolWithRarity(ref RoundManager.Instance.currentLevel.Enemies, SnareFlea, Get(ScaleType.InsideEnemyRarity));
            Manager.Spawn.InsideEnemies(SnareFlea, UnityEngine.Random.Range(Get(ScaleType.MinInsideEnemy), Get(ScaleType.MaxInsideEnemy) + 1));
        }
    }
}
