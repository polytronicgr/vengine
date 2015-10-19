#version 430 core
layout(invocations = 24) in;
layout(triangles) in;
layout(triangle_strip, max_vertices = 24) out;

#include Mesh3dUniforms.glsl

in Data {
    int instanceId;
    vec3 WorldPos;
    vec2 TexCoord;
    vec3 Normal;
    vec3 Tangent;
    vec3 Data;
} gs_in[];

out Data {
    int instanceId;
    vec3 WorldPos;
    vec2 TexCoord;
    vec3 Normal;
    vec3 Tangent;
    vec3 Data;
} Output;
uniform int MaterialType;


#define MaterialTypeSolid 0
#define MaterialTypeRandomlyDisplaced 1
#define MaterialTypeWater 2
#define MaterialTypeSky 3
#define MaterialTypeWetDrops 4
#define MaterialTypeGrass 5
#define MaterialTypePlanetSurface 6
#define MaterialTypeTessellatedTerrain 7
#define MaterialTypeFlag 8

#define MaterialTypePlastic 9
#define MaterialTypeMetal 10

#define MaterialTypeParallax 11

#include noise4D.glsl

// input 3 vertices
// output 3 to 32 vertices
vec2 interpolate2D(vec3 interpolator, vec2 v0, vec2 v1, vec2 v2)
{
   	return vec2(interpolator.x) * v0 + vec2(interpolator.y) * v1 + vec2(interpolator.z) * v2;
}

vec3 interpolate3D(vec3 interpolator, vec3 v0, vec3 v1, vec3 v2)
{
   	return vec3(interpolator.x) * v0 + vec3(interpolator.y )* v1 + vec3(interpolator.z) * v2;
}
float rand(vec2 co){
    return fract(sin(dot(co.xy ,vec2(12.9898,78.233))) * 43758.5453);
}
uniform int UseBumpMap;
layout(binding = 29) uniform sampler2D bumpMap;

mat4 PV = (ProjectionMatrix  * ViewMatrix);
    
struct Vertex {
    vec3 Position;
    vec2 TexCoord;
};

struct Triangle {
    Vertex Vertices[3];
};
struct Plane {
    Vertex Vertices[4];
};

void EmitTriangle(Triangle triangle){
    vec3 normal = normalize(cross(triangle.Vertices[1].Position - triangle.Vertices[0].Position, triangle.Vertices[2].Position - triangle.Vertices[0].Position));
    vec3 tangent = normalize(triangle.Vertices[1].Position - triangle.Vertices[0].Position);
    for(int l=0;l<3;l++){
        Output.instanceId = gs_in[0].instanceId;
        Output.WorldPos = triangle.Vertices[l].Position;
        Output.TexCoord = triangle.Vertices[l].TexCoord;
        Output.Normal = normal;
        Output.Tangent = tangent;
        gl_Position = PV * vec4(triangle.Vertices[l].Position, 1);
        EmitVertex();
    }
    EndPrimitive(); 
}

void EmitPlane(vec3 normal, Plane plane){
   // vec3 normal = normalize(cross(plane.Vertices[1].Position - plane.Vertices[0].Position, plane.Vertices[2].Position - plane.Vertices[0].Position));
    vec3 tangent = normalize(plane.Vertices[1].Position - plane.Vertices[0].Position);
    for(int l=0;l<4;l++){
        Output.instanceId = gs_in[0].instanceId;
        Output.WorldPos = plane.Vertices[l].Position;
        Output.TexCoord = plane.Vertices[l].TexCoord;
        Output.Normal = normal;
        Output.Tangent = tangent;
        gl_Position = PV * vec4(plane.Vertices[l].Position, 1);
        EmitVertex();
    }
    EndPrimitive(); 
}

