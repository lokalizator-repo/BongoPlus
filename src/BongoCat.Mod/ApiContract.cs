using System;
using System.Reflection;

namespace BongoCatMod;

internal static class ApiContract
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static FieldInfo Field(Type owner, string name, Type value, bool isStatic = false)
    {
        var field = owner.GetField(name, Flags) ?? throw new MissingFieldException(owner.FullName, name);
        if (field.FieldType != value || field.IsStatic != isStatic)
            throw new NotSupportedException(owner.Name + "." + name + " changed type or storage.");
        return field;
    }

    public static MethodInfo Method(Type owner, string name, Type returns, bool isStatic, params Type[] parameters)
    {
        var method = owner.GetMethod(name, Flags, null, parameters, null)
            ?? throw new MissingMethodException(owner.FullName, name);
        if (method.ReturnType != returns || method.IsStatic != isStatic || method.ContainsGenericParameters)
            throw new NotSupportedException(owner.Name + "." + name + " changed signature.");
        return method;
    }

    public static PropertyInfo Property(Type owner, string name, Type value, bool writable = false, params Type[] indices)
    {
        var property = owner.GetProperty(name, Flags, null, value, indices, null)
            ?? throw new MissingMemberException(owner.FullName, name);
        if (!property.CanRead || (writable && !property.CanWrite))
            throw new NotSupportedException(owner.Name + "." + name + " changed accessors.");
        return property;
    }
}
