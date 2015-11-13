﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace VEngine
{
    public class RainSystem
    {
        class RainDrop
        {
            public Vector3 Center;
            public float Radius;
            public RainDrop(Vector3 center)
            {
                Center = center;
                Radius = 0;
            }
        }

        float MaxRadius;
        float MaxDrops;
        float Speed = 1.0f;
        float Strength;
        List<RainDrop> Drops;
        ShaderStorageBuffer DropsBuffer;
        DateTime LastUpdate;

        public RainSystem(float maxRadius, float maxDrops, float speed, float strength)
        {
            Drops = new List<RainDrop>();
            DropsBuffer = new ShaderStorageBuffer();
            MaxRadius = maxRadius;
            MaxDrops = maxDrops;
            Strength = strength;
            LastUpdate = DateTime.Now;
            GLThread.OnUpdate += UpdateDrops;
        }

        private void UpdateDrops(object sender, EventArgs e)
        {
            lock(Drops)
            {
                for(int i = 0; i < Drops.Count; i++)
                {
                    Drops[i].Radius += (0.01f * Speed);
                }
                LastUpdate = DateTime.Now;
                Drops = Drops.Where((a) => a.Radius < MaxRadius).ToList();
            }
        }

        public void AddDrop(Vector3 position)
        {
            lock (Drops)
            {
                Drops.Add(new RainDrop(position));
                if(Drops.Count > MaxDrops)
                {
                    Drops = Drops.Skip(1).ToList();
                }
            }
        }

        public void MapToCurrentShader()
        {
            var shader = ShaderProgram.Current;
            List<Vector4> buf = new List<Vector4>();
            for(int i = 0; i < Drops.Count; i++)
            {
                buf.Add(new Vector4(Drops[i].Center, Drops[i].Radius));
            }
            DropsBuffer.MapData(buf.ToArray());
            DropsBuffer.Use(4);
            shader.SetUniform("DropsCount", Drops.Count);
            shader.SetUniform("DropsMaxRadius", MaxRadius);
            shader.SetUniform("DropsStrength", Strength);
        }
        
    }
}