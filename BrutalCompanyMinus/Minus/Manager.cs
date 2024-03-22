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

namespace BrutalCompanyMinus.Minus
{
    [HarmonyPatch]
    public class Manager
    {

        public static int daysPassed = -1;
        public static SelectableLevel currentLevel;
        public static Terminal currentTerminal;

        internal static float terrainArea = 0.0f;
        internal static string terrainTag = "";
        internal static string terrainName = "";
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
        internal static List<SpawnableItemWithRarity> ScrapToTransmuteTo = new List<SpawnableItemWithRarity>();

        public static class Spawn
        {
            internal static int randomSeedValue = 0;

            private static List<Vector3> spawnDenialPoints = new List<Vector3>();
            internal static void OutsideObjects(GameObject obj, Vector3 offset, float density, float radius = -1.0f, int objectCap = 1000)
            {
                if (obj == null) return;

                spawnDenialPoints = Functions.GetSpawnDenialNodes();

                int count = (int)Mathf.Clamp(density * terrainArea, 0, objectCap); // Compute amount
                Log.LogInfo(string.Format("Spawning: {0}, Count:{1}", obj.name, count));

                int batchSize = 8;
                int batches = count / batchSize;
                int remainder = count % batchSize;

                for(int i = 0; i < batches; i++)
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

            internal static void DoSpawnOutsideObjects(int count, float radius, Vector3 offset, GameObject obj) 
            {
                for (int i = 0; i < count; i++)
                {
                    randomSeedValue++;

                    UnityEngine.Random.InitState(randomSeedValue); // Important or wont be same on all clients
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

            public static void OutsideEnemies(EnemyType enemy, int count) => enemiesToSpawnOutside.Add(new ObjectInfo(enemy.enemyPrefab, count));
            public static void InsideEnemies(EnemyType enemy, int count, float radius = 0.0f) => enemiesToSpawnInside.Add(new ObjectInfo(enemy.enemyPrefab, count, 0.0f, radius));
            public static void ScrapOutside(int Amount) => randomItemsToSpawnOutsideCount += Amount;

            internal static void DoSpawnOutsideEnemies()
            {
                List<Vector3> OutsideAiNodes = Functions.GetOutsideNodes();
                List<Vector3> SpawnDenialNodes = Functions.GetSpawnDenialNodes();

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
                            Functions.GetSafePosition(OutsideAiNodes, SpawnDenialNodes, 20.0f),
                            Quaternion.Euler(Vector3.zero));

                        RoundManager.Instance.SpawnedEnemies.Add(obj.GetComponent<EnemyAI>());

                        obj.gameObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
                    }
                }
                enemiesToSpawnOutside.Clear();
            }
            internal static void DoSpawnInsideEnemies()
            {
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
                        EnemyAI component = gameObject.GetComponent<EnemyAI>();
                        RoundManager.Instance.SpawnedEnemies.Add(component);
                    }
                }
                enemiesToSpawnInside.Clear();
            }

            internal static ScrapSpawnInfo DoSpawnScrapOutside(int Amount)
            {
                if (Amount <= 0) return new ScrapSpawnInfo(new NetworkObjectReference[] { }, new int[] { });

                RoundManager r = RoundManager.Instance;
                System.Random rng = new System.Random();

                // Generate Scrap To Spawn
                List<Item> ScrapToSpawn = GetScrapToSpawn((int)(Amount * r.scrapAmountMultiplier * scrapAmountMultiplier));
                List<int> ScrapValues = new List<int>();

                // Spawn Scrap
                List<NetworkObjectReference> ScrapSpawnsNet = new List<NetworkObjectReference>();
                List<Vector3> OutsideNodes = Functions.GetOutsideNodes();

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

            internal static ScrapSpawnInfo DoSpawnScrapInside(int Amount) 
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
                    if(scrapSpawnPositions.Count <= 0)
                    {
                        Log.LogError("No positions to spawn scrap: " + ScrapToSpawn[i].itemName);
                        continue;
                    }
                    if(usedSpawns.Count > 0 && scrapSpawnPositions.Contains(randomScrapSpawn))
                    {
                        scrapSpawnPositions.RemoveAll((RandomScrapSpawn x) => usedSpawns.Contains(x));
                        if(scrapSpawnPositions.Count <= 0)
                        {
                            usedSpawns.Clear();
                            i--;
                            continue;
                        }
                    }

                    randomScrapSpawn = scrapSpawnPositions[rng.Next(0, scrapSpawnPositions.Count)];
                    usedSpawns.Add(randomScrapSpawn);

                    Vector3 pos;
                    if(randomScrapSpawn.spawnedItemsCopyPosition)
                    {
                        randomScrapSpawn.spawnUsed = true;
                        pos = randomScrapSpawn.transform.position;
                    } else
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
        }

        public static void TransmuteScrap(params SpawnableItemWithRarity[] Items)
        {
            transmuteScrap = true;
            ScrapToTransmuteTo.AddRange(Items);
        }

