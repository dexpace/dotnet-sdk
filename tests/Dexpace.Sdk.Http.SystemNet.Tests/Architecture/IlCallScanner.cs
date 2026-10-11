// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using System.Reflection.Emit;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Architecture;

/// <summary>One call site found in a method body: the method that contains it and the method it calls.</summary>
/// <param name="CallerMethod">The method or constructor whose IL holds the call.</param>
/// <param name="Callee">The method the <c>call</c>, <c>callvirt</c>, <c>newobj</c>, <c>ldftn</c> or <c>ldvirtftn</c> targets.</param>
internal sealed record IlCall(MethodBase CallerMethod, MethodBase Callee)
{
    /// <summary>The type that declares <see cref="CallerMethod"/>; a compiler-generated state machine for an async method.</summary>
    public Type Caller => CallerMethod.DeclaringType!;

    /// <summary>The call as <c>Type.Method -&gt; Type.Callee</c>, for an assertion message.</summary>
    public override string ToString() =>
        $"{Caller.FullName}.{CallerMethod.Name} -> {Callee.DeclaringType?.FullName}.{Callee.Name}";
}

/// <summary>
/// A test-only IL walker (phase 8b, CFG-28 and TRANSPORT-9 architecture pins): it lists every call site in every method
/// and constructor body of an assembly's types, so a test can assert that a banned or unwanted callee has no caller. It
/// reads <c>MethodBody.GetILAsByteArray()</c>, decodes opcodes from a table built once from <see cref="OpCodes"/> and
/// resolves a callee token with <c>Module.ResolveMethod</c>. Reflection lives here and never in <c>src/</c>.
/// </summary>
internal static class IlCallScanner
{
    private const BindingFlags AllMembers =
        BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly OpCode[] s_oneByte = new OpCode[256];
    private static readonly OpCode[] s_twoByte = new OpCode[256];

    static IlCallScanner()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
            {
                continue;
            }

            var value = (ushort)opCode.Value;
            if (opCode.Size == 1)
            {
                s_oneByte[value & 0xFF] = opCode;
            }
            else
            {
                s_twoByte[value & 0xFF] = opCode;
            }
        }
    }

    /// <summary>Every call site in every type of <paramref name="assembly"/> (nested and compiler-generated types included).</summary>
    /// <param name="assembly">The assembly to scan.</param>
    public static IReadOnlyList<IlCall> FindCalls(Assembly assembly) => FindCalls(assembly.GetTypes());

    /// <summary>Every call site in the given types' declared methods and constructors.</summary>
    /// <param name="types">The types to scan.</param>
    public static IReadOnlyList<IlCall> FindCalls(IEnumerable<Type> types)
    {
        var calls = new List<IlCall>();
        foreach (var type in types)
        {
            var typeArguments = type.IsGenericTypeDefinition ? type.GetGenericArguments() : null;
            foreach (var member in type.GetMethods(AllMembers).Cast<MethodBase>().Concat(type.GetConstructors(AllMembers)))
            {
                ScanBody(member, typeArguments, calls);
            }
        }

        return calls;
    }

    private static void ScanBody(MethodBase member, Type[]? typeArguments, List<IlCall> calls)
    {
        if (member.GetMethodBody()?.GetILAsByteArray() is not { } il)
        {
            return;
        }

        var methodArguments = member.IsGenericMethodDefinition ? member.GetGenericArguments() : null;
        var position = 0;
        while (position < il.Length)
        {
            OpCode opCode;
            if (il[position] == 0xFE)
            {
                opCode = s_twoByte[il[position + 1]];
                position += 2;
            }
            else
            {
                opCode = s_oneByte[il[position]];
                position++;
            }

            if (IsCall(opCode))
            {
                var callee = Resolve(member.Module, BitConverter.ToInt32(il, position), typeArguments, methodArguments);
                if (callee is not null)
                {
                    calls.Add(new IlCall(member, callee));
                }
            }

            position += OperandSize(opCode, il, position);
        }
    }

    private static bool IsCall(OpCode opCode) =>
        opCode == OpCodes.Call || opCode == OpCodes.Callvirt || opCode == OpCodes.Newobj || opCode == OpCodes.Ldftn || opCode == OpCodes.Ldvirtftn;

    private static MethodBase? Resolve(Module module, int token, Type[]? typeArguments, Type[]? methodArguments)
    {
        try
        {
            return module.ResolveMethod(token, typeArguments, methodArguments);
        }
        catch (Exception ex) when (ex is ArgumentException or MissingMemberException or BadImageFormatException or NotSupportedException)
        {
            // A token the module cannot resolve in this generic context is skipped, never thrown: the scan is best effort
            // over the callees that matter, and every banned callee is a non-generic runtime method.
            return null;
        }
    }

    private static int OperandSize(OpCode opCode, byte[] il, int position) => opCode.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, position)),
        _ => 4,
    };
}
