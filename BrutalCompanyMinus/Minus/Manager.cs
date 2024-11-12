﻿using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using HarmonyLib;
using System.Collections;
using GameNetcodeStuff;
using TMPro;
using System.Reflection.Emit;
using System.Reflection;
using DigitalRuby.ThunderAndLightning;
using System.IO;
using UnityEngine.Events;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using UnityEngine.AI;
using System.Xml.Linq;
using UnityEngine.Animations.Rigging;
using BrutalCompanyMinus.Minus.MonoBehaviours;
using BrutalCompanyMinus.Minus.Events;

namespace BrutalCompanyMinus.Minus
{
    [HarmonyPatch]
    public class Manager
    {
        internal static int daysPassed
        {
            get
            {
                if(StartOfRound.Instance != null) return StartOfRound.Instance.gameStats.daysSpent;
                return 0;
            }
        }
        internal static float difficulty = 0.0f;
        internal static float daysDifficulty = 0.0f;
        internal static float scrapInShipDifficulty = 0.0f;
        internal static float moonGradeDifficulty = 0.0f;
        internal static float weatherDifficulty = 0.0f;
        internal static float quotaDifficulty = 0.0f;

        public static SelectableLevel currentLevel;
        public static Terminal currentTerminal;

        internal static float terrainArea = 0.0f;
        internal static string terrainTag = "";
        internal static string terrainName = "";
        private static GameObject _terrainObject;
        internal static GameObject terrainObject
        {
            get 
            {
                if (_terrainObject == null) SampleMap();
                return _terrainObject;
            }
            private set 
            { 
                _terrainObject = value; 
            }
        }
        internal static List<Vector3> outsideObjectSpawnNodes = new List<Vector3>();
        internal static float outsideObjectSpawnRadius = 0.0f;

        internal static List<GameObject> objectsToClear = new List<GameObject>();

        internal static List<ObjectInfo> enemiesToSpawnInside = new List<ObjectInfo>();
        internal static List<ObjectInfo> enemiesToSpawnOutside = new List<ObjectInfo>();
        internal static List<ObjectInfo> insideObjectsToSpawnOutside = new List<ObjectInfo>();

        internal static float factorySizeMultiplier = 1f;
        internal static float scrapValueMultiplier = 1f;
        internal static float scrapAmountMultiplier = 1f;

        internal static int randomItemsToSpawnOutsideCount = 0;
        internal static int bonusEnemyHp = 0;
        internal static int bonusMaxInsidePowerCount = 0, bonusMaxOutsidePowerCount = 0;
        internal static int minEnemiesToSpawnInside = 0, minEnemiestoSpawnOutside = 0;
        internal static float spawnChanceMultiplier = 1.0f, spawncapMultipler = 1.0f;

        internal static bool transmuteScrap = false;
        internal static List<float> scrapTransmuteAmount = new List<float>();
        internal static List<SpawnableItemWithRarity> ScrapToTransmuteTo = new List<SpawnableItemWithRarity>();

        internal static bool moveTime = false;
        internal static float moveTimeAmount = 0.0f;
        internal static float timeSpeedMultiplier = 1.0f;
        internal static float inverseTimeSpeedMultiplier = 1.0f;

        internal static int randomSeedValue = 0;

        private static List<Vector3> spawnDenialPoints = new List<Vector3>();

        /// <summary>
        /// This is to be used inside of Execute(), will add to list to spawn object(s) safely.
        /// </summary>
        public static void SpawnOutsideObjects(GameObject obj, Vector3 offset, float density, float radius = -1.0f, int objectCap = 1000) => BatchSpawnOutsideObjects(obj, offset, density, radius, objectCap);

        /// <summary>
        /// This is to be used inside of Execute(), will add to list to spawn object(s) safely.
        /// </summary>
        public static void SpawnOutsideObjects(Assets.ObjectName objName, Vector3 offset, float density, float radius = -1.0f, int objectCap = 1000) => BatchSpawnOutsideObjects(Assets.GetObject(objName), offset, density, radius, objectCap);

        private static void BatchSpawnOutsideObjects(GameObject obj, Vector3 offset, float density, float radius, int objectCap)
        {
            if (obj == null) return;

            spawnDenialPoints = Helper.GetSpawnDenialNodes();

            int count = (int)Mathf.Clamp(density * terrainArea, 0, objectCap); // Compute amount
            Log.LogInfo(string.Format("Spawning: {0}, Count:{1}", obj.name, count));

            int batchSize = 8;
            int batches = count / batchSize;
            int remainder = count % batchSize;

            for (int i = 0; i < batches; i++)
            {
                Net.Instance.objectsToSpawn.Add(obj);
                Net.Instance.objectsToSpawnRadius.Add(radius);
                Net.Instance.objectsToSpawnOffsets.Add(offset);
                Net.Instance.objectsToSpawnAmount.Add(batchSize);
            }

            Net.Instance.objectsToSpawn.Add(obj);
            Net.Instance.objectsToSpawnRadius.Add(radius);
            Net.Instance.objectsToSpawnOffsets.Add(offset);
            Net.Instance.objectsToSpawnAmount.Add(remainder);
        }

