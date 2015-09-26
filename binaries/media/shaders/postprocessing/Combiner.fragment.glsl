#version 430 core

in vec2 UV;
#include LogDepth.glsl
#include UsefulIncludes.glsl
#include_once LightingSamplers.glsl
#include FXAA.glsl

#define mPI (3.14159265)
#define mPI2 (2.0*3.14159265)
#define GOLDEN_RATIO (1.6180339)
out vec4 outColor;


float centerDepth;
uniform float Brightness;

float rand(vec2 co){
    return fract(sin(dot(co.xy ,vec2(12.9898,78.233))) * 43758.5453);
}

vec3 lookupFog(vec2 fuv){
    vec3 outc = vec3(0);
    int counter = 0;
    float depthCenter = texture(depthTex, fuv).r;
    for(float g = 0; g < mPI2 * 2; g+=GOLDEN_RATIO)
    {
        for(float g2 = 0; g2 < 6.0; g2+=1.0)
        {
            vec2 gauss = vec2(sin(g + g2)*ratio, cos(g + g2)) * (g2 * 0.01);
            vec3 color = texture(fogTex, fuv + gauss).rgb;
            float depthThere = texture(fogTex, fuv + gauss).a;
            if(abs(depthThere - depthCenter) < 0.01){
                outc += color;
                counter++;
            }
        }
    }
    return counter == 0 ? texture(fogTex, fuv).rgb : outc / counter;
}
vec3 blurByUV(sampler2D sampler, vec2 fuv, float force){
    vec3 outc = vec3(0);
    int counter = 0;
    for(float g = 0; g < mPI2; g+=GOLDEN_RATIO)
    {
        for(float g2 = 0; g2 < 3.0; g2+=1.0)
        {
            vec2 gauss = vec2(sin(g + g2)*ratio, cos(g + g2)) * (g2 * 0.001 * force);
            vec3 color = texture(sampler, fuv + gauss).rgb;
            outc += color;
            counter++;
        }
    }
    return outc / counter;
}
vec3 blurByUV2(sampler2D sampler, vec2 fuv, float force){
    vec3 outc = vec3(0);
    float counter = 0;
    float depthCenter = texture(depthTex, fuv).r;
    for(float g = 0; g < mPI2; g+=0.3)
    {
        for(float g2 = 0.02; g2 < 1.0; g2+=0.02)
        {
            vec2 gauss = vec2(sin(g + g2)*ratio, cos(g + g2)) * (g2 * 0.01 * force);
            vec3 color = clamp(texture(sampler, fuv + gauss).rgb, 0.02, 1.0);
            float depthThere = texture(depthTex, fuv + gauss).r;
           // if(abs(depthThere - depthCenter) < 0.001){
                outc += color*length(color);
                counter+=length(color);
           // }
        }
    }
    return clamp(outc / counter, 0.02, 1.0);
}
vec3 blurssao(sampler2D sampler, vec2 fuv, float force){
    float roughs = texture(meshDataTex, fuv).a;
    vec3 outc = vec3(0);
    float counter = 0;
    vec3 norm = texture(normalsTex, fuv).xyz;
    float depthCenter = texture(depthTex, fuv).r;
    for(float g = 0; g < mPI2; g+=0.1)
    {
        for(float g2 = 0.0002; g2 < 1.0; g2+=0.05)
        {
            vec2 gauss = vec2(sin(g + g2)*ratio, cos(g + g2)) * (g2 * force)*0.004;
            vec3 color = texture(sampler, fuv + gauss).rgb;
            float depthd = texture(sampler, fuv + gauss).a;
            if(abs(depthCenter-depthd)<0.005){
                outc += color;
                counter+=1;
            }
        }
    }
    vec3 a = texture(sampler, fuv).rgb;
    if(counter == 0) return a;
    return outc/counter;
}

vec3 mixAlbedo(vec3 a){
    return mix(a*texture(diffuseColorTex, UV).rgb, a, texture(meshDataTex, UV).z);
}



uniform int UseFog;
uniform int UseLightPoints;
uniform int UseDepth;
uniform int UseDeferred;
uniform int UseHBAO;
uniform int UseVDAO;
uniform int UseRSM;

vec3 emulateSkyWithDepth(vec2 uv){
    vec3 worldPos = (texture(worldPosTex, uv).rgb);
    float depth = length(worldPos)*0.001;
    worldPos = FromCameraSpace(worldPos);
    depth = depth * clamp(1.0 / (abs(worldPos.y) * 0.0001), 0.0, 1.0);
    return vec3(1) * depth;
}

vec3 ball(vec3 colour, float sizec, float xc, float yc){
	float xdist = (abs(UV.x - xc));
	float ydist = (abs(UV.y - yc)) * ratio;

	float d = sizec / length(vec2(xdist, ydist));
	return colour * (d);
}

vec3 lightPoints(){
    vec3 color = vec3(0);
	for(int i=0;i<LightsCount;i++){

		mat4 lightPV = (LightsPs[i] * LightsVs[i]);

		vec4 clipspace = (ProjectionMatrix * ViewMatrix) * vec4((LightsPos[i]), 1.0);
		vec2 sspace1 = ((clipspace.xyz / clipspace.w).xy + 1.0) / 2.0;
		if(clipspace.z < 0.0) continue;

        float badass_depth = distance(LightsPos[i], CameraPosition);
        float logg = length(texture(worldPosTex, sspace1).rgb);
        float mixv = 1.0 - smoothstep(0.1, 2.5, distance(sspace1*resolution.xy * 0.01, UV*resolution.xy * 0.009));

        if(logg > badass_depth) {
            color += ball(vec3(LightsColors[i].rgb*1.0),LightPointSize / ( badass_depth) * 0.1, sspace1.x, sspace1.y);
            //color += ball(vec3(LightsColors[i]*2.0 * overall),12.0 / dist, sspace1.x, sspace1.y) * 0.03f;
        }

	}

    return color;
}



void main()
{
    vec2 nUV = UV;
    vec3 color1 = vec3(0);
    if(UseDeferred == 1) color1 += texture(currentTex, nUV).rgb;
    
    if(UseRSM == 1 && UseHBAO == 1){
        color1 += mixAlbedo(texture(indirectTex, nUV).rgb) * texture(HBAOTex, nUV).r;
    } else if(UseRSM == 1 && UseHBAO == 0){
        color1 += mixAlbedo(texture(indirectTex, nUV).rgb);
    } else if(UseRSM == 0 && UseHBAO == 1){
        color1 += texture(HBAOTex, nUV).rrr;
    }
    
    color1 += lightPoints();
    if(UseFog == 1) color1 += lookupFog(nUV) * FogContribution;

    if(UseDepth == 1) color1 += emulateSkyWithDepth(nUV);

    centerDepth = texture(depthTex, UV).r;

    gl_FragDepth = centerDepth;

    vec3 gamma = vec3(1.0/2.2, 1.0/2.2, 1.0/2.2) / Brightness;
    color1.rgb = vec3(pow(color1.r, gamma.r),
    pow(color1.g, gamma.g),
    pow(color1.b, gamma.b));
    //float Y = dot(vec3(0.30, 0.59, 0.11), color1);
    //float YD = Brightness * (Brightness + 1.0) / (Brightness + 1.0);
    //color1 *= YD * Y;
    outColor = vec4(clamp(color1, 0.0, 1.0), texture(depthTex, nUV).r);
}
