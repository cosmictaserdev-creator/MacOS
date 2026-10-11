param(
    [string]$Source = (Join-Path $PSScriptRoot '..\Shaders\LiquidGlass.hlsl'),
    [string]$Output = (Join-Path $PSScriptRoot '..\Shaders\LiquidGlass.ps')
)

$compiler = @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class HlslCompiler
{
    [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int D3DCompile(byte[] source, UIntPtr sourceLength, string sourceName,
        IntPtr defines, IntPtr include, string entryPoint, string target, uint flags1, uint flags2,
        out IntPtr bytecode, out IntPtr errors);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr GetBufferPointer(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate UIntPtr GetBufferSize(IntPtr self);

    private static byte[] ReadBlob(IntPtr blob)
    {
        IntPtr vtable = Marshal.ReadIntPtr(blob);
        var pointer = (GetBufferPointer)Marshal.GetDelegateForFunctionPointer(
            Marshal.ReadIntPtr(vtable, 3 * IntPtr.Size), typeof(GetBufferPointer));
        var length = (GetBufferSize)Marshal.GetDelegateForFunctionPointer(
            Marshal.ReadIntPtr(vtable, 4 * IntPtr.Size), typeof(GetBufferSize));
        int size = checked((int)length(blob).ToUInt64());
        byte[] bytes = new byte[size];
        Marshal.Copy(pointer(blob), bytes, 0, size);
        return bytes;
    }

    public static byte[] Compile(string text)
    {
        var source = Encoding.UTF8.GetBytes(text);
        IntPtr code = IntPtr.Zero;
        IntPtr errors = IntPtr.Zero;
        try
        {
            int hr = D3DCompile(source, (UIntPtr)source.Length, "LiquidGlass.hlsl", IntPtr.Zero,
                IntPtr.Zero, "main", "ps_3_0", 0, 0, out code, out errors);
            if (hr < 0)
            {
                var message = errors == IntPtr.Zero ? "Unknown shader compiler error" :
                    Encoding.UTF8.GetString(ReadBlob(errors));
                throw new COMException(message, hr);
            }
            return ReadBlob(code);
        }
        finally
        {
            if (code != IntPtr.Zero) Marshal.Release(code);
            if (errors != IntPtr.Zero) Marshal.Release(errors);
        }
    }
}
'@

Add-Type -TypeDefinition $compiler
$bytes = [HlslCompiler]::Compile([System.IO.File]::ReadAllText($Source))
[System.IO.File]::WriteAllBytes($Output, $bytes)
Write-Output "Compiled $($bytes.Length) bytes to $Output"
