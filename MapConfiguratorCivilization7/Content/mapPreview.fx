
struct VertexShaderInput
{
    float4 Position : SV_POSITION;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
};

const static float4 COLOR_HEX_BORDER = float4(0, 0, 0, 1);
const static float4 COLOR_MAP_BORDER = float4(1, 0, 0, 1);

SamplerState mapSampler;
SamplerState iconSampler;
Texture2D mapTiles, mapDebug, icons;
float3 biomeColors[8];

matrix WorldProjection;
int screenWidth, screenHeight;
int mapSizeX, mapSizeY;

float2 pos;
float zoom;

bool renderDebug, wrapMap, showMapBorder, showHexBorder;
float hexBorderSize;
int superSamplingCount;

const static int MAX_SIZE_X = 512;
const static int MAX_SIZE_Y = 320;
const static float SQRT3HALF = sqrt(3.0f) / 2.0f;

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput) 0;

    output.Position = mul(input.Position, WorldProjection);

    return output;
}

float sdHex(float2 p, float r)
{
    p = abs(p);
    return max(dot(p, float2(0.8660254f, 0.5f)), p.x) - r;
}

int2 axial_to_oddr(int2 hex)
{
    int col = hex.x + (hex.y - (hex.y % 2)) / 2;
    int row = hex.y;
    return int2(col, row);
}

// From https://observablehq.com/@jrus/hexround
int2 axial_round(float2 hex)
{
    float x_grid = round(hex.x);
    float y_grid = round(hex.y);

    float x_diff = hex.x - x_grid;
    float y_diff = hex.y - y_grid;

    float dx = round(x_diff + 0.5 * y_diff) * step(y_diff * y_diff, x_diff * x_diff);
    float dy = round(y_diff + 0.5 * x_diff) * (1.0 - step(y_diff * y_diff, x_diff * x_diff));

    return int2(int(x_grid + dx), int(y_grid + dy));
}

float hexEdgeDist(float2 pixel)
{
    
    float hex_radius = 1.0f / 2.0;
    float q = (1.7320508f / 3.0 * pixel.x - 1.0 / 3.0 * pixel.y) / hex_radius;
    float r = (2.0 / 3.0 * pixel.y) / hex_radius;
    float2 hex = float2(q, r);
    
    float x_grid = round(hex.x);
    float y_grid = round(hex.y);

    float x_diff = hex.x - x_grid;
    float y_diff = hex.y - y_grid;

    float dx = round(x_diff + 0.5 * y_diff) * step(y_diff * y_diff, x_diff * x_diff);
    float dy = round(y_diff + 0.5 * x_diff) * (1.0 - step(y_diff * y_diff, x_diff * x_diff));
    
    float a = min(x_diff, y_diff);

    return a;
}

int2 pixel_to_hex(float2 pixel)
{
    float hex_radius = 1.0f / 2.0;
    float q = (1.7320508f / 3.0 * pixel.x - 1.0 / 3.0 * pixel.y) / hex_radius;
    float r = (2.0 / 3.0 * pixel.y) / hex_radius;
    return axial_round(float2(q, r));
}

float modI(float a, float b)
{
    float m = a - floor((a + 0.5f) / b) * b;
    return floor(m + 0.5f);
}

int extractBits(float f, float shift, float mask)
{
    return modI(floor((f + 0.5f) / pow(2.0, shift)), mask + 1.0f);
}

