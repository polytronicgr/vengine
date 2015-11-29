﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using VEngine.PathTracing;

namespace VEngine
{
    public class PostProcessing
    {
        public class AAB
        {
            public Vector4 Color;
            public Vector3 Maximum;
            public Vector3 Minimum;
            public AABContainers Container = null;

            public AAB(Vector4 color, Vector3 min, Vector3 max, AABContainers container)
            {
                Color = color;
                Minimum = min;
                Maximum = max;
                Container = container;
            }
        }
        public class AABContainers
        {
            public Vector3 Maximum;
            public Vector3 Minimum;

            public AABContainers(Vector3 min, Vector3 max)
            {
                Minimum = min;
                Maximum = max;
            }
        }
        
        public MRTFramebuffer MRT;
        public int Width, Height;
        public bool ShowSelected = false;
        public bool UnbiasedIntegrateRenderMode = false;

        public float LastDeferredTime = 0;
        public float LastSSAOTime = 0;
        public float LastIndirectTime = 0;
        public float LastCombinerTime = 0;
        public float LastFogTime = 0;
        public float LastMRTTime = 0;
        public float LastHDRTime = 0;
        public float LastTotalFrameTime = 0;

        private bool DisablePostEffects = false;
        

        public float VDAOGlobalMultiplier = 1.0f, RSMGlobalMultiplier = 1.0f, AOGlobalModifier = 1.0f;

        private uint[] postProcessingPlaneIndices = {
                0, 1, 2, 3, 2, 1
            };

        private float[] postProcessingPlaneVertices = {
                -1.0f, -1.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f,
                1.0f, -1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f,
                -1.0f, 1.0f, 1.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
                1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.0f, 0.0f, 0.0f
            };

        private static Random Rand = new Random();

        private ShaderProgram
            BloomShader,
            FogShader,
            HDRShader,
            BlitShader,
            CombinerShader;
        
        public CubeMapTexture CubeMap;

        private Framebuffer LastFrameBuffer;

        private long lastTime = 0;

        private Texture NumbersTexture;

        private Framebuffer
            Pass1FrameBuffer,
            Pass2FrameBuffer,
           LastDeferredFramebuffer,
            BloomFrameBuffer,
            FogFramebuffer;

        private Object3dInfo PostProcessingMesh;
        // public static uint RandomIntFrame = 1;

        // public static Texture3D FullScene3DTexture;

        private Stopwatch stopwatch = new Stopwatch(), separatestowatch = new Stopwatch();

        private void StartMeasureMS()
        {
            separatestowatch.Reset();
            separatestowatch.Start();
        }

        public float StopMeasureMS()
        {
            separatestowatch.Stop();
            long ticks = separatestowatch.ElapsedTicks;
            double ms = 1000.0 * (double)ticks / Stopwatch.Frequency;
            return (float)ms;
        }