void GenerateGrass(vec3 start, vec3 normal, vec3 direction, vec3 tangent, float uplength, float width, float bending, float wind, float seed){
    vec3 rvec2 = (vec3(rand(gs_in[2].TexCoord+seed), rand(gs_in[1].TexCoord+seed), rand(gs_in[0].TexCoord+seed)) * 2 - 1) * 0.5;
    float nois1 = snoise(vec4(start/12, Time*0.6))*wind;
    float nois2 = snoise(vec4(start/8+12, Time*0.6))*wind;
    vec3 bidir = cross(tangent, normal);
    vec3 end = start + (uplength) * mix(normal, direction, bending) + (wind)*(1.0*tangent * nois1 + bidir * nois2);
    vec3 endStraight = start + uplength * normal;
    vec3 left = -cross(CameraPosition - start, end - start);
    vec3 initial[] = vec3[](
        start - normalize(left)*width,
        start + normalize(left)*width,
        end
    );
    vec3 lastVertex1 = initial[0];
    vec3 lastVertex2 = initial[1];
    for(int i=0;i<4;i++){
        float mixfactor = float(i + 1.0) /4.0;
        vec3 emix = mix(endStraight, end, pow(mixfactor, 0.7));
        vec3 pmix1 = mix(initial[0], emix, mixfactor);
        vec3 pmix2 = mix(initial[1], emix, mixfactor);
        EmitPlane(rvec2, 
            Plane(Vertex[4](
                Vertex(lastVertex1, vec2(0, mixfactor)),
                Vertex(lastVertex2, vec2(1, mixfactor)),
                Vertex(pmix1, vec2(0, mixfactor)),
                Vertex(pmix2, vec2(1, mixfactor)))));
        lastVertex1 = pmix1;
        lastVertex2 = pmix2;
    }
}

#define GEO_GRASS_INSTANCES 12

void GeometryGenerateGrass(){
    if(gl_InvocationID > GEO_GRASS_INSTANCES) return;
    vec3 center = vec3(gs_in[0].WorldPos + gs_in[1].WorldPos + gs_in[2].WorldPos) / 3.0;
    //float dist = distance(center, CameraPosition);
    //if(dist > 350)return;
    //dist = dist+1;
    vec3 startNormal = normalize(gs_in[0].Normal);
    float dt = pow(max(0, dot(normalize(CameraPosition - center), startNormal)), 1.2);
    //dist = clamp((2.0/dist*27.0) * dt, 0, 2);
    //int cnt = int(dist);
    //if(gl_InvocationID > cnt) return;
    vec2 centertx = vec2(gs_in[0].TexCoord + gs_in[1].TexCoord + gs_in[2].TexCoord) / 3.0;
    vec3 startTangent = normalize(gs_in[0].Tangent);
    vec3 startBitangent = normalize(cross(startNormal, startTangent));
    for(int i=0;i<6*dt;i++){
        float rd = ((gl_InvocationID+1)*12.123436 * (i+1));
        vec3 rvec = vec3(rand(gs_in[1].TexCoord+rd), rand(gs_in[0].TexCoord+rd), rand(gs_in[2].TexCoord+rd)) * 2 - 1;
        rd = ((gl_InvocationID+1)*122.343436 * (i+1));
        vec3 rvec2 = (vec3(rand(gs_in[2].TexCoord+rd), rand(gs_in[1].TexCoord+rd), rand(gs_in[0].TexCoord+rd)) * 2 - 1) * 0.5;
        vec3 startPoint = center + interpolate3D(rvec, gs_in[0].WorldPos - center, gs_in[1].WorldPos - center, gs_in[2].WorldPos - center);
        vec2 startPointTx = centertx + interpolate2D(rvec, gs_in[0].TexCoord - centertx, gs_in[1].TexCoord - centertx, gs_in[2].TexCoord - centertx);
        float height = 45.8;//texture(bumpMap, startPointTx).r*45.8;
        if(height <= 0) continue;
        //float height = 1.9;
        float width = rand(gs_in[2].TexCoord.yx-rd)*0.127 + 0.111; 
        
        GenerateGrass(startPoint, startNormal, normalize(rvec), startTangent, height, width, 1.0, height*0.03, rd);
    }
}

#define GEO_FLAG_XDIVISION 95
#define GEO_FLAG_YDIVISION 23