        public static void DeliverRandomItems(int Amount, int MaxPrice)
        {
            if (RoundManager.Instance.IsServer)
            {
                Terminal terminal = GameObject.FindObjectOfType<Terminal>();

                List<int> validItems = new List<int>();
                for (int i = 0; i < terminal.buyableItemsList.Length; i++)
                {
                    if (terminal.buyableItemsList[i].creditsWorth <= MaxPrice) validItems.Add(i);
                }

                for (int i = 0; i < Amount; i++)
                {
                    int item = validItems[UnityEngine.Random.Range(0, validItems.Count)];
                    terminal.orderedItemsFromTerminal.Add(item);
                }
            }
        }

        public static int GetLevelIndex()
        {
            for(int i = 0; i < StartOfRound.Instance.levels.Length; i++)
            {
                if (StartOfRound.Instance.levels[i].name == RoundManager.Instance.currentLevel.name) return i;
            }
            return 0;
        }

        public static void AddEnemyHp(int amount) => bonusEnemyHp += amount;
        public static void AddInsidePower(int amount) => bonusMaxInsidePowerCount += amount;
        public static void AddOutsidePower(int amount) => bonusMaxOutsidePowerCount += amount;

        public static void MultiplySpawnCap(float multiplier) => spawncapMultipler *= multiplier;

        internal static void SampleMap()
        {
            // Compute Map Area
            List<Vector2> OuterPoints = new List<Vector2>();
            foreach (GameObject outsideAiNode in RoundManager.Instance.outsideAINodes)
            {
                OuterPoints.Add(new Vector2(outsideAiNode.transform.position.x, outsideAiNode.transform.position.z));
            }
            OuterPoints = Functions.ComputeConvexHull(OuterPoints).ToList();

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
            List<Vector3> nodes = Functions.GetOutsideNodes();
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

            terrainTag = Functions.MostCommon(hits.Select(x => x.collider.gameObject.tag).ToList());
            terrainName = Functions.MostCommon(hits.Select(x => x.collider.gameObject.name).ToList());
        }

        public static void AddEnemyToPoolWithRarity(ref List<SpawnableEnemyWithRarity> list, EnemyType enemy, int rarity)
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

        public static void SetAtmosphere(string name, bool state) => Net.Instance.currentWeatherEffects.Add(new CurrentWeatherEffect(name, state));

        public static void RemoveSpawn(string Name)
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

        public static bool SpawnExists(string name)
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

        public static void AddInsideSpawnChance(float value)
        {
            Keyframe[] insideKeyFrames = new Keyframe[currentLevel.enemySpawnChanceThroughoutDay.keys.Length];
            for (int i = 0; i < currentLevel.enemySpawnChanceThroughoutDay.keys.Length; i++)
            {
                insideKeyFrames[i] = new Keyframe(currentLevel.enemySpawnChanceThroughoutDay.keys[i].time, currentLevel.enemySpawnChanceThroughoutDay.keys[i].value + value);
            }
            currentLevel.enemySpawnChanceThroughoutDay = new AnimationCurve(insideKeyFrames);
        }

