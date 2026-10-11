sampler2D Backdrop : register(s0);
float4 Pixel : register(c0);

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = uv * 2.0 - 1.0;
    float edge = saturate((max(abs(p.x), abs(p.y)) - 0.64) / 0.36);
    float2 bend = p * (0.003 + 0.017 * edge * edge);
    float2 q = saturate(uv + bend);
    float2 s = Pixel.xy * 3.2;

    float3 blur = tex2D(Backdrop, q).rgb * 0.22;
    blur += tex2D(Backdrop, q + float2(s.x, 0)).rgb * 0.11;
    blur += tex2D(Backdrop, q - float2(s.x, 0)).rgb * 0.11;
    blur += tex2D(Backdrop, q + float2(0, s.y)).rgb * 0.11;
    blur += tex2D(Backdrop, q - float2(0, s.y)).rgb * 0.11;
    blur += tex2D(Backdrop, q + s).rgb * 0.085;
    blur += tex2D(Backdrop, q - s).rgb * 0.085;
    blur += tex2D(Backdrop, q + float2(s.x, -s.y)).rgb * 0.085;
    blur += tex2D(Backdrop, q + float2(-s.x, s.y)).rgb * 0.085;

    float red = tex2D(Backdrop, saturate(q + bend * 0.38)).r;
    float blue = tex2D(Backdrop, saturate(q - bend * 0.38)).b;
    float3 refracted = float3(lerp(blur.r, red, edge * 0.6), blur.g,
                               lerp(blur.b, blue, edge * 0.6));
    float3 tinted = lerp(refracted, float3(0.70, 0.78, 0.88), 0.18);
    float rim = pow(edge, 3.0) * 0.15;
    float sheen = pow(saturate(1.0 - abs(uv.x - 0.12) * 2.2), 8.0) *
                  pow(saturate(1.0 - uv.y), 2.0) * 0.14;
    return float4(saturate(tinted + rim + sheen), 0.90);
}