        /// <summary>
        /// This is not safe to be used inside of Execute(), this will spawn client side objects.
        /// </summary>
        public static void DoSpawnOutsideObjects(int count, float radius, Vector3 offset, GameObject obj)
        {
            for (int i = 0; i < count; i++)
            {
                randomSeedValue++;

                UnityEngine.Random.InitState(randomSeedValue++); // Important or wont be same on all clients
                Vector3 position = new Vector3(0.0f, 0.0f, 0.0f);
                if (radius != -1.0f || outsideObjectSpawnNodes.Count == 0)
                {
                    position = RoundManager.Instance.GetRandomNavMeshPositionInRadius(RoundManager.Instance.outsideAINodes[UnityEngine.Random.Range(0, RoundManager.Instance.outsideAINodes.Length)].transform.position, radius);
                }
                else
                {
                    position = RoundManager.Instance.GetRandomNavMeshPositionInRadius(outsideObjectSpawnNodes[UnityEngine.Random.Range(0, outsideObjectSpawnNodes.Count)], outsideObjectSpawnRadius);
                }
                Quaternion rotation = obj.transform.rotation;

                RaycastHit info;
                bool isInvalidPosition = false;
                if (Physics.Raycast(new Ray(position, Vector3.down), out info))
                {
                    if (info.collider.gameObject.tag != terrainTag && info.collider.gameObject.name != terrainName) // Did it hit terrain mesh? if not then position is not valid...
                    {
                        isInvalidPosition = true;
                    }
                }
                else // If didn't hit anything, position is is invalid
                {
                    isInvalidPosition = true;
                }
                foreach (Vector3 spawnDenialPoint in spawnDenialPoints)
                {
                    if (Vector3.Distance(position, spawnDenialPoint) <= 10.0f)
                    {
                        isInvalidPosition = true;
                    }
                }

                if (!isInvalidPosition)
                {
                    position.y = info.point.y; // Match raycast hit y position

                    position += offset;
                    rotation.eulerAngles += new Vector3(0.0f, UnityEngine.Random.Range(0, 360), 0.0f);

                    GameObject gameObject = UnityEngine.Object.Instantiate(obj, position, rotation);

                    NetworkObject netObject = gameObject.GetComponent<NetworkObject>();
                    if (netObject != null) gameObject.GetComponent<NetworkObject>().Spawn(true);

                    objectsToClear.Add(gameObject);
                }
            }
        }

        /// <summary>
        /// This is to be used inside of Execute(), will add to a list to spawn enemie(s) safely.
        /// </summary>
        public static void SpawnOutsideEnemies(GameObject enemy, int count) => enemiesToSpawnOutside.Add(new ObjectInfo(enemy, count));

        /// <summary>
        /// This is to be used inside of Execute(), will add to a list to spawn enemie(s) safely.
        /// </summary>
        public static void SpawnOutsideEnemies(EnemyType enemy, int count) => enemiesToSpawnOutside.Add(new ObjectInfo(enemy.enemyPrefab, count));

        /// <summary>
        /// This is to be used inside of Execute(), will add to a list to spawn enemie(s) safely.
        /// </summary>
        public static void SpawnOutsideEnemies(Assets.EnemyName enemyName, int count) => enemiesToSpawnOutside.Add(new ObjectInfo(Assets.GetEnemy(enemyName).enemyPrefab, count));

        /// <summary>
        /// This is to be used inside of Execute(), will add to a list to spawn enemie(s) safely.
        /// </summary>
        public static void SpawnInsideEnemies(GameObject enemy, int count, float radius = 0.0f) => enemiesToSpawnInside.Add(new ObjectInfo(enemy, count, 0.0f, radius));

        /// <summary>
        /// This is to be used inside of Execute(), will add to a list to spawn enemie(s) safely.
        /// </summary>
        public static void SpawnInsideEnemies(EnemyType enemy, int count, float radius = 0.0f) => enemiesToSpawnInside.Add(new ObjectInfo(enemy.enemyPrefab, count, 0.0f, radius));

        /// <summary>
        /// This is to be used inside of Execute(), will add to a list to spawn enemie(s) safely.
        /// </summary>
        public static void SpawnInsideEnemies(Assets.EnemyName enemyName, int count, float radius = 0.0f) => enemiesToSpawnInside.Add(new ObjectInfo(Assets.GetEnemy(enemyName).enemyPrefab, count, 0.0f, radius));

        /// <summary>
        /// This is to be used inside of Execute(), will add to a list to spawn scrap outside safely.
        /// </summary>
        public static void SpawnOutsideScrap(int Amount) => randomItemsToSpawnOutsideCount += Amount;