void GeometryProcessFlagDynamics(){
    if(gl_InvocationID > GEO_FLAG_XDIVISION) return;
    float xmix1 = float(gl_InvocationID) / GEO_FLAG_XDIVISION;
    float xmix2 = float(gl_InvocationID + 1) / GEO_FLAG_XDIVISION;
    vec3 v1 = gs_in[0].WorldPos;
    vec3 v2 = gs_in[1].WorldPos;
    vec3 v3 = gs_in[2].WorldPos;
    vec3 t1 = v2 - v1;
    vec3 t2 = v3 - v1;
    vec3 v4 = v1 + t1 + t2;
    vec3 noiseModifier = vec3(3, 0.5, 3);
    float timeModifier = 0.3;
    vec3 n1 = normalize(cross(t1, t2));
    for(int i = 0; i < GEO_FLAG_YDIVISION; i++) {
        float ymix1 = float(i) / GEO_FLAG_YDIVISION;
        float ymix2 = float(i + 1) / GEO_FLAG_YDIVISION;
        vec3 p1 = v1 + t1 * xmix1 + t2 * ymix1;
        vec3 p2 = v1 + t1 * xmix1 + t2 * ymix2;
        vec3 p3 = v1 + t1 * xmix2 + t2 * ymix1;
        vec3 p4 = v1 + t1 * xmix2 + t2 * ymix2;
        p1 += n1 * snoise(vec4(p1 * noiseModifier, Time * timeModifier)) * (1.0 - ymix1);
        p2 += n1 * snoise(vec4(p2 * noiseModifier, Time * timeModifier)) * (1.0 - ymix2);
        p3 += n1 * snoise(vec4(p3 * noiseModifier, Time * timeModifier)) * (1.0 - ymix1);
        p4 += n1 * snoise(vec4(p4 * noiseModifier, Time * timeModifier)) * (1.0 - ymix2);
        vec3 n2 = normalize(cross(p2 - p1, p3 - p1));
        EmitPlane(n2, 
            Plane(Vertex[4](
                Vertex(p1, vec2(xmix1, 1.0 - ymix1)),
                Vertex(p2, vec2(xmix1, 1.0 - ymix2)),
                Vertex(p3, vec2(xmix2, 1.0 - ymix1)),
                Vertex(p4, vec2(xmix2, 1.0 - ymix2)))));
    }
}

void FixNormals(){
    if(gl_InvocationID > 1) return;
    vec3 n = normalize(cross(gs_in[1].WorldPos - gs_in[0].WorldPos, gs_in[2].WorldPos - gs_in[0].WorldPos));
    for(int i=0;i<3;i++){
        Output.instanceId = gs_in[0].instanceId;
        Output.WorldPos = gs_in[i].WorldPos;
        Output.TexCoord = gs_in[i].TexCoord;
        Output.Normal = n;
        Output.Tangent = gs_in[i].Tangent;
        gl_Position = PV * vec4(gs_in[i].WorldPos, 1);
        EmitVertex();
    }
    EndPrimitive(); 
}

uniform float ParallaxHeightMultiplier;
uniform int ParallaxInstances;
void GeometryProcessParallaxDraw(){
    float inter = 0.0;
    float v1 = distance(CameraPosition, gs_in[0].WorldPos);
    float v2 = distance(CameraPosition, gs_in[1].WorldPos);
    float v3 = distance(CameraPosition, gs_in[2].WorldPos);
    float prx = floor(mix(4.0, 1.0, clamp(min(min(v1, v2), v3) / 6.0, 0.0, 1.0)));
    int iprx = int(prx);
    if(gl_InvocationID > ParallaxInstances) return;

     
    float stepsize = 1.0 / float(ParallaxInstances);
    float midstep = stepsize / prx;
    float midstep2 = midstep / 3.0;
    inter =  float(gl_InvocationID+1) / float(ParallaxInstances);
    for(int i=0;i<iprx;i++){
        for(int l=0;l<3;l++){
            Output.instanceId = gs_in[l].instanceId;
            Output.WorldPos = gs_in[l].WorldPos - gs_in[l].Normal * inter * 0.11 * ParallaxHeightMultiplier;
            Output.TexCoord =  gs_in[l].TexCoord;
            Output.Normal =  gs_in[l].Normal;
            Output.Tangent =  gs_in[l].Tangent;
            Output.Data = vec3(inter);
            gl_Position = PV * vec4(Output.WorldPos, 1);
            EmitVertex();
            inter += midstep2;
        }
        //inter += midstep;
    }  
    EndPrimitive(); 
}

void main(){

    if(MaterialType == MaterialTypeGrass) GeometryGenerateGrass();
    if(MaterialType == MaterialTypeFlag) GeometryProcessFlagDynamics();
    if(MaterialType == MaterialTypeTessellatedTerrain) FixNormals();
    if(MaterialType == MaterialTypeParallax) GeometryProcessParallaxDraw();

}