        public PostProcessing(int initialWidth, int initialHeight)
        {/*
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, 0u);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, 0u);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, 0u);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, 0u);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 4, 0u);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 5, 0u);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 6, 0u);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 7, 0u);*/
            //FullScene3DTexture = new Texture3D(new Vector3(64, 64, 64));
            NumbersTexture = new Texture(Media.Get("numbers.png"));
            CubeMap = new CubeMapTexture(Media.Get("posx.jpg"), Media.Get("posy.jpg"), Media.Get("posz.jpg"),
                Media.Get("negx.jpg"), Media.Get("negy.jpg"), Media.Get("negz.jpg"));

            Width = initialWidth;
            Height = initialHeight;
          //   initialWidth *= 2; initialHeight *= 2;
            MRT = new MRTFramebuffer(initialWidth, initialHeight);

            Pass1FrameBuffer = new Framebuffer(initialWidth, initialHeight);
            Pass2FrameBuffer = new Framebuffer(initialWidth, initialHeight);

            BloomFrameBuffer = new Framebuffer(initialWidth / 6, initialHeight / 6)
            {
                ColorOnly = true,
                ColorInternalFormat = PixelInternalFormat.Rgba,
                ColorPixelFormat = PixelFormat.Rgba,
                ColorPixelType = PixelType.UnsignedByte
            };

            FogFramebuffer = new Framebuffer(initialWidth / 2, initialHeight / 2)
            {
                ColorOnly = true,
                ColorInternalFormat = PixelInternalFormat.Rgba,
                ColorPixelFormat = PixelFormat.Rgba,
                ColorPixelType = PixelType.UnsignedByte
            };

             LastDeferredFramebuffer = new Framebuffer(initialWidth / 1, initialHeight / 1)
             {
                 ColorOnly = true,
                 ColorInternalFormat = PixelInternalFormat.Rgba,
                 ColorPixelFormat = PixelFormat.Rgba,
                 ColorPixelType = PixelType.UnsignedByte
             };


            BloomShader = ShaderProgram.Compile("PostProcess.vertex.glsl", "Bloom.fragment.glsl");
            FogShader = ShaderProgram.Compile("PostProcess.vertex.glsl", "Fog.fragment.glsl");
            HDRShader = ShaderProgram.Compile("PostProcess.vertex.glsl", "HDR.fragment.glsl");
            BlitShader = ShaderProgram.Compile("PostProcess.vertex.glsl", "Blit.fragment.glsl");
            CombinerShader = ShaderProgram.Compile("PostProcess.vertex.glsl", "Combiner.fragment.glsl");
            
            PostProcessingMesh = new Object3dInfo(postProcessingPlaneVertices, postProcessingPlaneIndices);
        }

        private enum BlurMode
        {
            Linear, Gaussian, Temporal, Additive
        }

        private Matrix4 LastVP = Matrix4.Identity;

        private void RenderCore()
        {
            MRT.Use();
            World.Root.Draw();

            SwitchToFB(Pass1FrameBuffer);

            MRT.UseTextureDiffuseColor(14);
            MRT.UseTextureDepth(1);
            MRT.UseTextureNormals(16);
            MRT.UseTextureMeshData(17);
            MRT.UseTextureId(18);

            Combine();

            Pass1FrameBuffer.GenerateMipMaps();

            SwitchToFB(Pass2FrameBuffer);

            Pass1FrameBuffer.UseTexture(0);
            MRT.UseTextureDepth(1);
            NumbersTexture.Use(TextureUnit.Texture25);

            HDR(lastTime == 0 ? 0 : 1000000 / lastTime);
        }

        private void RenderPrepareToBlit()
        {
            if(UnbiasedIntegrateRenderMode)
            {
                //RandomsSSBO.MapData(JitterRandomSequenceGenerator.Generate(1, 16 * 16 * 16, true).ToArray());
            }
            stopwatch.Stop();
            lastTime = (lastTime * 20 + stopwatch.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L))) / 21;
            stopwatch.Reset();
            stopwatch.Start();

            StartMeasureMS();
            MRT.Use();
            LastMRTTime = StopMeasureMS();
            World.Root.Draw();

            MRT.UseTextureDiffuseColor(14);
            MRT.UseTextureDepth(1);
            MRT.UseTextureNormals(16);
            MRT.UseTextureMeshData(17);
            MRT.UseTextureId(18);

            if(GLThread.GraphicsSettings.UseFog)
            {
                SwitchToFB(FogFramebuffer);
                Fog();
            }

            SwitchToFB(Pass1FrameBuffer);
            
            FogFramebuffer.UseTexture(24);
            Combine();

            if(GLThread.GraphicsSettings.UseBloom)
            {
                SwitchToFB(BloomFrameBuffer);
                Pass1FrameBuffer.UseTexture(0);
                Bloom();
                BloomFrameBuffer.UseTexture(26);
            }

            Pass1FrameBuffer.GenerateMipMaps();
            SwitchToFB(Pass2FrameBuffer);


            Pass1FrameBuffer.UseTexture(0);
            MRT.UseTextureDepth(1);
            NumbersTexture.Use(TextureUnit.Texture25);

            HDR(lastTime == 0 ? 0 : 1000000 / lastTime);