        /// <summary>
        /// This is not safe to be used inside of Execute(), will spawn the enemie(s) using outsideAINodes.
        /// </summary>
        /// <returns>Returns EnemyAI scripts after spawned.</returns>
        public static List<EnemyAI> DoSpawnOutsideEnemies()
        {
            if (Events.SafeOutside.Active)
            {
                Log.LogInfo("Outside spawning prevented by OutsideSafe");
                return new List<EnemyAI>();
            }
            List<EnemyAI> spawnedEnemies = new List<EnemyAI>();

            List<Vector3> OutsideAiNodes = Helper.GetOutsideNodes();
            List<Vector3> SpawnDenialNodes = Helper.GetSpawnDenialNodes();

            // Spawn Outside enemies
            for (int i = 0; i < enemiesToSpawnOutside.Count; i++)
            {
                for (int j = 0; j < enemiesToSpawnOutside[i].count; j++)
                {
                    if (enemiesToSpawnOutside[i].obj == null)
                    {
                        Log.LogError("Enemy prefab on DoSpawnOutsideEnemies() is null, continuing.");
                        continue;
                    }
                    GameObject obj = UnityEngine.Object.Instantiate(
                        enemiesToSpawnOutside[i].obj,
                        Helper.GetSafePosition(OutsideAiNodes, SpawnDenialNodes, 20.0f, seed++),
                        Quaternion.Euler(Vector3.zero));

                    EnemyAI ai = obj.GetComponent<EnemyAI>();
                    spawnedEnemies.Add(ai);
                    RoundManager.Instance.SpawnedEnemies.Add(ai);

                    obj.gameObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
                }
            }
            enemiesToSpawnOutside.Clear();
            return spawnedEnemies;
        }

        /// <summary>
        /// This is not safe to be used inside of Execute(), will spawn the enemie(s) using using vents.
        /// </summary>
        /// <returns>Returns EnemyAI scripts after spawned.</returns>
        public static List<EnemyAI> DoSpawnInsideEnemies()
        {
            List<EnemyAI> spawnedEnemies = new List<EnemyAI>();

            // Spawn Inside enemies
            for (int i = 0; i < enemiesToSpawnInside.Count; i++)
            {
                for (int j = 0; j < enemiesToSpawnInside[i].count; j++)
                {
                    if (enemiesToSpawnInside[i].obj == null)
                    {
                        Log.LogError("Enemy prefab on DoSpawnInsideEnemies() is null, continuing.");
                        continue;
                    }
                    int index = UnityEngine.Random.Range(0, RoundManager.Instance.allEnemyVents.Length);
                    Vector3 position = RoundManager.Instance.allEnemyVents[index].floorNode.position;
                    position = RoundManager.Instance.GetRandomNavMeshPositionInRadius(position, enemiesToSpawnInside[i].radius, RoundManager.Instance.navHit);
                    Quaternion rotation = Quaternion.Euler(0.0f, RoundManager.Instance.allEnemyVents[index].floorNode.eulerAngles.y, 0.0f);
                    GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(enemiesToSpawnInside[i].obj, position, rotation);

                    gameObject.GetComponentInChildren<NetworkObject>().Spawn(true);
                    EnemyAI ai = gameObject.GetComponent<EnemyAI>();

                    spawnedEnemies.Add(ai);

                    RoundManager.Instance.SpawnedEnemies.Add(ai);
                }
            }
            enemiesToSpawnInside.Clear();

            return spawnedEnemies;
        }

        /// <summary>
        /// This is not safe to be used inside of Execute(), this will spawn scrap outside using outsideAINodes.
        /// </summary>
        /// <returns>Returns all NetworkObjectReferences and ScrapValues</returns>
        public static ScrapSpawnInfo DoSpawnScrapOutside(int Amount)
        {
            if (Amount <= 0) return new ScrapSpawnInfo(new NetworkObjectReference[] { }, new int[] { });

            RoundManager r = RoundManager.Instance;
            System.Random rng = new System.Random();

            // Generate Scrap To Spawn
            List<Item> ScrapToSpawn = GetScrapToSpawn((int)(Amount * r.scrapAmountMultiplier * scrapAmountMultiplier));
            List<int> ScrapValues = new List<int>();

            // Spawn Scrap
            List<NetworkObjectReference> ScrapSpawnsNet = new List<NetworkObjectReference>();
            List<Vector3> OutsideNodes = Helper.GetOutsideNodes();

            Log.LogInfo($"Spawning {ScrapToSpawn.Count} outside");
            for (int i = 0; i < ScrapToSpawn.Count; i++)
            {
                if (ScrapToSpawn[i] == null)
                {
                    Log.LogError("Found null element in list ScrapToSpawn. Skipping it.");
                    continue;
                }
                Vector3 position = r.GetRandomNavMeshPositionInBoxPredictable(OutsideNodes[UnityEngine.Random.Range(0, OutsideNodes.Count)], 10.0f, r.navHit, rng);
                GameObject obj = UnityEngine.Object.Instantiate(ScrapToSpawn[i].spawnPrefab, position, Quaternion.identity, r.spawnedScrapContainer);
                GrabbableObject grabbableObject = obj.GetComponent<GrabbableObject>();
                grabbableObject.transform.rotation = Quaternion.Euler(grabbableObject.itemProperties.restingRotation);
                grabbableObject.fallTime = 0.0f;
                ScrapValues.Add((int)(UnityEngine.Random.Range(ScrapToSpawn[i].minValue, ScrapToSpawn[i].maxValue + 1) * r.scrapValueMultiplier * scrapValueMultiplier));
                grabbableObject.scrapValue = ScrapValues[ScrapValues.Count - 1];
                NetworkObject netObj = obj.GetComponent<NetworkObject>();
                netObj.Spawn();
                ScrapSpawnsNet.Add(netObj);
            }

            return new ScrapSpawnInfo(ScrapSpawnsNet.ToArray(), ScrapValues.ToArray());
        }

