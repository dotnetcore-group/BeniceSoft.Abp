using System.Reflection;
using System.Reflection.Emit;

namespace BeniceSoft.Core.Reflector;

internal static class ILGeneratorExtensions
{
    public static void EmitLoadArg(this ILGenerator ilGenerator, int index)
    {
        ArgumentNullException.ThrowIfNull(ilGenerator);

        switch (index)
        {
            case 0:
                ilGenerator.Emit(OpCodes.Ldarg_0);
                break;
            case 1:
                ilGenerator.Emit(OpCodes.Ldarg_1);
                break;
            case 2:
                ilGenerator.Emit(OpCodes.Ldarg_2);
                break;
            case 3:
                ilGenerator.Emit(OpCodes.Ldarg_3);
                break;
            default:
                if (index <= byte.MaxValue)
                {
                    ilGenerator.Emit(OpCodes.Ldarg_S, (byte)index);
                }
                else
                {
                    ilGenerator.Emit(OpCodes.Ldarg, index);
                }

                break;
        }
    }

    public static void EmitLoadArgA(this ILGenerator ilGenerator, int index)
    {
        ArgumentNullException.ThrowIfNull(ilGenerator);

        if (index <= byte.MaxValue)
        {
            ilGenerator.Emit(OpCodes.Ldarga_S, (byte)index);
        }
        else
        {
            ilGenerator.Emit(OpCodes.Ldarga, index);
        }
    }

    public static void EmitConvertToObject(this ILGenerator ilGenerator, Type typeFrom)
    {
        ArgumentNullException.ThrowIfNull(ilGenerator);
        ArgumentNullException.ThrowIfNull(typeFrom);

        if (typeFrom.GetTypeInfo().IsGenericParameter)
        {
            ilGenerator.Emit(OpCodes.Box, typeFrom);
        }
        else
        {
            ilGenerator.EmitConvertToType(typeFrom, typeof(object), true);
        }
    }

    public static void EmitConvertFromObject(this ILGenerator ilGenerator, Type typeTo)
    {
        ArgumentNullException.ThrowIfNull(ilGenerator);
        ArgumentNullException.ThrowIfNull(typeTo);

        if (typeTo.GetTypeInfo().IsGenericParameter)
        {
            ilGenerator.Emit(OpCodes.Unbox_Any, typeTo);
        }
        else
        {
            ilGenerator.EmitConvertToType(typeof(object), typeTo, true);
        }
    }

    public static void EmitThis(this ILGenerator ilGenerator)
    {
        ArgumentNullException.ThrowIfNull(ilGenerator);

        ilGenerator.EmitLoadArg(0);
    }

    public static void EmitType(this ILGenerator ilGenerator, Type type)
    {
        ArgumentNullException.ThrowIfNull(ilGenerator);
        ArgumentNullException.ThrowIfNull(type);

        ilGenerator.Emit(OpCodes.Ldtoken, type);
        ilGenerator.Emit(OpCodes.Call, MethodInfoConstant.GetTypeFromHandle);
    }

    public static void EmitMethod(this ILGenerator ilGenerator, MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(ilGenerator);
        ArgumentNullException.ThrowIfNull(method);

        EmitMethod(ilGenerator, method, method.DeclaringType!);
    }

    public static void EmitMethod(this ILGenerator ilGenerator, MethodInfo method, Type declaringType)
    {
        ArgumentNullException.ThrowIfNull(ilGenerator);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(declaringType);

        ilGenerator.Emit(OpCodes.Ldtoken, method);
        ilGenerator.Emit(OpCodes.Ldtoken, method.DeclaringType!);
        ilGenerator.Emit(OpCodes.Call, MethodInfoConstant.GetMethodFromHandle);
        ilGenerator.EmitConvertToType(typeof(MethodBase), typeof(MethodInfo));
    }

