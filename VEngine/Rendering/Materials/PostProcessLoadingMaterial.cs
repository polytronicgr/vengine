﻿namespace VEngine
{
    public class PostProcessLoadingMaterial : IMaterial
    {
        public PostProcessLoadingMaterial()
        {
            Program = ShaderProgram.Compile("PostProcess.vertex.glsl", "Loading.fragment.glsl");
        }

        private ShaderProgram Program;

        public ShaderProgram GetShaderProgram()
        {
            return Program;
        }

        public bool Use()
        {
            bool res = Program.Use();
            Program.SetUniform("resolution", GLThread.Resolution);
            return res;
        }
    }
}