﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using OpenTK;
using VEngine;

namespace ShadowsTester
{
    internal class Program
    {
        private class Config
        {
            public static int Width = 1366;
            public static string MediaPath = "media";
            public static int Height = 768;
        }

        [STAThread]
        private static void Main(string[] args)
        {
            Game.Initialize(new Size(Config.Width, Config.Height),1, Config.MediaPath, GameWindowFlags.Default);

            var freeCamera = Commons.SetUpFreeCamera();
            //System.Threading.Thread.Sleep(1100);
            Commons.AddControllableLight();
            Commons.SetUpInputBehaviours();

            new VictorianHouseScene();
            //new OldCityScene();
            //new PhysicsTest();
            //new LightningTestScene();
            //new HotelScene();

            //new DragonScene();

            System.Threading.Thread.CurrentThread.Join();
        }
    }
}