﻿using System;
using HarmonyLib;
using BepInEx;
using UnityEngine;
using System.Reflection;
using BrutalCompanyMinus.Minus;
using System.Collections.Generic;
using BrutalCompanyMinus.Minus.Handlers;
using UnityEngine.Diagnostics;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine.InputSystem.HID;
using Discord;
using System.Diagnostics;
using BepInEx.Configuration;
using System.Globalization;

namespace BrutalCompanyMinus
{
    [HarmonyPatch]
    [BepInPlugin(GUID, NAME, VERSION)]
    internal class Plugin : BaseUnityPlugin
    {
        private const string GUID = "Drinkable.BrutalCompanyMinus";
        private const string NAME = "BrutalCompanyMinus";
        private const string VERSION = "0.11.0";
        private static readonly Harmony harmony = new Harmony(GUID);

        void Awake()
        {
            // Logger
            Log.Initalize(Logger);

            // Required for netweaving
            var EventTypes = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var EventType in EventTypes)
            {
                var methods = EventType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }

            // Load assets
            Assets.Load();

            // Patch all
            harmony.PatchAll();
            harmony.PatchAll(typeof(GrabObjectTranspiler));

            Log.LogInfo(NAME + " " + VERSION + " " + "is done patching.");
        }
    }
}