        /// <summary>
        /// This is not safe to be used inside of Execute(), this will spawn scrap inside using randomScrapSpawn.
        /// </summary>
        /// <returns>Returns all NetworkObjectReferences and ScrapValues</returns>
        public static ScrapSpawnInfo DoSpawnScrapInside(int Amount)
        {
            if (Amount <= 0) return new ScrapSpawnInfo(new NetworkObjectReference[] { }, new int[] { });

            RoundManager r = RoundManager.Instance;
            System.Random rng = new System.Random();

            RandomScrapSpawn randomScrapSpawn = null;
            RandomScrapSpawn[] source = UnityEngine.Object.FindObjectsOfType<RandomScrapSpawn>();
            List<RandomScrapSpawn> usedSpawns = new List<RandomScrapSpawn>();
            List<Item> ScrapToSpawn = GetScrapToSpawn(Amount);

            List<int> ScrapValues = new List<int>();
            List<NetworkObjectReference> NetScrapList = new List<NetworkObjectReference>();

            Log.LogInfo($"Spawning {ScrapToSpawn.Count} inside");
            for (int i = 0; i < ScrapToSpawn.Count; i++)
            {
                if (ScrapToSpawn[i] == null)
                {
                    Log.LogError("Null entry in scrapToSpawn, skipping entry");
                    continue;
                }
                List<RandomScrapSpawn> scrapSpawnPositions = ((ScrapToSpawn[i].spawnPositionTypes != null && ScrapToSpawn[i].spawnPositionTypes.Count != 0) ? source.Where((RandomScrapSpawn x) => ScrapToSpawn[i].spawnPositionTypes.Contains(x.spawnableItems) && !x.spawnUsed).ToList() : source.ToList());
                if (scrapSpawnPositions.Count <= 0)
                {
                    Log.LogError("No positions to spawn scrap: " + ScrapToSpawn[i].itemName);
                    continue;
                }
                if (usedSpawns.Count > 0 && scrapSpawnPositions.Contains(randomScrapSpawn))
                {
                    scrapSpawnPositions.RemoveAll((RandomScrapSpawn x) => usedSpawns.Contains(x));
                    if (scrapSpawnPositions.Count <= 0)
                    {
                        usedSpawns.Clear();
                        i--;
                        continue;
                    }
                }

                randomScrapSpawn = scrapSpawnPositions[rng.Next(0, scrapSpawnPositions.Count)];
                usedSpawns.Add(randomScrapSpawn);

                Vector3 pos;
                if (randomScrapSpawn.spawnedItemsCopyPosition)
                {
                    randomScrapSpawn.spawnUsed = true;
                    pos = randomScrapSpawn.transform.position;
                }
                else
                {
                    pos = r.GetRandomNavMeshPositionInBoxPredictable(randomScrapSpawn.transform.position, randomScrapSpawn.itemSpawnRange, r.navHit, rng) + Vector3.up * ScrapToSpawn[i].verticalOffset;
                }

                if (ScrapToSpawn[i].spawnPrefab.GetComponent<GrabbableObject>() == null)
                {
                    Log.LogError("GrabbableObject is null in scrapToSpawn, skipping entry.");
                    continue;
                }

                GameObject scrap = GameObject.Instantiate(ScrapToSpawn[i].spawnPrefab, pos, Quaternion.identity, r.spawnedScrapContainer);
                GrabbableObject grabbableObject = scrap.GetComponent<GrabbableObject>();
                grabbableObject.transform.rotation = Quaternion.Euler(grabbableObject.itemProperties.restingRotation);
                grabbableObject.fallTime = 0.0f;

                int ScrapValue = (int)(UnityEngine.Random.Range(ScrapToSpawn[i].minValue, ScrapToSpawn[i].maxValue + 1) * r.scrapValueMultiplier * scrapValueMultiplier);
                ScrapValues.Add(ScrapValue);
                grabbableObject.scrapValue = ScrapValue;

                NetworkObject netObj = scrap.GetComponent<NetworkObject>();
                netObj.Spawn();
                NetScrapList.Add(netObj);
            }

            return new ScrapSpawnInfo(NetScrapList.ToArray(), ScrapValues.ToArray());
        }

        private static int seed = 0;
        private static List<Item> GetScrapToSpawn(int Amount)
        {
            RoundManager r = RoundManager.Instance;
            System.Random rng = new System.Random(StartOfRound.Instance.randomMapSeed + seed);
            seed++;

            List<Item> ScrapToSpawn = new List<Item>();
            List<int> ScrapWeights = new List<int>();
            for (int i = 0; i < r.currentLevel.spawnableScrap.Count; i++)
            {
                if (i == r.increasedScrapSpawnRateIndex)
                {
                    ScrapWeights.Add(i);
                }
                else
                {
                    ScrapWeights.Add(r.currentLevel.spawnableScrap[i].rarity);
                }
            }
            int[] weights = ScrapWeights.ToArray();
            for (int i = 0; i < Amount; i++)
            {
                Item pickedScrap = r.currentLevel.spawnableScrap[r.GetRandomWeightedIndex(weights, rng)].spawnableItem;
                ScrapToSpawn.Add(Assets.GetItem(pickedScrap.name));
            }

            return ScrapToSpawn;
        }