            SwitchToFB(LastDeferredFramebuffer);
            if(UnbiasedIntegrateRenderMode)
            {
                Pass2FrameBuffer.UseTexture(0);
                MRT.UseTextureDepth(1);
                Blit();
            }
        }
        public void RenderToFramebuffer(Framebuffer framebuffer)
        {
            Width = framebuffer.Width;
            Height = framebuffer.Height;

            RenderPrepareToBlit();

            framebuffer.Use(false, false);
            GL.Viewport(0, 0, Width, Height);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            Pass2FrameBuffer.UseTexture(0);
            MRT.UseTextureDepth(1);
            Blit();

        }
        
        private void FaceRender(CubeMapFramebuffer framebuffer, TextureTarget target)
        {
            GL.Enable(EnableCap.DepthTest);
            framebuffer.SwitchCamera(target);
            RenderCore();

             framebuffer.Use(true, false);
             framebuffer.SwitchFace(target);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
           //   framebuffer.Use(true, true);
             Pass2FrameBuffer.UseTexture(0);
              MRT.UseTextureDepth(1);
             Blit();
              framebuffer.GenerateMipMaps();
        }

        public void RenderToCubeMapFramebuffer(CubeMapFramebuffer framebuffer)
        {
            World.Root.RootScene.RecreateSimpleLightsSSBO();
            Width = framebuffer.Width;
            Height = framebuffer.Height;

           // framebuffer.Use(true, true);
            var cam = Camera.Current;
            DisablePostEffects = true;
            FaceRender(framebuffer, TextureTarget.TextureCubeMapPositiveX);
            FaceRender(framebuffer, TextureTarget.TextureCubeMapPositiveY);
            FaceRender(framebuffer, TextureTarget.TextureCubeMapPositiveZ);

            FaceRender(framebuffer, TextureTarget.TextureCubeMapNegativeX);
            FaceRender(framebuffer, TextureTarget.TextureCubeMapNegativeY);
            FaceRender(framebuffer, TextureTarget.TextureCubeMapNegativeZ);
            DisablePostEffects = false;
            GL.Enable(EnableCap.DepthTest);
            Camera.Current = cam;

        }

        private void Blit()
        {
            BlitShader.Use();
            DrawPPMesh();
        }

        private void Bloom()
        {
            BloomShader.Use();
            DrawPPMesh();
        }

        private float DrawPPMesh()
        {
            StartMeasureMS();
            SetUniformsShared();
            PostProcessingMesh.Draw();
            return StopMeasureMS();
        }

        private void Combine()
        {
            CombinerShader.Use();
            World.Root.RootScene.SetLightingUniforms(CombinerShader, Matrix4.Identity);
            //RandomsSSBO.Use(0);
            MRT.UseTextureDepth(1);
            MRT.UseTextureNormals(16);
            MRT.UseTextureMeshData(17);
            MRT.UseTextureId(18);
            LastDeferredFramebuffer.UseTexture(20);
            CubeMap.Use(TextureUnit.Texture19);
            World.Root.RootScene.MapLightsSSBOToShader(CombinerShader);
            CombinerShader.SetUniform("RandomsCount", 16 * 16 * 16);
            CombinerShader.SetUniform("UseFog", GLThread.GraphicsSettings.UseFog);
            CombinerShader.SetUniform("UseLightPoints", GLThread.GraphicsSettings.UseLightPoints);
            CombinerShader.SetUniform("UseDepth", GLThread.GraphicsSettings.UseDepth);
            CombinerShader.SetUniform("UseDeferred", GLThread.GraphicsSettings.UseDeferred);
            CombinerShader.SetUniform("UseVDAO", GLThread.GraphicsSettings.UseVDAO);
            CombinerShader.SetUniform("UseHBAO", GLThread.GraphicsSettings.UseHBAO);
            CombinerShader.SetUniform("UseRSM", GLThread.GraphicsSettings.UseRSM);
            CombinerShader.SetUniform("UseSSReflections", GLThread.GraphicsSettings.UseSSReflections);
            CombinerShader.SetUniform("UseHBAO", GLThread.GraphicsSettings.UseHBAO);
            CombinerShader.SetUniform("Brightness", Camera.Current.Brightness);
            CombinerShader.SetUniform("VDAOGlobalMultiplier", VDAOGlobalMultiplier);
            CombinerShader.SetUniform("DisablePostEffects", DisablePostEffects);
            // GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
            LastCombinerTime = DrawPPMesh();
        }
        
        private void DisableBlending()
        {
           // GL.Disable(EnableCap.Blend);
           // GL.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.Zero);
        }

        private void EnableFullBlend()
        {
          //  GL.Disable(EnableCap.Blend);
          //  GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
           // GL.BlendEquation(BlendEquationMode.FuncAdd);
        }

        private void Fog()
        {
            FogShader.Use();
            World.Root.RootScene.SetLightingUniforms(FogShader, Matrix4.Identity);
            FogShader.SetUniform("Time", (float)(DateTime.Now - GLThread.StartTime).TotalMilliseconds / 1000);
            LastFogTime = DrawPPMesh();
        }

        private void HDR(long time)
        {
            string lastTimeSTR = time.ToString();
            //Console.WriteLine(time);
            int[] nums = lastTimeSTR.ToCharArray().Select<char, int>((a) => a - 48).ToArray();
            HDRShader.Use();
            HDRShader.SetUniform("UseBloom", GLThread.GraphicsSettings.UseBloom);
            HDRShader.SetUniform("NumbersCount", nums.Length);
            HDRShader.SetUniform("ShowSelected", ShowSelected);
            HDRShader.SetUniformArray("Numbers", nums);
            HDRShader.SetUniform("UnbiasedIntegrateRenderMode", UnbiasedIntegrateRenderMode);
            HDRShader.SetUniform("InputFocalLength", Camera.Current.FocalLength);
            MRT.UseTextureDepth(1);
            MRT.UseTextureId(18);
            LastDeferredFramebuffer.UseTexture(20);
            if(Camera.MainDisplayCamera != null)
            {
                HDRShader.SetUniform("CameraCurrentDepth", Camera.MainDisplayCamera.CurrentDepthFocus);
                HDRShader.SetUniform("LensBlurAmount", Camera.MainDisplayCamera.LensBlurAmount);
            }
            MRT.UseTextureDepth(1);
            LastHDRTime = DrawPPMesh();
        }
        
        public void SetUniformsShared()
        {
            var shader = ShaderProgram.Current;
            shader.SetUniform("ViewMatrix", Camera.Current.ViewMatrix);
            shader.SetUniform("ProjectionMatrix", Camera.Current.ProjectionMatrix);

            shader.SetUniform("CameraPosition", Camera.Current.Transformation.GetPosition());
            shader.SetUniform("CameraDirection", Camera.Current.Transformation.GetOrientation().ToDirection());
            shader.SetUniform("CameraTangentUp", Camera.Current.Transformation.GetOrientation().GetTangent(MathExtensions.TangentDirection.Up));
            shader.SetUniform("CameraTangentLeft", Camera.Current.Transformation.GetOrientation().GetTangent(MathExtensions.TangentDirection.Left));
            shader.SetUniform("FarPlane", Camera.Current.Far);
            shader.SetUniform("resolution", new Vector2(Width, Height));
            shader.SetUniform("DisablePostEffects", DisablePostEffects);
            shader.SetUniform("Time", (float)(DateTime.Now - GLThread.StartTime).TotalMilliseconds / 1000);
        }

        private Framebuffer SwitchBetweenFB()
        {
            if(LastFrameBuffer == Pass1FrameBuffer)
                return SwitchToFB2();
            else
                return SwitchToFB1();
        }

        private void SwitchToFB(Framebuffer buffer)
        {
            buffer.Use();
            if((buffer == Pass1FrameBuffer || buffer == Pass2FrameBuffer) && LastFrameBuffer != null)
            {
                LastFrameBuffer.UseTexture(0);
                LastFrameBuffer = buffer;
            }
        }
        
        private Framebuffer SwitchToFB1()
        {
            Pass1FrameBuffer.Use();
            if(LastFrameBuffer != null)
                LastFrameBuffer.UseTexture(0);
            LastFrameBuffer = Pass1FrameBuffer;
            return Pass1FrameBuffer;
        }

        private Framebuffer SwitchToFB2()
        {
            Pass2FrameBuffer.Use();
            if(LastFrameBuffer != null)
                LastFrameBuffer.UseTexture(0);
            LastFrameBuffer = Pass2FrameBuffer;
            return Pass2FrameBuffer;
        }
    }
}