    public static void EmitConvertToType(this ILGenerator ilGenerator, Type typeFrom, Type typeTo, bool isChecked = true)
    {
        ArgumentNullException.ThrowIfNull(ilGenerator);
        ArgumentNullException.ThrowIfNull(typeFrom);
        ArgumentNullException.ThrowIfNull(typeTo);

        var typeFromInfo = typeFrom.GetTypeInfo();
        var typeToInfo = typeTo.GetTypeInfo();

        var nnExprType = typeFromInfo.GetNonNullableType();
        var nnType = typeToInfo.GetNonNullableType();

        if (TypeInfoUtils.AreEquivalent(typeFromInfo, typeToInfo))
        {
            return;
        }

        if (typeFromInfo.IsInterface ||
            typeToInfo.IsInterface ||
            typeFrom == typeof(object) ||
            typeTo == typeof(object) ||
            typeFrom == typeof(System.Enum) ||
            typeFrom == typeof(System.ValueType) ||
            TypeInfoUtils.IsLegalExplicitVariantDelegateConversion(typeFromInfo, typeToInfo))
        {
            ilGenerator.EmitCastToType(typeFromInfo, typeToInfo);
        }
        else if (typeFromInfo.IsNullableType() || typeToInfo.IsNullableType())
        {
            ilGenerator.EmitNullableConversion(typeFromInfo, typeToInfo, isChecked);
        }
        else if (!(typeFromInfo.IsConvertible() && typeToInfo.IsConvertible()) && (nnExprType.GetTypeInfo().IsAssignableFrom(nnType) || nnType.GetTypeInfo().IsAssignableFrom(nnExprType)))
        {
            ilGenerator.EmitCastToType(typeFromInfo, typeToInfo);
        }
        else if (typeFromInfo.IsArray && typeToInfo.IsArray)
        {
            // See DevDiv Bugs #94657.
            ilGenerator.EmitCastToType(typeFromInfo, typeToInfo);
        }
        else
        {
            ilGenerator.EmitNumericConversion(typeFromInfo, typeToInfo, isChecked);
        }
    }

    public static void EmitCastToType(this ILGenerator ilGenerator, TypeInfo typeFrom, TypeInfo typeTo)
    {
        ArgumentNullException.ThrowIfNull(ilGenerator);

        if (typeFrom.IsValueType)
        {
            ilGenerator.Emit(OpCodes.Box, typeFrom);
            if (typeTo != typeof(object))
            {
                ilGenerator.Emit(OpCodes.Castclass, typeTo);
            }
        }
        else
        {
            ilGenerator.Emit(typeTo.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, typeTo);
        }
    }

    public static void EmitHasValue(this ILGenerator ilGenerator, Type nullableType)
    {
        ArgumentNullException.ThrowIfNull(ilGenerator);

        var mi = nullableType.GetTypeInfo().GetMethod("get_HasValue", BindingFlags.Instance | BindingFlags.Public);
        ilGenerator.Emit(OpCodes.Call, mi!);
    }

    public static void EmitGetValueOrDefault(this ILGenerator ilGenerator, Type nullableType)
    {
        var mi = nullableType.GetTypeInfo().GetMethod("GetValueOrDefault", Type.EmptyTypes);
        ilGenerator.Emit(OpCodes.Call, mi!);
    }

    public static void EmitGetValue(this ILGenerator ilGenerator, Type nullableType)
    {
        var mi = nullableType.GetTypeInfo().GetMethod("get_Value", BindingFlags.Instance | BindingFlags.Public);
        ilGenerator.Emit(OpCodes.Call, mi!);
    }

    public static void EmitConstant(this ILGenerator ilGenerator, object value, Type valueType)
    {
        ArgumentNullException.ThrowIfNull(ilGenerator);
        ArgumentNullException.ThrowIfNull(valueType);

        if (value == null)
        {
            EmitDefault(ilGenerator, valueType);
            return;
        }

        if (ilGenerator.TryEmitILConstant(value, valueType))
        {
            return;
        }

        var t = value as Type;
        if (t != null)
        {
            ilGenerator.EmitType(t);
            if (valueType != typeof(Type))
            {
                ilGenerator.Emit(OpCodes.Castclass, valueType);
            }

            return;
        }

        var mb = value as MethodBase;
        if (mb != null)
        {
            ilGenerator.EmitMethod((MethodInfo)mb);
            return;
        }

        if (valueType.IsArray())
        {
            var array = (Array)value;
            ilGenerator.EmitArray(array, valueType.GetElementType()!);
        }

        throw new InvalidOperationException("Code supposed to be unreachable.");
    }