        public static void AddOutsideSpawnChance(float value)
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
            HUDManager.Instance.AddTextToChatOnServer(string.Format("<color={0}>{1}{2} ■</color>", isPositive ? "#008000" : "#FF0000", isPositive ? "+" : "", amount));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RoundManager), "FinishGeneratingLevel")]
        private static void ObjectSpawnHandling()
        {
            SampleMap();

            // Client side objects
            Spawn.randomSeedValue = StartOfRound.Instance.randomMapSeed + 2; // Reset seed value
            RoundManager.Instance.StartCoroutine(DelayedExecution());

            // Net objects
            foreach (ObjectInfo obj in insideObjectsToSpawnOutside) Spawn.OutsideObjects(obj.obj, new Vector3(0.0f, -0.05f, 0.0f), obj.density, -1, 250); // 250 Cap for outside landmines and turrets as such
        }

        private static IEnumerator DelayedExecution() // Delay this to fix trees not spawning in correctly on clients
        {
            yield return new WaitForSeconds(5.0f);
            foreach (OutsideObjectsToSpawn obj in Net.Instance.outsideObjectsToSpawn)
            {
                Spawn.OutsideObjects(Assets.GetObject((Assets.ObjectName)obj.objectEnumID), new Vector3(0.0f, -1.0f, 0.0f), obj.density, -1, 1000); // 1000 cap for trees as such
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RoundManager), "RefreshEnemyVents")]
        private static void OnRefreshEnemyVents()
        {
            if (RoundManager.Instance.allEnemyVents.Length == 0) return;

            Spawn.DoSpawnInsideEnemies();
            Spawn.DoSpawnOutsideEnemies();
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

        internal struct ScrapSpawnInfo
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

    
    // Mostly used for sample map
    internal static class Functions
    {
        public static List<Vector3> GetOutsideNodes() => GameObject.FindGameObjectsWithTag("OutsideAINode").Select(n => n.transform.position).ToList();
        public static List<Vector3> GetSpawnDenialNodes()
        {
            List<Vector3> nodes = GameObject.FindGameObjectsWithTag("SpawnDenialPoint").Select(n => n.transform.position).ToList();
            nodes.Add(GameObject.FindGameObjectWithTag("ItemShipLandingNode").transform.position);

            switch (RoundManager.Instance.currentLevel.name) // Custom denial points so spawned objects dont block something
            {
                case "ExperimentationLevel":
                    nodes.Add(new Vector3(-72, 0, -100));
                    nodes.Add(new Vector3(-72, 0, -45));
                    nodes.Add(new Vector3(-72, 0, 15));
                    nodes.Add(new Vector3(-72, 0, 75));
                    nodes.Add(new Vector3(-30, 2, -30));
                    nodes.Add(new Vector3(-20, -2, 75));
                    break;
                case "AssuranceLevel":
                    nodes.Add(new Vector3(63, -2, -43));
                    nodes.Add(new Vector3(120, -1, 75));
                    break;
                case "OffenseLevel":
                    nodes.Add(new Vector3(120, 10, -65));
                    break;
                case "DineLevel":
                    nodes.Add(new Vector3(-40, 0, 80));
                    break;
                case "TitanLevel":
                    nodes.Add(new Vector3(-16, -3, 5));
                    nodes.Add(new Vector3(-50, 20, -30));
                    break;
            }

            return nodes;
        }

        public static int[] IntArray(this float[] Values)
        {
            int[] newValues = new int[Values.Length];
            for(int i = 0; i < Values.Length; i++)
            {
                newValues[i] = (int)Values[i];
            }
            return newValues;
        }

        public static Vector3 GetSafePosition(List<Vector3> nodes, List<Vector3> denialNodes, float radius)
        {
            Vector3 position = nodes[UnityEngine.Random.Range(0, nodes.Count)];
            int Iteration = 0;

            while (true)
            {
                Iteration++;
                Vector3 newPosition = RoundManager.Instance.GetRandomNavMeshPositionInRadius(position, radius);
                bool foundSafe = true;
                foreach (Vector3 node in denialNodes)
                {
                    if (Vector3.Distance(node, newPosition) <= 16.0f) foundSafe = false;
                }
                if (foundSafe)
                {
                    position = newPosition;
                    break;
                }
                if (Iteration > 51)
                {
                    Log.LogError("GetSafePosition() got stuck, returning " + position);
                    break;
                }
                if (Iteration % 10 == 0) // Refresh if not found
                {
                    position = nodes[UnityEngine.Random.Range(0, nodes.Count)];
                }
            }

            return position;
        }

        public static string MostCommon(List<string> list)
        {
            string mostCommon = "";

            if (list != null && list.Count > 0)
            {
                Dictionary<string, int> counts = new Dictionary<string, int>();

                foreach (string s in list)
                {
                    if (counts.ContainsKey(s))
                    {
                        counts[s]++;
                    }
                    else
                    {
                        counts.Add(s, 1);
                    }
                }

                int max = 0;
                foreach (KeyValuePair<string, int> count in counts)
                {
                    if (count.Value > max)
                    {
                        mostCommon = count.Key;
                        max = count.Value;
                    }
                }

            }
            return mostCommon;
        }

        public static IList<Vector2> ComputeConvexHull(List<Vector2> points, bool sortInPlace = false) // Taken from https://gist.github.com/dLopreiato/7fd142d0b9728518552188794b8a750c
        {
            if (!sortInPlace)
                points = new List<Vector2>(points);
            points.Sort((a, b) =>
                a.x == b.x ? a.y.CompareTo(b.y) : (a.x > b.x ? 1 : -1));

            CircularList<Vector2> hull = new CircularList<Vector2>();
            int L = 0, U = 0;

            for (int i = points.Count - 1; i >= 0; i--)
            {
                Vector2 p = points[i], p1;

                while (L >= 2 && (p1 = hull.Last).Sub(hull[hull.Count - 2]).Cross(p.Sub(p1)) >= 0)
                {
                    hull.PopLast();
                    L--;
                }
                hull.PushLast(p);
                L++;

                while (U >= 2 && (p1 = hull.First).Sub(hull[1]).Cross(p.Sub(p1)) <= 0)
                {
                    hull.PopFirst();
                    U--;
                }
                if (U != 0)
                    hull.PushFirst(p);
                U++;
                Debug.Assert(U + L == hull.Count + 1);
            }
            hull.PopLast();
            return hull;
        }

        private static Vector2 Sub(this Vector2 a, Vector2 b)
        {
            return a - b;
        }

        private static float Cross(this Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private class CircularList<T> : List<T>
        {
            public T Last
            {
                get
                {
                    return this[this.Count - 1];
                }
                set
                {
                    this[this.Count - 1] = value;
                }
            }

            public T First
            {
                get
                {
                    return this[0];
                }
                set
                {
                    this[0] = value;
                }
            }

            public void PushLast(T obj)
            {
                this.Add(obj);
            }

            public T PopLast()
            {
                T retVal = this[this.Count - 1];
                this.RemoveAt(this.Count - 1);
                return retVal;
            }

            public void PushFirst(T obj)
            {
                this.Insert(0, obj);
            }

            public T PopFirst()
            {
                T retVal = this[0];
                this.RemoveAt(0);
                return retVal;
            }
        }
    }
}