        /// <summary>
        /// This is to be used in Execute(), this will add to list to be transmute scrap safely.
        /// </summary>
        /// <param name="amount">This is a percentage value between 0.0 and 1.0</param>
        /// <param name="Items">Items to spawn with rarities.</param>
        public static void TransmuteScrap(float amount, params SpawnableItemWithRarity[] Items)
        {
            transmuteScrap = true;
            scrapTransmuteAmount.Add(Mathf.Clamp(amount, 0.0f, 1.0f));
            ScrapToTransmuteTo.AddRange(Items);
        }

        /// <summary>
        /// This will deliver items in said price range.
        /// </summary>
        /// <param name="Amount">Amount to deliver</param>
        /// <param name="MinPrice">Wont deliver items below this price.</param>
        /// <param name="MaxPrice">Wont deliver items above this price.</param>
        public static void DeliverRandomItems(int Amount, int MinPrice, int MaxPrice)
        {
            if (RoundManager.Instance.IsServer)
            {
                Terminal terminal = GameObject.FindObjectOfType<Terminal>();

                List<int> validItems = new List<int>();
                for (int i = 0; i < terminal.buyableItemsList.Length; i++)
                {
                    if (terminal.buyableItemsList[i].creditsWorth >= MinPrice && terminal.buyableItemsList[i].creditsWorth <= MaxPrice) validItems.Add(i);
                }

                for (int i = 0; i < Amount; i++)
                {
                    int item = validItems[UnityEngine.Random.Range(0, validItems.Count)];
                    terminal.orderedItemsFromTerminal.Add(item);
                }
            }
        }

        internal static int GetLevelIndex()
        {
            for(int i = 0; i < StartOfRound.Instance.levels.Length; i++)
            {
                if (StartOfRound.Instance.levels[i].name == RoundManager.Instance.currentLevel.name) return i;
            }
            return 0;
        }
        
        /// <summary>
        /// Adds to enemy hp.
        /// </summary>
        public static void AddEnemyHp(int amount) => bonusEnemyHp += amount;

        public static void AddInsidePower(int amount) => bonusMaxInsidePowerCount += amount;

        public static void AddOutsidePower(int amount) => bonusMaxOutsidePowerCount += amount;

        public static void MultiplySpawnCap(float multiplier) => spawncapMultipler *= multiplier;

        internal static void ComputeDifficultyValues()
        {
            difficulty = 0.0f;

            if (Configuration.scaleByDaysPassed.Value)
            {
                daysDifficulty = Mathf.Clamp(daysPassed * Configuration.daysPassedDifficultyMultiplier.Value, 0.0f, Configuration.daysPassedDifficultyCap.Value);
                difficulty += daysDifficulty;
            }
            if (Configuration.scaleByScrapInShip.Value)
            {
                scrapInShipDifficulty = Mathf.Clamp(GetScrapInShip() * Configuration.scrapInShipDifficultyMultiplier.Value, 0.0f, Configuration.scrapInShipDifficultyCap.Value);
                difficulty += scrapInShipDifficulty;
            }
            if (Configuration.scaleByMoonGrade.Value)
            {
                if (Configuration.gradeAdditives.TryGetValue(StartOfRound.Instance.currentLevel.riskLevel, out float value))
                {
                    moonGradeDifficulty = value;
                    difficulty += value;
                }
                else
                {
                    moonGradeDifficulty = Configuration.gradeAdditives["Other"];
                    difficulty += moonGradeDifficulty;
                }
            }
            if(Configuration.scaleByQuota.Value)
            {
                quotaDifficulty = Mathf.Clamp(TimeOfDay.Instance.profitQuota * Configuration.quotaDifficultyMultiplier.Value, 0.0f, Configuration.quotaDifficultyCap.Value);
                difficulty += quotaDifficulty;
            }
            if(Configuration.scaleByWeather.Value)
            {
                weatherDifficulty = Configuration.weatherAdditives.GetValueOrDefault(StartOfRound.Instance.currentLevel.currentWeather, 0.0f);
                difficulty += weatherDifficulty;
            }
            difficulty = Mathf.Clamp(difficulty, 0.0f, Configuration.difficultyMaxCap.Value);
        }

        internal static float GetScrapInShip()
        {
            GameObject hangarShip = Assets.hangarShip;
            if (hangarShip == null) return 0;

            GrabbableObject[] itemsInShip = hangarShip.GetComponentsInChildren<GrabbableObject>();

            int count = 0;
            foreach (GrabbableObject item in itemsInShip)
            {
                if (item != null) count += item.scrapValue;
            }
            return count;
        }

