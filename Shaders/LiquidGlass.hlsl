sampler2D Backdrop : register(s0);
float4 Pixel : register(c0);

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = uv * 2.0 - 1.0;
    float edge = saturate((max(abs(p.x), abs(p.y)) - 0.56) / 0.44);
    float2 bend = p * (0.002 + 0.027 * edge * edge);
    float2 q = saturate(uv + bend);
    float2 s = Pixel.xy * 4.0;

    float3 blur = tex2D(Backdrop, q).rgb * 0.16;
    blur += tex2D(Backdrop, q + float2(s.x, 0)).rgb * 0.09;
    blur += tex2D(Backdrop, q - float2(s.x, 0)).rgb * 0.09;
    blur += tex2D(Backdrop, q + float2(0, s.y)).rgb * 0.09;
    blur += tex2D(Backdrop, q - float2(0, s.y)).rgb * 0.09;
    blur += tex2D(Backdrop, q + s).rgb * 0.07;
    blur += tex2D(Backdrop, q - s).rgb * 0.07;
    blur += tex2D(Backdrop, q + float2(s.x, -s.y)).rgb * 0.07;
    blur += tex2D(Backdrop, q + float2(-s.x, s.y)).rgb * 0.07;
    blur += tex2D(Backdrop, q + float2(2 * s.x, 0)).rgb * 0.05;
    blur += tex2D(Backdrop, q - float2(2 * s.x, 0)).rgb * 0.05;
    blur += tex2D(Backdrop, q + float2(0, 2 * s.y)).rgb * 0.05;
    blur += tex2D(Backdrop, q - float2(0, 2 * s.y)).rgb * 0.05;

    float red = tex2D(Backdrop, saturate(q + bend * 0.38)).r;
    float blue = tex2D(Backdrop, saturate(q - bend * 0.38)).b;
    float3 refracted = float3(lerp(blur.r, red, edge * 0.58), blur.g,
                               lerp(blur.b, blue, edge * 0.58));
    float3 tinted = lerp(refracted, float3(0.72, 0.81, 0.94), 0.13);
    float rim = pow(edge, 4.0) * 0.18;
    float sheen = pow(saturate(1.0 - abs(uv.x - 0.18) * 2.1), 9.0) *
                  pow(saturate(1.0 - uv.y), 2.0) * 0.19;
    return float4(saturate(tinted + rim + sheen), 0.83);
}
