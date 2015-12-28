﻿using OpenTK;

namespace VEngine
{
    public class SimplePointLight : ITransformable, ILight
    {
        public Vector4 Color;
        public TransformationManager Transformation;

        public SimplePointLight(Vector3 position, Vector4 color)
        {
            Transformation = new TransformationManager(position);
            Color = color;
        }

        public Vector4 GetColor()
        {
            return Color;
        }

        public Vector3 GetPosition()
        {
            return Transformation.Position;
        }

        public TransformationManager GetTransformationManager()
        {
            return Transformation;
        }
    }
}