        internal static void SampleMap()
        {
            // Compute Map Area
            List<Vector2> OuterPoints = new List<Vector2>();
            foreach (GameObject outsideAiNode in RoundManager.Instance.outsideAINodes)
            {
                OuterPoints.Add(new Vector2(outsideAiNode.transform.position.x, outsideAiNode.transform.position.z));
            }
            OuterPoints = Helper.ComputeConvexHull(OuterPoints).ToList();

            // Get outside spawn nodes
            if(OuterPoints.Count > 0)
            {
                float xSum = 0.0f, ySum = 0.0f;
                foreach (Vector2 outerPoint in OuterPoints)
                {
                    xSum += outerPoint.x;
                    ySum += outerPoint.y;
                }
                Vector2 CentrePoint = new Vector2(xSum / OuterPoints.Count, ySum / OuterPoints.Count);

                foreach (Vector2 outerPoint in OuterPoints)
                {
                    Vector2 innerPoint = (outerPoint + CentrePoint) * 0.5f;

                    outsideObjectSpawnNodes.Add(new Vector3(innerPoint.x, 100.0f, innerPoint.y));
                }
                outsideObjectSpawnRadius = Vector2.Distance(CentrePoint, OuterPoints[0]) + 75.0f;
            }


            float Area = 0.0f;
            for (int i = 0; i != OuterPoints.Count - 1; i++)
            {
                Vector2 from = OuterPoints[i], to = OuterPoints[i + 1];

                float averageHeight = (from.y + to.y) * 0.5f;
                float width = from.x - to.x;

                Area += averageHeight * width;
            }
            if (Area < 0.0f) Area *= -1.0f;
            terrainArea = Area;

            // Get terrainTag and terrainName
            List<Vector3> nodes = Helper.GetOutsideNodes();
            List<RaycastHit> hits = new List<RaycastHit>();
            for (int i = 0; i != nodes.Count * 10; i++) // 10 samples per node
            {
                Vector3 node = nodes[i % nodes.Count];
                Vector2 randomPoint = UnityEngine.Random.insideUnitCircle * 3.0f;
                RaycastHit hit;
                if (Physics.Raycast(new Ray(node + new Vector3(randomPoint.x, 10.0f, randomPoint.y), Vector3.down), out hit))
                {
                    hits.Add(hit);
                }
            }

            terrainTag = Helper.MostCommon(hits.Select(x => x.collider.gameObject.tag).ToList());
            terrainName = Helper.MostCommon(hits.Select(x => x.collider.gameObject.name).ToList());

            GameObject terrainMap = GameObject.FindGameObjectWithTag(Manager.terrainTag);
            GameObject[] objects = GameObject.FindGameObjectsWithTag(Manager.terrainTag);
            foreach (GameObject obj in objects)
            {
                if (obj.name == Manager.terrainName)
                {
                    terrainMap = obj;
                }
            }

            terrainObject = terrainMap;
        }

        /// <summary>
        /// This adds the enemy to given spawn list.
        /// </summary>
        /// <param name="list">List to add to.</param>
        public static void AddEnemyToPoolWithRarity(ref List<SpawnableEnemyWithRarity> list, EnemyType enemy, int rarity) => DoAddEnemyToPoolWithRarity(ref list, enemy, rarity);

        /// <summary>
        /// This adds the enemy to given spawn list.
        /// </summary>
        /// <param name="list">List to add to.</param>
        public static void AddEnemyToPoolWithRarity(ref List<SpawnableEnemyWithRarity> list, Assets.EnemyName enemy, int rarity) => DoAddEnemyToPoolWithRarity(ref list, Assets.GetEnemy(enemy), rarity);

        /// <summary>
        /// This adds the enemy to given spawn list.
        /// </summary>
        /// <param name="list">List to add to.</param>
        public static void AddEnemyToPoolWithRarity(ref List<SpawnableEnemyWithRarity> list, string enemy, int rarity) => DoAddEnemyToPoolWithRarity(ref list, Assets.GetEnemy(enemy), rarity);

        private static void DoAddEnemyToPoolWithRarity(ref List<SpawnableEnemyWithRarity> list, EnemyType enemy, int rarity)
        {
            if (enemy.enemyPrefab == null)
            {
                Log.LogError("Enemy prefab is null on AddEnemyToPoolWithRarity(), returning.");
                return;
            }
            SpawnableEnemyWithRarity spawnableEnemyWithRarity = new SpawnableEnemyWithRarity();
            spawnableEnemyWithRarity.enemyType = enemy;
            spawnableEnemyWithRarity.rarity = rarity;
            list.Add(spawnableEnemyWithRarity);
        }

        /// <summary>
        /// Sets weather effects.
        /// </summary>
        public static void SetAtmosphere(string name, bool state) => Net.Instance.currentWeatherEffects.Add(new CurrentWeatherEffect(name, state));


        /// <summary>
        /// Sets weather effects.
        /// </summary>
        public static void SetAtmosphere(Assets.AtmosphereName name, bool state) => Net.Instance.currentWeatherEffects.Add(new CurrentWeatherEffect(Assets.AtmosphereNameList[name], state));

        /// <summary>
        /// Removes enemy from all spawn lists of currentLevel.
        /// </summary>
        public static void RemoveSpawn(string name) => DoRemoveSpawn(name);

        /// <summary>
        /// Removes enemy from all spawn lists.
        /// </summary>
        public static void RemoveSpawn(Assets.EnemyName name) => DoRemoveSpawn(Assets.EnemyNameList[name]);

