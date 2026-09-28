// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using System.Reflection.Emit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// The types one type refers to, read from compiled metadata with reflection (design §9.2: no NetArchTest
/// dependency for a handful of rules): its base type and interfaces; the constraints of its own and its methods'
/// generic parameters; the attributes on the type, its fields, properties, events, methods, constructors, parameters
/// and return values, including <c>typeof</c> attribute arguments; the signatures of those members; their local
/// variables; and every type, method and field token in their IL bodies. Generic arguments, array elements and by-ref
/// targets are unwrapped. Nested and compiler-generated types (closures, async state machines) are separate types;
/// scan them too.
/// </summary>
internal static class TypeReferences
{
    private const BindingFlags Declared =
        BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static
        | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Dictionary<short, OpCode> s_opCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value);

    /// <summary>Every type <paramref name="type"/> refers to, unwrapped to named types.</summary>
    public static IReadOnlySet<Type> Of(Type type)
    {
        var found = new HashSet<Type>();
        Add(found, type.BaseType);
        foreach (var referenced in type.GetInterfaces())
        {
            Add(found, referenced);
        }

        AddAttributes(found, type);
        AddConstraints(found, type.IsGenericTypeDefinition ? type.GetGenericArguments() : []);
        foreach (var field in type.GetFields(Declared))
        {
            AddAttributes(found, field);
            Add(found, field.FieldType);
        }

        foreach (var property in type.GetProperties(Declared))
        {
            AddAttributes(found, property);
            Add(found, property.PropertyType);
        }

        foreach (var @event in type.GetEvents(Declared))
        {
            AddAttributes(found, @event);
            Add(found, @event.EventHandlerType);
        }

        foreach (var method in type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)))
        {
            AddMethod(found, type, method);
        }

        return found;
    }

    private static void AddAttributes(HashSet<Type> found, ICustomAttributeProvider member)
    {
        var attributes = member switch
        {
            MemberInfo info => info.CustomAttributes,
            ParameterInfo parameter => parameter.CustomAttributes,
            _ => [],
        };
        foreach (var attribute in attributes)
        {
            Add(found, attribute.AttributeType);
            var arguments = attribute.ConstructorArguments.Concat(attribute.NamedArguments.Select(named => named.TypedValue));
            foreach (var argument in arguments)
            {
                AddAttributeValue(found, argument);
            }
        }
    }

    private static void AddAttributeValue(HashSet<Type> found, CustomAttributeTypedArgument argument)
    {
        switch (argument.Value)
        {
            case Type referenced:
                Add(found, referenced);
                break;
            case IEnumerable<CustomAttributeTypedArgument> elements:
                foreach (var element in elements)
                {
                    AddAttributeValue(found, element);
                }

                break;
        }
    }

    private static void AddConstraints(HashSet<Type> found, Type[] genericParameters)
    {
        foreach (var parameter in genericParameters.Where(argument => argument.IsGenericParameter))
        {
            foreach (var constraint in parameter.GetGenericParameterConstraints())
            {
                Add(found, constraint);
            }
        }
    }

    private static void AddMethod(HashSet<Type> found, Type owner, MethodBase method)
    {
        AddAttributes(found, method);
        if (method is MethodInfo info)
        {
            AddAttributes(found, info.ReturnParameter);
            Add(found, info.ReturnType);
            AddConstraints(found, info.IsGenericMethodDefinition ? info.GetGenericArguments() : []);
        }

        foreach (var parameter in method.GetParameters())
        {
            AddAttributes(found, parameter);
            Add(found, parameter.ParameterType);
        }

        var body = method.GetMethodBody();
        if (body is null)
        {
            return;
        }

        foreach (var local in body.LocalVariables)
        {
            Add(found, local.LocalType);
        }

        foreach (var operand in BodyOperands(owner, method))
        {
            switch (operand)
            {
                case Type referenced:
                    Add(found, referenced);
                    break;
                case FieldInfo field:
                    Add(found, field.DeclaringType);
                    Add(found, field.FieldType);
                    break;
                case MethodBase callee:
                    AddCallee(found, callee);
                    break;
            }
        }
    }

    /// <summary>The string literals (<c>ldstr</c> operands) in the method bodies of <paramref name="type"/>.</summary>
    public static IEnumerable<string> StringLiterals(Type type) => BodyOperands(type).OfType<string>();

    /// <summary>The fields, methods and constructors the method bodies of <paramref name="type"/> use.</summary>
    public static IEnumerable<MemberInfo> UsedMembers(Type type) =>
        BodyOperands(type).OfType<MemberInfo>().Where(member => member is FieldInfo or MethodBase);

    private static IEnumerable<object> BodyOperands(Type type) =>
        type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared))
            .SelectMany(method => BodyOperands(type, method));

    private static void AddCallee(HashSet<Type> found, MethodBase callee)
    {
        Add(found, callee.DeclaringType);
        if (callee is MethodInfo calleeInfo)
        {
            Add(found, calleeInfo.ReturnType);
            foreach (var argument in calleeInfo.IsGenericMethod ? calleeInfo.GetGenericArguments() : [])
            {
                Add(found, argument);
            }
        }

        foreach (var parameter in callee.GetParameters())
        {
            Add(found, parameter.ParameterType);
        }
    }

    /// <summary>
    /// Every operand of <paramref name="method"/>'s IL that names metadata, resolved: a <see cref="Type"/>,
    /// <see cref="FieldInfo"/> or <see cref="MethodBase"/> (field, method, type and ldtoken operands), or a
    /// <see cref="string"/> (ldstr operands).
    /// </summary>
    private static IEnumerable<object> BodyOperands(Type owner, MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        var typeArguments = owner.IsGenericType ? owner.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        foreach (var (kind, token) in TokenOperands(il))
        {
            yield return kind == OperandType.InlineString
                ? method.Module.ResolveString(token)
                : method.Module.ResolveMember(token, typeArguments, methodArguments)!;
        }
    }

    /// <summary>The metadata-token operands (field, method, type, ldtoken and string) of an IL stream.</summary>
    private static IEnumerable<(OperandType Kind, int Token)> TokenOperands(byte[] il)
    {
        var position = 0;
        while (position < il.Length)
        {
            short value = il[position++];
            if (value == 0xFE)
            {
                value = unchecked((short)(0xFE00 | il[position++]));
            }

            var code = s_opCodesByValue[value];
            switch (code.OperandType)
            {
                case OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineTok
                    or OperandType.InlineType or OperandType.InlineString:
                    yield return (code.OperandType, BitConverter.ToInt32(il, position));
                    position += 4;
                    break;
                case OperandType.InlineSwitch:
                    position += 4 + (4 * BitConverter.ToInt32(il, position));
                    break;
                default:
                    position += OperandSize(code.OperandType);
                    break;
            }
        }
    }

    private static int OperandSize(OperandType operand) => operand switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        _ => 4,
    };

    private static void Add(HashSet<Type> found, Type? type)
    {
        if (type is null)
        {
            return;
        }

        if (type.HasElementType)
        {
            Add(found, type.GetElementType());
            return;
        }

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                Add(found, argument);
            }

            type = type.GetGenericTypeDefinition();
        }

        if (!type.IsGenericParameter)
        {
            found.Add(type);
        }
    }
}