float4 renderMap(float2 pixelPos)
{
    float2 mapPosCur = pixelPos / zoom - pos;
    int2 mapPosCurHex = axial_to_oddr(pixel_to_hex(mapPosCur));
    int2 mapPosCurHexAfterWrap = mapPosCurHex;
    if (wrapMap)
    {
        mapPosCurHexAfterWrap.x = modI(mapPosCurHexAfterWrap.x, mapSizeX);
        if (mapPosCurHexAfterWrap.x < 0)
            mapPosCurHexAfterWrap.x += mapSizeX;
    }
    
    if (mapPosCurHexAfterWrap.x < 0 || mapPosCurHexAfterWrap.y < 0 || mapPosCurHexAfterWrap.x >= mapSizeX || mapPosCurHexAfterWrap.y >= mapSizeY)
        return float4(0.1f, 0.1f, 0.1f, 1);
    
    
    float2 hexCenter = float2(mapPosCurHex.x + (mapPosCurHex.y % 2) * 0.5f, mapPosCurHex.y * SQRT3HALF) * SQRT3HALF;
    float2 hexCenterPixel = (hexCenter + pos) * zoom;
    float2 local = (pixelPos - hexCenterPixel) / zoom;
    local *= (2.0f / SQRT3HALF);
    
    
    float2 texSize = float2(MAX_SIZE_X, MAX_SIZE_Y);
    float2 uv = (mapPosCurHexAfterWrap + 0.5f) / texSize;
    int tile = (int) mapTiles.Sample(mapSampler, uv).r;
    
    // Biome
    int type = extractBits(tile, 0, 0x3); // 2 bits
    int biome = extractBits(tile, 2, 0x7); // 3 bits
    int feature = extractBits(tile, 5, 0x3); // 2 bits
    int morph = extractBits(tile, 7, 0x3); // 2 bits
    int floodPlain = extractBits(tile, 9, 1) != 0; // 1 bit
    int wet = extractBits(tile, 10, 1) != 0; // 1 bit
    int vegetated = extractBits(tile, 11, 1) != 0; // 1 bit
    int snow = extractBits(tile, 12, 1) != 0; // 1 bit
    int reef = extractBits(tile, 13, 1) != 0; // 1 bit
    int isDist = extractBits(tile, 14, 1) != 0; // 1 bit
    
    float3 color = biomeColors[biome];
    
    // Icons
    float iconIndex = -1;
    if (vegetated == 1)
        iconIndex = 4;
    if (snow == 1)
        iconIndex = 3;
    if (feature == 3)
        iconIndex = 2;
    if (morph == 2)
        iconIndex = 0;
    if (feature == 1)
        iconIndex = 1;
    if (iconIndex >= 0)
    {
        float4 iconColor = icons.Sample(iconSampler, float2((iconIndex + (0.5f + local.x * 0.5f)) * 0.2f, 0.5f + -local.y * 0.5f)).rgba;
        color = lerp(color, iconColor.rgb, iconColor.a);
    }
    
    // Debug
    if (renderDebug == true)
    {
        float debugValue = mapDebug.Sample(mapSampler, uv).r;
        if (debugValue >= 0)
            color = float3(debugValue, 0, 0);
        else
            color = float3(0, 0, -debugValue);
    }
    
    if (showHexBorder == true || showMapBorder == true)
    {
        
        float edgeDot1 = dot(local, float2(1, 0));
        float edgeDot2 = dot(local, float2(0.5f, SQRT3HALF));
        float edgeDot3 = dot(local, float2(0.5f, -SQRT3HALF));
        
        float edgeDist1 = 1.0 - abs(edgeDot1);
        float edgeDist2 = 1.0 - abs(edgeDot2);
        float edgeDist3 = 1.0 - abs(edgeDot3);
        
        bool atMapBorder1 = (mapPosCurHexAfterWrap.x == 0 && edgeDot1 < 0) || (mapPosCurHexAfterWrap.x == mapSizeX - 1 && edgeDot1 > 0);
        bool atMapBorder2 = (mapPosCurHexAfterWrap.x == mapSizeX - 1 && mapPosCurHexAfterWrap.y % 2 == 1 && edgeDot2 > 0);
        atMapBorder2 = atMapBorder2 || (mapPosCurHexAfterWrap.x == 0 && mapPosCurHexAfterWrap.y % 2 == 0 && edgeDot2 < 0);
        atMapBorder2 = atMapBorder2 || (mapPosCurHexAfterWrap.y == 0 && edgeDot2 < 0);
        atMapBorder2 = atMapBorder2 || (mapPosCurHexAfterWrap.y == mapSizeY - 1 && edgeDot2 > 0);
        bool atMapBorder3 = (mapPosCurHexAfterWrap.x == mapSizeX - 1 && mapPosCurHexAfterWrap.y % 2 == 1 && edgeDot3 > 0);
        atMapBorder3 = atMapBorder3 || (mapPosCurHexAfterWrap.x == 0 && mapPosCurHexAfterWrap.y % 2 == 0 && edgeDot3 < 0);
        atMapBorder3 = atMapBorder3 || (mapPosCurHexAfterWrap.y == 0 && edgeDot3 > 0);
        atMapBorder3 = atMapBorder3 || (mapPosCurHexAfterWrap.y == mapSizeY - 1 && edgeDot3 < 0);
        
        int borderState = 0;
        if (edgeDist1 < hexBorderSize)
            borderState = max(borderState, 1 + (showMapBorder ? atMapBorder1 : 0));
        if (edgeDist2 < hexBorderSize)
            borderState = max(borderState, 1 + (showMapBorder ? atMapBorder2 : 0));
        if (edgeDist3 < hexBorderSize)
            borderState = max(borderState, 1 + (showMapBorder ? atMapBorder3 : 0));
        
        if (borderState == 2 && showMapBorder)
            return COLOR_MAP_BORDER;
        if (borderState == 1 && showHexBorder)
            return COLOR_HEX_BORDER;

    }
    
    return float4(color, 1);
}

float4 MainPS(VertexShaderOutput input) : SV_Target
{
    float2 pixelPos = input.Position.xy;
    float4 finalCol = float4(0, 0, 0, 0);
    float distBetweenSamples = 1.0f / superSamplingCount;
    [loop]
    for (int x = 0; x < superSamplingCount; ++x)
    {
        [loop]
        for (int y = 0; y < superSamplingCount; ++y)
        {
            finalCol += renderMap(pixelPos + distBetweenSamples * 0.5f + float2(x * distBetweenSamples, y * distBetweenSamples));
        }
    }
    
    return finalCol / (float) (superSamplingCount * superSamplingCount);

}

technique SpriteDrawing
{
	pass P0
	{
        VertexShader = compile vs_3_0 MainVS();
		PixelShader = compile ps_3_0 MainPS();
	}
};
