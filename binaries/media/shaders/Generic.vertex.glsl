#version 430 core
#include AttributeLayout.glsl
#include Mesh3dUniforms.glsl

out Data {
    int instanceId;
    vec3 ModelPos;
    vec3 WorldPos;
    vec2 TexCoord;
    vec3 Normal;
    vec3 Tangent;
} Output;

#include Bones.glsl

void main(){

    vec4 v = vec4(in_position,1);

	Output.Tangent = in_tangent;
    
	Output.TexCoord = vec2(in_uv.x, -in_uv.y);
    
    vec3 inorm = in_normal;
	mat4 mmat = ModelMatrix;
    if(Instances > 0) mmat = ModelMatrixes[gl_InstanceID];

    vec3 mspace = v.xyz;
    if(UseBoneSystem == 1){
        int bone = determineBone(mspace);
        mspace = applyBoneRotationChain(mspace, bone);
        inorm = applyBoneRotationChainNormal(inorm, bone);
    }
    v = vec4(mspace, 1);
    Output.WorldPos = (InitialTransformation * mmat * v).xyz;	

	Output.Normal = inorm;
	
	Output.instanceId = gl_InstanceID;

	Output.ModelPos = v.xyz;	
	
    gl_Position = (ProjectionMatrix  * ViewMatrix) * vec4(Output.WorldPos, 1);
}