        private static void DoRemoveSpawn(string Name)
        {
            int amountRemoved = 0;
            try
            {
                amountRemoved += RoundManager.Instance.currentLevel.Enemies.RemoveAll(x => x.enemyType.name.ToUpper() == Name.ToUpper());
            } catch
            {
                Log.LogError("RemoveAll() on insideEnemies failed");
            }
            try
            {
                amountRemoved += RoundManager.Instance.currentLevel.OutsideEnemies.RemoveAll(x => x.enemyType.name.ToUpper() == Name.ToUpper());
            } catch
            {
                Log.LogError("RemoveAll() on outsideEnemies failed");
            }
            try
            {
                amountRemoved += RoundManager.Instance.currentLevel.DaytimeEnemies.RemoveAll(x => x.enemyType.name.ToUpper() == Name.ToUpper());
            } catch
            {
                Log.LogError("RemoveAll() on daytimeEnemies failed");
            }
            if (amountRemoved == 0) Log.LogInfo(string.Format("Failed to remove '{0}' from enemy pool, either it dosen't exist on the map or wrong string used.", Name));
        }

        /// <summary>
        /// Checks if spawn exists in any of the enemy lists of currentLevel.
        /// </summary>
        public static bool SpawnExists(string name) => DoSpawnExists(name);

        /// <summary>
        /// Checks if spawn exists in any of the enemy lists of currentLevel.
        /// </summary>
        public static bool SpawnExists(Assets.EnemyName name) => DoSpawnExists(Assets.EnemyNameList[name]);

        private static bool DoSpawnExists(string name)
        {
            try
            {
                if (RoundManager.Instance.currentLevel.Enemies.Exists(x => x.enemyType.name == name)) return true;
            } catch
            {
                Log.LogError("Exists() on insideEnemies failed");
            }
            try
            {
                if (RoundManager.Instance.currentLevel.OutsideEnemies.Exists(x => x.enemyType.name == name)) return true;
            } catch
            {
                Log.LogError("Exists() on outsideEnemies failed");
            }
            try
            {
                if (RoundManager.Instance.currentLevel.DaytimeEnemies.Exists(x => x.enemyType.name == name)) return true;
            } catch
            {
                Log.LogError("Exists() on daytimeEnemies failed");
            }
            return false;
        }

        /// <summary>
        /// This will multiply the insideSpawnCurve, outsieSpawnCurve and dayTimeSpawnCurve by said value, wont multiply negative spawn curve values.
        /// </summary>
        public static void MultiplySpawnChance(SelectableLevel currentLevel, float by)
        {
            spawnChanceMultiplier *= by;

            // Inside
            Keyframe[] insideKeyFrames = new Keyframe[currentLevel.enemySpawnChanceThroughoutDay.keys.Length];
            for (int i = 0; i < currentLevel.enemySpawnChanceThroughoutDay.keys.Length; i++)
            {
                float multiplier = by;
                if (currentLevel.enemySpawnChanceThroughoutDay.keys[i].value <= 0) multiplier = 1.0f;
                insideKeyFrames[i] = new Keyframe(currentLevel.enemySpawnChanceThroughoutDay.keys[i].time, currentLevel.enemySpawnChanceThroughoutDay.keys[i].value * multiplier);
            }
            currentLevel.enemySpawnChanceThroughoutDay = new AnimationCurve(insideKeyFrames);

            // Outside
            Keyframe[] outsideKeyFrames = new Keyframe[currentLevel.outsideEnemySpawnChanceThroughDay.keys.Length];
            for (int i = 0; i < currentLevel.outsideEnemySpawnChanceThroughDay.keys.Length; i++)
            {
                float multiplier = by;
                if (currentLevel.outsideEnemySpawnChanceThroughDay.keys[i].value <= 0) multiplier = 1.0f;
                outsideKeyFrames[i] = new Keyframe(currentLevel.outsideEnemySpawnChanceThroughDay.keys[i].time, currentLevel.outsideEnemySpawnChanceThroughDay.keys[i].value * multiplier);
            }
            currentLevel.outsideEnemySpawnChanceThroughDay = new AnimationCurve(outsideKeyFrames);

            // Daytime
            Keyframe[] daytimeKeyFrames = new Keyframe[currentLevel.daytimeEnemySpawnChanceThroughDay.keys.Length];
            for (int i = 0; i < currentLevel.daytimeEnemySpawnChanceThroughDay.keys.Length; i++)
            {
                float multiplier = by;
                if (currentLevel.daytimeEnemySpawnChanceThroughDay.keys[i].value <= 0) multiplier = 1.0f;
                daytimeKeyFrames[i] = new Keyframe(currentLevel.daytimeEnemySpawnChanceThroughDay.keys[i].time, currentLevel.daytimeEnemySpawnChanceThroughDay.keys[i].value * multiplier);
            }
            currentLevel.daytimeEnemySpawnChanceThroughDay = new AnimationCurve(daytimeKeyFrames);
        }

        internal static void AddInsideSpawnChance(SelectableLevel currentLevel, float value)
        {
            Keyframe[] insideKeyFrames = new Keyframe[currentLevel.enemySpawnChanceThroughoutDay.keys.Length];
            for (int i = 0; i < currentLevel.enemySpawnChanceThroughoutDay.keys.Length; i++)
            {
                insideKeyFrames[i] = new Keyframe(currentLevel.enemySpawnChanceThroughoutDay.keys[i].time, currentLevel.enemySpawnChanceThroughoutDay.keys[i].value + value);
            }
            currentLevel.enemySpawnChanceThroughoutDay = new AnimationCurve(insideKeyFrames);
        }