    public static void EmitDefault(this ILGenerator ilGenerator, Type type)
    {
        ArgumentNullException.ThrowIfNull(ilGenerator);
        ArgumentNullException.ThrowIfNull(type);

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Object:
            case TypeCode.DateTime:
                if (type.IsValueType())
                {
                    // Type.GetTypeCode on an enum returns the underlying
                    // integer TypeCode, so we won't get here.
                    // This is the IL for default(T) if T is a generic type
                    // parameter, so it should work for any type. It's also
                    // the standard pattern for structs.
                    var lb = ilGenerator.DeclareLocal(type);
                    ilGenerator.Emit(OpCodes.Ldloca, lb);
                    ilGenerator.Emit(OpCodes.Initobj, type);
                    ilGenerator.Emit(OpCodes.Ldloc, lb);
                }
                else
                {
                    ilGenerator.Emit(OpCodes.Ldnull);
                }

                break;

            case TypeCode.Empty:
            case TypeCode.String:
                ilGenerator.Emit(OpCodes.Ldnull);
                break;

            case TypeCode.Boolean:
            case TypeCode.Char:
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
                ilGenerator.Emit(OpCodes.Ldc_I4_0);
                break;

            case TypeCode.Int64:
            case TypeCode.UInt64:
                ilGenerator.Emit(OpCodes.Ldc_I4_0);
                ilGenerator.Emit(OpCodes.Conv_I8);
                break;

            case TypeCode.Single:
                ilGenerator.Emit(OpCodes.Ldc_R4, default(float));
                break;

            case TypeCode.Double:
                ilGenerator.Emit(OpCodes.Ldc_R8, default(double));
                break;

            case TypeCode.Decimal:
                ilGenerator.Emit(OpCodes.Ldc_I4_0);
                ilGenerator.Emit(OpCodes.Newobj, typeof(decimal).GetTypeInfo().GetConstructor([typeof(int)])!);
                break;

            default:
                throw new InvalidOperationException("Code supposed to be unreachable.");
        }
    }

    public static bool CanEmitConstant(object value, Type type)
    {
        if (value == null || CanEmitILConstant(type))
        {
            return true;
        }

        var t = value as Type;
        if (t != null && ShouldLdtoken(t))
        {
            return true;
        }

        MethodBase? mb = value as MethodInfo;
        if (mb != null && ShouldLdtoken(mb))
        {
            return true;
        }

        return false;
    }

    public static void EmitDecimal(this ILGenerator ilGenerator, decimal value)
    {
        if (decimal.Truncate(value) == value)
        {
            if (value is >= int.MinValue and <= int.MaxValue)
            {
                var intValue = decimal.ToInt32(value);
                ilGenerator.EmitInt(intValue);
                ilGenerator.EmitNew(typeof(decimal).GetTypeInfo().GetConstructor([typeof(int)])!);
            }
            else if (value is >= long.MinValue and <= long.MaxValue)
            {
                var longValue = decimal.ToInt64(value);
                ilGenerator.EmitLong(longValue);
                ilGenerator.EmitNew(typeof(decimal).GetTypeInfo().GetConstructor([typeof(long)])!);
            }
            else
            {
                ilGenerator.EmitDecimalBits(value);
            }
        }
        else
        {
            ilGenerator.EmitDecimalBits(value);
        }
    }

    public static void EmitNew(this ILGenerator ilGenerator, ConstructorInfo ci)
    {
        ilGenerator.Emit(OpCodes.Newobj, ci);
    }

    public static void EmitNull(this ILGenerator ilGenerator)
    {
        ilGenerator.Emit(OpCodes.Ldnull);
    }

    public static void EmitString(this ILGenerator ilGenerator, string value)
    {
        ilGenerator.Emit(OpCodes.Ldstr, value);
    }

    public static void EmitBoolean(this ILGenerator ilGenerator, bool value)
    {
        if (value)
        {
            ilGenerator.Emit(OpCodes.Ldc_I4_1);
        }
        else
        {
            ilGenerator.Emit(OpCodes.Ldc_I4_0);
        }
    }

    public static void EmitChar(this ILGenerator ilGenerator, char value)
    {
        ilGenerator.EmitInt(value);
        ilGenerator.Emit(OpCodes.Conv_U2);
    }

    public static void EmitByte(this ILGenerator ilGenerator, byte value)
    {
        ilGenerator.EmitInt(value);
        ilGenerator.Emit(OpCodes.Conv_U1);
    }

    public static void EmitSByte(this ILGenerator ilGenerator, sbyte value)
    {
        ilGenerator.EmitInt(value);
        ilGenerator.Emit(OpCodes.Conv_I1);
    }

    public static void EmitShort(this ILGenerator ilGenerator, short value)
    {
        ilGenerator.EmitInt(value);
        ilGenerator.Emit(OpCodes.Conv_I2);
    }

    public static void EmitUShort(this ILGenerator ilGenerator, ushort value)
    {
        ilGenerator.EmitInt(value);
        ilGenerator.Emit(OpCodes.Conv_U2);
    }

    public static void EmitInt(this ILGenerator ilGenerator, int value)
    {
        OpCode c;
        switch (value)
        {
            case -1:
                c = OpCodes.Ldc_I4_M1;
                break;
            case 0:
                c = OpCodes.Ldc_I4_0;
                break;
            case 1:
                c = OpCodes.Ldc_I4_1;
                break;
            case 2:
                c = OpCodes.Ldc_I4_2;
                break;
            case 3:
                c = OpCodes.Ldc_I4_3;
                break;
            case 4:
                c = OpCodes.Ldc_I4_4;
                break;
            case 5:
                c = OpCodes.Ldc_I4_5;
                break;
            case 6:
                c = OpCodes.Ldc_I4_6;
                break;
            case 7:
                c = OpCodes.Ldc_I4_7;
                break;
            case 8:
                c = OpCodes.Ldc_I4_8;
                break;
            default:
                if (value is >= (-128) and <= 127)
                {
                    ilGenerator.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
                }
                else
                {
                    ilGenerator.Emit(OpCodes.Ldc_I4, value);
                }

                return;
        }

        ilGenerator.Emit(c);
    }

    public static void EmitUInt(this ILGenerator ilGenerator, uint value)
    {
        ilGenerator.EmitInt((int)value);
        ilGenerator.Emit(OpCodes.Conv_U4);
    }

    public static void EmitLong(this ILGenerator ilGenerator, long value)
    {
        ilGenerator.Emit(OpCodes.Ldc_I8, value);

        //
        // Now, emit convert to give the constant type information.
        //
        // Otherwise, it is treated as unsigned and overflow is not
        // detected if it's used in checked ops.
        //
        ilGenerator.Emit(OpCodes.Conv_I8);
    }

    public static void EmitULong(this ILGenerator ilGenerator, ulong value)
    {
        ilGenerator.Emit(OpCodes.Ldc_I8, (long)value);
        ilGenerator.Emit(OpCodes.Conv_U8);
    }

    public static void EmitDouble(this ILGenerator ilGenerator, double value)
    {
        ilGenerator.Emit(OpCodes.Ldc_R8, value);
    }

    public static void EmitSingle(this ILGenerator ilGenerator, float value)
    {
        ilGenerator.Emit(OpCodes.Ldc_R4, value);
    }

    public static void EmitArray(this ILGenerator ilGenerator, Array items, Type elementType)
    {
        ilGenerator.EmitInt(items.Length);
        ilGenerator.Emit(OpCodes.Newarr, elementType);
        foreach (var i in items.Length)
        {
            ilGenerator.Emit(OpCodes.Dup);
            ilGenerator.EmitInt(i);
            var itemValue = items.GetValue(i);
            var constantType = itemValue!.GetType();
            if (constantType == elementType)
            {
                ilGenerator.EmitConstant(itemValue, elementType);
            }
            else
            {
                ilGenerator.EmitConstant(itemValue, constantType);
                ilGenerator.EmitConvertToObject(constantType);
            }

            ilGenerator.EmitStoreElement(elementType);
        }
    }

    public static void EmitStoreElement(this ILGenerator ilGenerator, Type type)
    {
        if (type.IsEnum())
        {
            ilGenerator.Emit(OpCodes.Stelem, type);
            return;
        }

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Boolean:
            case TypeCode.SByte:
            case TypeCode.Byte:
                ilGenerator.Emit(OpCodes.Stelem_I1);
                break;
            case TypeCode.Char:
            case TypeCode.Int16:
            case TypeCode.UInt16:
                ilGenerator.Emit(OpCodes.Stelem_I2);
                break;
            case TypeCode.Int32:
            case TypeCode.UInt32:
                ilGenerator.Emit(OpCodes.Stelem_I4);
                break;
            case TypeCode.Int64:
            case TypeCode.UInt64:
                ilGenerator.Emit(OpCodes.Stelem_I8);
                break;
            case TypeCode.Single:
                ilGenerator.Emit(OpCodes.Stelem_R4);
                break;
            case TypeCode.Double:
                ilGenerator.Emit(OpCodes.Stelem_R8);
                break;
            default:
                if (type.IsValueType())
                {
                    ilGenerator.Emit(OpCodes.Stelem, type);
                }
                else
                {
                    ilGenerator.Emit(OpCodes.Stelem_Ref);
                }

                break;
        }
    }

    public static void EmitLoadElement(this ILGenerator ilGenerator, Type type)
    {
        if (!type.IsValueType())
        {
            ilGenerator.Emit(OpCodes.Ldelem_Ref);
        }
        else if (type.IsEnum())
        {
            ilGenerator.Emit(OpCodes.Ldelem, type);
        }
        else
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                case TypeCode.SByte:
                    ilGenerator.Emit(OpCodes.Ldelem_I1);
                    break;
                case TypeCode.Byte:
                    ilGenerator.Emit(OpCodes.Ldelem_U1);
                    break;
                case TypeCode.Int16:
                    ilGenerator.Emit(OpCodes.Ldelem_I2);
                    break;
                case TypeCode.Char:
                case TypeCode.UInt16:
                    ilGenerator.Emit(OpCodes.Ldelem_U2);
                    break;
                case TypeCode.Int32:
                    ilGenerator.Emit(OpCodes.Ldelem_I4);
                    break;
                case TypeCode.UInt32:
                    ilGenerator.Emit(OpCodes.Ldelem_U4);
                    break;
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    ilGenerator.Emit(OpCodes.Ldelem_I8);
                    break;
                case TypeCode.Single:
                    ilGenerator.Emit(OpCodes.Ldelem_R4);
                    break;
                case TypeCode.Double:
                    ilGenerator.Emit(OpCodes.Ldelem_R8);
                    break;
                default:
                    ilGenerator.Emit(OpCodes.Ldelem, type);
                    break;
            }
        }
    }

    public static void EmitLdRef(this ILGenerator ilGenerator, Type type)
    {
        ArgumentNullException.ThrowIfNull(ilGenerator);
        ArgumentNullException.ThrowIfNull(type);

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.SByte:
                ilGenerator.Emit(OpCodes.Ldind_I1);
                break;
            case TypeCode.Boolean:
            case TypeCode.Byte:
                ilGenerator.Emit(OpCodes.Ldind_U1);
                break;
            case TypeCode.Int16:
                ilGenerator.Emit(OpCodes.Ldind_I2);
                break;
            case TypeCode.Char:
            case TypeCode.UInt16:
                ilGenerator.Emit(OpCodes.Ldind_U2);
                break;
            case TypeCode.Int32:
                ilGenerator.Emit(OpCodes.Ldind_I4);
                break;
            case TypeCode.UInt32:
                ilGenerator.Emit(OpCodes.Ldind_U4);
                break;
            case TypeCode.Int64:
            case TypeCode.UInt64:
                ilGenerator.Emit(OpCodes.Ldind_I8);
                break;
            case TypeCode.Single:
                ilGenerator.Emit(OpCodes.Ldind_R4);
                break;
            case TypeCode.Double:
                ilGenerator.Emit(OpCodes.Ldind_R8);
                break;
            default:
                if (type.IsValueType())
                {
                    ilGenerator.Emit(OpCodes.Ldobj, type);
                }
                else
                {
                    ilGenerator.Emit(OpCodes.Ldind_Ref);
                }

                break;
        }
    }

    public static void EmitStRef(this ILGenerator ilGenerator, Type type)
    {
        ArgumentNullException.ThrowIfNull(ilGenerator);
        ArgumentNullException.ThrowIfNull(type);

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Boolean:
            case TypeCode.Byte:
            case TypeCode.SByte:
                ilGenerator.Emit(OpCodes.Stind_I1);
                break;
            case TypeCode.Char:
            case TypeCode.Int16:
            case TypeCode.UInt16:
                ilGenerator.Emit(OpCodes.Stind_I2);
                break;
            case TypeCode.Int32:
            case TypeCode.UInt32:
                ilGenerator.Emit(OpCodes.Stind_I4);
                break;
            case TypeCode.Int64:
            case TypeCode.UInt64:
                ilGenerator.Emit(OpCodes.Stind_I8);
                break;
            case TypeCode.Single:
                ilGenerator.Emit(OpCodes.Stind_R4);
                break;
            case TypeCode.Double:
                ilGenerator.Emit(OpCodes.Stind_R8);
                break;
            default:
                if (type.IsValueType())
                {
                    ilGenerator.Emit(OpCodes.Stobj, type);
                }
                else
                {
                    ilGenerator.Emit(OpCodes.Stind_Ref);
                }

                break;
        }
    }

    #region private
    private static void EmitNullableConversion(this ILGenerator ilGenerator, TypeInfo typeFrom, TypeInfo typeTo, bool isChecked)
    {
        var isTypeFromNullable = TypeInfoUtils.IsNullableType(typeFrom);
        var isTypeToNullable = TypeInfoUtils.IsNullableType(typeTo);
        if (isTypeFromNullable && isTypeToNullable)
        {
            ilGenerator.EmitNullableToNullableConversion(typeFrom, typeTo, isChecked);
        }
        else if (isTypeFromNullable)
        {
            ilGenerator.EmitNullableToNonNullableConversion(typeFrom, typeTo, isChecked);
        }
        else
        {
            ilGenerator.EmitNonNullableToNullableConversion(typeFrom, typeTo, isChecked);
        }
    }

    private static void EmitNullableToNullableConversion(this ILGenerator ilGenerator, TypeInfo typeFrom, TypeInfo typeTo, bool isChecked)
    {
        var locFrom = ilGenerator.DeclareLocal(typeFrom.AsType());
        ilGenerator.Emit(OpCodes.Stloc, locFrom);
        var locTo = ilGenerator.DeclareLocal(typeTo.AsType());
        // test for null
        ilGenerator.Emit(OpCodes.Ldloca, locFrom);
        ilGenerator.EmitHasValue(typeFrom.AsType());
        var labIfNull = ilGenerator.DefineLabel();
        ilGenerator.Emit(OpCodes.Brfalse_S, labIfNull);
        ilGenerator.Emit(OpCodes.Ldloca, locFrom);
        ilGenerator.EmitGetValueOrDefault(typeFrom.AsType());
        var nnTypeFrom = TypeInfoUtils.GetNonNullableType(typeFrom);
        var nnTypeTo = TypeInfoUtils.GetNonNullableType(typeTo);
        ilGenerator.EmitConvertToType(nnTypeFrom, nnTypeTo, isChecked);
        // construct result type
        var ci = typeTo.GetConstructor([nnTypeTo]);
        ilGenerator.Emit(OpCodes.Newobj, ci!);
        ilGenerator.Emit(OpCodes.Stloc, locTo);
        var labEnd = ilGenerator.DefineLabel();
        ilGenerator.Emit(OpCodes.Br_S, labEnd);
        // if null then create a default one
        ilGenerator.MarkLabel(labIfNull);
        ilGenerator.Emit(OpCodes.Ldloca, locTo);
        ilGenerator.Emit(OpCodes.Initobj, typeTo.AsType());
        ilGenerator.MarkLabel(labEnd);
        ilGenerator.Emit(OpCodes.Ldloc, locTo);
    }

    private static void EmitNullableToNonNullableConversion(this ILGenerator ilGenerator, TypeInfo typeFrom, TypeInfo typeTo, bool isChecked)
    {
        if (typeTo.IsValueType)
        {
            ilGenerator.EmitNullableToNonNullableStructConversion(typeFrom, typeTo, isChecked);
        }
        else
        {
            ilGenerator.EmitNullableToReferenceConversion(typeFrom);
        }
    }

    private static void EmitNullableToNonNullableStructConversion(this ILGenerator ilGenerator, TypeInfo typeFrom, TypeInfo typeTo, bool isChecked)
    {
        var locFrom = ilGenerator.DeclareLocal(typeFrom.AsType());
        ilGenerator.Emit(OpCodes.Stloc, locFrom);
        ilGenerator.Emit(OpCodes.Ldloca, locFrom);
        ilGenerator.EmitGetValue(typeFrom.AsType());
        var nnTypeFrom = TypeInfoUtils.GetNonNullableType(typeFrom);
        ilGenerator.EmitConvertToType(nnTypeFrom, typeTo.AsType(), isChecked);
    }

    private static void EmitNullableToReferenceConversion(this ILGenerator ilGenerator, TypeInfo typeFrom)
    {
        // We've got a conversion from nullable to Object, ValueType, Enum, etc.  Just box it so that
        // we get the nullable semantics.
        ilGenerator.Emit(OpCodes.Box, typeFrom.AsType());
    }

    private static void EmitNonNullableToNullableConversion(this ILGenerator ilGenerator, TypeInfo typeFrom, TypeInfo typeTo, bool isChecked)
    {
        var locTo = ilGenerator.DeclareLocal(typeTo.AsType());
        var nnTypeTo = TypeInfoUtils.GetNonNullableType(typeTo);
        ilGenerator.EmitConvertToType(typeFrom.AsType(), nnTypeTo, isChecked);
        var ci = typeTo.GetConstructor([nnTypeTo]);
        ilGenerator.Emit(OpCodes.Newobj, ci!);
        ilGenerator.Emit(OpCodes.Stloc, locTo);
        ilGenerator.Emit(OpCodes.Ldloc, locTo);
    }

    private static void EmitNumericConversion(this ILGenerator ilGenerator, TypeInfo typeFrom, TypeInfo typeTo, bool isChecked)
    {
        var isFromUnsigned = TypeInfoUtils.IsUnsigned(typeFrom);
        var isFromFloatingPoint = TypeInfoUtils.IsFloatingPoint(typeFrom);
        if (typeTo.AsType() == typeof(float))
        {
            if (isFromUnsigned)
            {
                ilGenerator.Emit(OpCodes.Conv_R_Un);
            }

            ilGenerator.Emit(OpCodes.Conv_R4);
        }
        else if (typeTo.AsType() == typeof(double))
        {
            if (isFromUnsigned)
            {
                ilGenerator.Emit(OpCodes.Conv_R_Un);
            }

            ilGenerator.Emit(OpCodes.Conv_R8);
        }
        else
        {
            var tc = Type.GetTypeCode(typeTo.AsType());
            if (isChecked)
            {
                // Overflow checking needs to know if the source value on the IL stack is unsigned or not.
                if (isFromUnsigned)
                {
                    switch (tc)
                    {
                        case TypeCode.SByte:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_I1_Un);
                            break;
                        case TypeCode.Int16:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_I2_Un);
                            break;
                        case TypeCode.Int32:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_I4_Un);
                            break;
                        case TypeCode.Int64:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_I8_Un);
                            break;
                        case TypeCode.Byte:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_U1_Un);
                            break;
                        case TypeCode.UInt16:
                        case TypeCode.Char:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_U2_Un);
                            break;
                        case TypeCode.UInt32:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_U4_Un);
                            break;
                        case TypeCode.UInt64:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_U8_Un);
                            break;
                        default:
                            throw new InvalidCastException();
                    }
                }
                else
                {
                    switch (tc)
                    {
                        case TypeCode.SByte:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_I1);
                            break;
                        case TypeCode.Int16:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_I2);
                            break;
                        case TypeCode.Int32:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_I4);
                            break;
                        case TypeCode.Int64:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_I8);
                            break;
                        case TypeCode.Byte:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_U1);
                            break;
                        case TypeCode.UInt16:
                        case TypeCode.Char:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_U2);
                            break;
                        case TypeCode.UInt32:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_U4);
                            break;
                        case TypeCode.UInt64:
                            ilGenerator.Emit(OpCodes.Conv_Ovf_U8);
                            break;
                        default:
                            throw new InvalidCastException();
                    }
                }
            }
            else
            {
                switch (tc)
                {
                    case TypeCode.SByte:
                        ilGenerator.Emit(OpCodes.Conv_I1);
                        break;
                    case TypeCode.Byte:
                        ilGenerator.Emit(OpCodes.Conv_U1);
                        break;
                    case TypeCode.Int16:
                        ilGenerator.Emit(OpCodes.Conv_I2);
                        break;
                    case TypeCode.UInt16:
                    case TypeCode.Char:
                        ilGenerator.Emit(OpCodes.Conv_U2);
                        break;
                    case TypeCode.Int32:
                        ilGenerator.Emit(OpCodes.Conv_I4);
                        break;
                    case TypeCode.UInt32:
                        ilGenerator.Emit(OpCodes.Conv_U4);
                        break;
                    case TypeCode.Int64:
                        if (isFromUnsigned)
                        {
                            ilGenerator.Emit(OpCodes.Conv_U8);
                        }
                        else
                        {
                            ilGenerator.Emit(OpCodes.Conv_I8);
                        }

                        break;
                    case TypeCode.UInt64:
                        if (isFromUnsigned || isFromFloatingPoint)
                        {
                            ilGenerator.Emit(OpCodes.Conv_U8);
                        }
                        else
                        {
                            ilGenerator.Emit(OpCodes.Conv_I8);
                        }

                        break;
                    default:
                        throw new InvalidCastException();
                }
            }
        }
    }

    private static bool ShouldLdtoken(Type t)
    {
        return t.IsGenericParameter || t.GetTypeInfo().IsVisible;
    }

    private static bool ShouldLdtoken(MethodBase mb)
    {
        // Can't ldtoken on a DynamicMethod
        if (mb is DynamicMethod)
        {
            return false;
        }

        var dt = mb.DeclaringType;
        return dt == null || ShouldLdtoken(dt);
    }

    private static bool TryEmitILConstant(this ILGenerator ilGenerator, object value, Type type)
    {
        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Boolean:
                ilGenerator.EmitBoolean((bool)value);
                return true;
            case TypeCode.SByte:
                ilGenerator.EmitSByte((sbyte)value);
                return true;
            case TypeCode.Int16:
                ilGenerator.EmitShort((short)value);
                return true;
            case TypeCode.Int32:
                ilGenerator.EmitInt((int)value);
                return true;
            case TypeCode.Int64:
                ilGenerator.EmitLong((long)value);
                return true;
            case TypeCode.Single:
                ilGenerator.EmitSingle((float)value);
                return true;
            case TypeCode.Double:
                ilGenerator.EmitDouble((double)value);
                return true;
            case TypeCode.Char:
                ilGenerator.EmitChar((char)value);
                return true;
            case TypeCode.Byte:
                ilGenerator.EmitByte((byte)value);
                return true;
            case TypeCode.UInt16:
                ilGenerator.EmitUShort((ushort)value);
                return true;
            case TypeCode.UInt32:
                ilGenerator.EmitUInt((uint)value);
                return true;
            case TypeCode.UInt64:
                ilGenerator.EmitULong((ulong)value);
                return true;
            case TypeCode.Decimal:
                ilGenerator.EmitDecimal((decimal)value);
                return true;
            case TypeCode.String:
                ilGenerator.EmitString((string)value);
                return true;
            default:
                return false;
        }
    }

    private static void EmitDecimalBits(this ILGenerator ilGenerator, decimal value)
    {
        var bits = decimal.GetBits(value);
        ilGenerator.EmitInt(bits[0]);
        ilGenerator.EmitInt(bits[1]);
        ilGenerator.EmitInt(bits[2]);
        ilGenerator.EmitBoolean((bits[3] & 0x80000000) != 0);
        ilGenerator.EmitByte((byte)(bits[3] >> 16));
        ilGenerator.EmitNew(typeof(decimal).GetTypeInfo().GetConstructor([typeof(int), typeof(int), typeof(int), typeof(bool), typeof(byte)])!);
    }

    private static bool CanEmitILConstant(Type type)
    {
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean or TypeCode.SByte or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Char or TypeCode.Byte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 or TypeCode.Decimal or TypeCode.String => true,
            _ => false,
        };
    }
    #endregion
}
