Shader "TK/PostFX/ColorScale"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
    
    CBUFFER_START(UnityPerMaterial)
    uniform TEXTURE2D_X(_MainTex);
    SAMPLER(sampler_MainTex);
    uniform TEXTURE2D_X(_LutTex2D);
    SAMPLER(sampler_LutTex2D);
    uniform TEXTURE3D(_LutTex3D);
    SAMPLER(sampler_LutTex3D);

    int _RefColMaskNum, _RefLutMask;
    float _Temp, _pAmount, _Rev;
    float4 _Color, _Gamma, _Lift, _Gain, _BaseColor;
    half _Hue, _Sat, _Val;
    uniform half _LutAmount;
    CBUFFER_END

    float3 rgb2hsv(float3 rgb)
    {
        float3 hsv;

        // RGBの三つの値で最大のもの
        float maxValue = max(rgb.r, max(rgb.g, rgb.b));
        // RGBの三つの値で最小のもの
        float minValue = min(rgb.r, min(rgb.g, rgb.b));
        // 最大値と最小値の差
        float delta = maxValue - minValue;

        hsv.z = maxValue;

        if (maxValue != 0.0)
        {
            hsv.y = delta / maxValue;
        }
        else
        {
            hsv.y = 0.0;
        }

        if (hsv.y > 0.0)
        {
            if (rgb.r == maxValue)
            {
                hsv.x = (rgb.g - rgb.b) / delta;
            }
            else if (rgb.g == maxValue)
            {
                hsv.x = 2 + (rgb.b - rgb.r) / delta;
            }
            else
            {
                hsv.x = 4 + (rgb.r - rgb.g) / delta;
            }
            hsv.x /= 6.0;
            if (hsv.x < 0)
            {
                hsv.x += 1.0;
            }
        }

        return hsv;
    }

    float3 hsv2rgb(float3 hsv)
    {
        float3 rgb;

        if (hsv.y == 0)
        {
            rgb.r = rgb.g = rgb.b = hsv.z;
        }
        else
        {
            hsv.x *= 6.0;
            float i = floor(hsv.x);
            float f = hsv.x - i;
            float aa = hsv.z * (1 - hsv.y);
            float bb = hsv.z * (1 - (hsv.y * f));
            float cc = hsv.z * (1 - (hsv.y * (1 - f)));
            if (i < 1)
            {
                rgb.r = hsv.z;
                rgb.g = cc;
                rgb.b = aa;
            }
            else if (i < 2)
            {
                rgb.r = bb;
                rgb.g = hsv.z;
                rgb.b = aa;
            }
            else if (i < 3)
            {
                rgb.r = aa;
                rgb.g = hsv.z;
                rgb.b = cc;
            }
            else if (i < 4)
            {
                rgb.r = aa;
                rgb.g = bb;
                rgb.b = hsv.z;
            }
            else if (i < 5)
            {
                rgb.r = cc;
                rgb.g = aa;
                rgb.b = hsv.z;
            }
            else {
                rgb.r = hsv.z;
                rgb.g = aa;
                rgb.b = bb;
            }
        }
        return rgb;
    }

    float3 shift_col(float3 rgb, half3 shift)
    {
        // RGB->HSV変換
        float3 hsv = rgb2hsv(rgb);

        // HSV操作
        hsv.x += shift.x;
        if (1.0 <= hsv.x)
        {
            hsv.x -= 1.0;
        }
        hsv.y *= shift.y;
        hsv.z *= shift.z;

        // HSV->RGB変換
        return hsv2rgb(hsv);
    }

    float3 ColorTemputure(float3 rgb)
    {
        float3 col = rgb;
        if (_Temp > 0)
        {
            col.b -= (rgb.b * _Temp);

        }
        else if (_Temp < 0)
        {
            col.b -= (rgb.b * _Temp);
        }
        return col;
    }

    half4 fragLut2D(Varyings input) : SV_Target
    {
        half4 c = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv);
        half b = floor(c.b * 256.0h);
        half by = floor(b * 0.0625h);
        half2 uv = c.rg * 0.05859375h + 0.001953125h + half2(floor(b - by * 16.0h), by) * 0.0625h;
        half4 lc = SAMPLE_TEXTURE2D_X(_LutTex2D, sampler_LutTex2D, uv);
        return lerp(c, lc, _LutAmount);
    }

    half4 fragLut3D(Varyings input) : SV_Target
    {
        half4 c = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv);
        half3 lc = SAMPLE_TEXTURE3D(_LutTex3D, sampler_LutTex3D, c.rgb * 0.9375h + 0.03125h).rgb;
        return lerp(c, half4(lc, 1.0h), _LutAmount);
    }

    half4 fragLut2DLin(Varyings input) : SV_Target
    {
        half4 c = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv);
        c.rgb = sqrt(c.rgb);
        half b = floor(c.b * 256.0h);
        half by = floor(b * 0.0625h);
        half2 uv = c.rg * 0.05859375h + 0.001953125h + half2(floor(b - by * 16.0h), by) * 0.0625h;
        half4 lc = SAMPLE_TEXTURE2D_X(_LutTex2D, sampler_LutTex2D, uv);
        c.rgb *= c.rgb;
        return lerp(c, lc, _LutAmount);
    }

    half4 fragLut3DLin(Varyings i) : SV_Target
    {
        half4 c = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, i.uv);
        c.rgb = sqrt(c.rgb);
        half4 lc = SAMPLE_TEXTURE3D(_LutTex3D, sampler_LutTex3D, c.rgb * 0.9375h + 0.03125h);
        c = lerp(c,lc, _LutAmount);
        c.rgb *= c.rgb;
        return c;
    }
    
    half4 FragColorScale(Varyings input) : SV_Target
    {
        half4 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp , input.uv);
        float x = abs(_Rev - input.uv.x);
        half4 cols = _Color * x * input.uv.y * (_pAmount / 10);
        half4 colm = _Color * (1 - x) * (1 - input.uv.y) * (_pAmount / 10);
        cols *= _pAmount / 5;
        colm *= _pAmount / 5;
        color += cols;
        color -= colm;
        color *= _Color;
        half3 shift = half3(_Hue, _Sat, _Val);
        half4 subcolor = half4(shift_col(color.rgb, shift), color.a);
        subcolor += ((_BaseColor - color) * _Lift);
        subcolor *= _Gain;
        subcolor = pow(subcolor, 1 / _Gamma);
        subcolor.rgb = ColorTemputure(subcolor.rgb);
        return subcolor;
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off
        
        // 0 ColorScale
        Pass
        {
             Stencil
            {
                Ref[_RefColMaskNum]
                Comp NotEqual
            }
            Name "ColorScale"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragColorScale
            ENDHLSL
        }
        // 1~ LUT
        Pass //1
        {
          ZTest Always Cull Off ZWrite Off
          Fog { Mode off }
          HLSLPROGRAM
          #pragma vertex Vert
          #pragma fragment fragLut2D
          #pragma fragmentoption ARB_precision_hint_fastest
          ENDHLSL
        }
        Pass //2
        {
          ZTest Always Cull Off ZWrite Off
          Fog { Mode off }
          HLSLPROGRAM
          #pragma vertex Vert
          #pragma fragment fragLut2DLin
          #pragma fragmentoption ARB_precision_hint_fastest
          ENDHLSL
        }
        Pass //3
        {
          ZTest Always Cull Off ZWrite Off
          Fog { Mode off }
          HLSLPROGRAM
          #pragma vertex Vert
          #pragma fragment fragLut3D
          #pragma fragmentoption ARB_precision_hint_fastest
          ENDHLSL
        }
        Pass //4
        {
          ZTest Always Cull Off ZWrite Off
          Fog { Mode off }
          HLSLPROGRAM
          #pragma vertex Vert
          #pragma fragment fragLut3DLin
          #pragma fragmentoption ARB_precision_hint_fastest
          ENDHLSL
        }
        
    }
}