        internal static void AddOutsideSpawnChance(SelectableLevel currentLevel, float value)
        {
            Keyframe[] outsideKeyFrames = new Keyframe[currentLevel.outsideEnemySpawnChanceThroughDay.keys.Length];
            for (int i = 0; i < currentLevel.outsideEnemySpawnChanceThroughDay.keys.Length; i++)
            {
                outsideKeyFrames[i] = new Keyframe(currentLevel.outsideEnemySpawnChanceThroughDay.keys[i].time, currentLevel.outsideEnemySpawnChanceThroughDay.keys[i].value + value);
            }
            currentLevel.outsideEnemySpawnChanceThroughDay = new AnimationCurve(outsideKeyFrames);
        }

        public static void PayCredits(int amount)
        {
            if (amount == 0) return;
            currentTerminal.groupCredits += amount;
            currentTerminal.SyncGroupCreditsServerRpc(currentTerminal.groupCredits, currentTerminal.numberOfItemsInDropship);

            bool isPositive = (amount >= 0);
            HUDManager.Instance.AddTextToChatOnServer(string.Format("<color={0}>{1}{2}■</color>", isPositive ? "#008000" : "#FF0000", isPositive ? "+" : "", amount));
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(TimeOfDay), nameof(TimeOfDay.MoveGlobalTime))]
        private static void OnMoveGlobaTime(ref TimeOfDay __instance, ref float ___timeUntilDeadline, ref float ___globalTime)
        {
            if(moveTime)
            {
                ___timeUntilDeadline -= moveTimeAmount;
                ___globalTime += moveTimeAmount;
                __instance.globalTimeSpeedMultiplier *= timeSpeedMultiplier;

                moveTimeAmount = 0.0f;
                inverseTimeSpeedMultiplier = 1.0f / timeSpeedMultiplier;
                timeSpeedMultiplier = 1.0f;
                moveTime = false;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StartOfRound), "ShipLeave")]
        private static void OnShipLeave()
        {
            TimeOfDay.Instance.globalTimeSpeedMultiplier *= inverseTimeSpeedMultiplier;
            inverseTimeSpeedMultiplier = 1.0f;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TimeOfDay), "Start")]
        private static void OnTimeOfDayStart() => inverseTimeSpeedMultiplier = 1.0f;

        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(RoundManager), "FinishGeneratingLevel")]
        private static void ObjectSpawnHandling()
        {
            SampleMap();

            // Client side objects
            randomSeedValue = StartOfRound.Instance.randomMapSeed + 2 + Net.Instance.GiveSeed(); // Reset seed value
            RoundManager.Instance.StartCoroutine(DelayedExecution());

            // Net objects
            foreach (ObjectInfo obj in insideObjectsToSpawnOutside) SpawnOutsideObjects(obj.obj, new Vector3(0.0f, -0.05f, 0.0f), obj.density, -1, 250); // 250 Cap for outside landmines and turrets as such
        }

        private static IEnumerator DelayedExecution() // Delay this to fix trees not spawning in correctly on clients
        {
            yield return new WaitForSeconds(5.0f);
            foreach (OutsideObjectsToSpawn obj in Net.Instance.outsideObjectsToSpawn)
            {
                SpawnOutsideObjects(Assets.GetObject((Assets.ObjectName)obj.objectEnumID), new Vector3(0.0f, -1.0f, 0.0f), obj.density, -1, 1000); // 1000 cap for trees as such
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RoundManager), "AdvanceHourAndSpawnNewBatchOfEnemies")]
        private static void OnAdvanceHourAndSpawnNewBatchOfEnemies(ref RoundManager __instance)
        {
            __instance.minEnemiesToSpawn += minEnemiesToSpawnInside;
            __instance.minOutsideEnemiesToSpawn += minEnemiestoSpawnOutside;

            minEnemiesToSpawnInside = 0;
            minEnemiestoSpawnOutside = 0;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RoundManager), "RefreshEnemyVents")]
        private static void OnRefreshEnemyVents()
        {
            if (RoundManager.Instance.allEnemyVents.Length == 0) return;

            List<EnemyAI> spawnedAI = new List<EnemyAI>();

            spawnedAI.AddRange(DoSpawnInsideEnemies());
            spawnedAI.AddRange(DoSpawnOutsideEnemies());
        }
        
        internal struct ObjectInfo
        {
            public int count;
            public float radius;
            public float density;
            public GameObject obj;

            public ObjectInfo(GameObject obj, int count)
            {
                this.obj = obj;
                this.count = count;
                radius = 0.0f;
                density = 0.0f;
            }

            public ObjectInfo(GameObject obj, float density)
            {
                this.obj = obj;
                this.density = density;
                radius = 0.0f;
                count = 0;
            }

            public ObjectInfo(GameObject obj, float density, float radius)
            {
                this.obj = obj;
                this.density = density;
                this.radius = radius;
                count = 0;
            }

            public ObjectInfo(GameObject obj, int count, float density, float radius)
            {
                this.obj = obj;
                this.density = density;
                this.radius = radius;
                this.count = count;
            }
        }

        public struct ScrapSpawnInfo
        {
            public NetworkObjectReference[] netObjects;
            public int[] scrapPrices;

            public ScrapSpawnInfo(NetworkObjectReference[] netObjects, int[] scrapPrices)
            {
                this.netObjects = netObjects;
                this.scrapPrices = scrapPrices;
            }
        }
    }
}