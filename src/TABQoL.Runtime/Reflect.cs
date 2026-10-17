using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace TABQoL
{
    internal static class Reflect
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly Dictionary<string, PropertyInfo> Properties = new Dictionary<string, PropertyInfo>();
        internal static Assembly GameAssembly;

        internal static Type Type(string name)
        {
            name = Compatibility.RuntimeSymbol(name);
            var t = GameAssembly.GetType(name, false);
            if (t != null) return t;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = assembly.GetType(name, false);
                if (t != null) return t;
            }
            throw new TypeLoadException(name);
        }

        private static PropertyInfo Property(Type type, string name)
        {
            string key = type.AssemblyQualifiedName + ":" + name;
            PropertyInfo property;
            lock (Properties)
            {
                if (!Properties.TryGetValue(key, out property))
                {
                    var declaring = type;
                    while (declaring != null && property == null)
                    {
                        property = declaring.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
                        declaring = declaring.BaseType;
                    }
                    if (property == null) throw new MissingMemberException(type.FullName, name);
                    Properties.Add(key, property);
                }
                return property;
            }
        }

        internal static object Get(object instance, string name)
        {
            var type = instance as Type ?? instance.GetType();
            return Property(type, name).GetValue(instance is Type ? null : instance, null);
        }

        internal static void Set(object instance, string name, object value)
        {
            var type = instance as Type ?? instance.GetType();
            Property(type, name).SetValue(instance is Type ? null : instance, value, null);
        }

        internal static MethodInfo Method(Type type, string name, object[] args)
        {
            name = Compatibility.RuntimeSymbol(name);
            return type.GetMethods(Flags).Single(m => m.Name == name && Fits(m.GetParameters(), args));
        }

        private static bool Fits(ParameterInfo[] parameters, object[] args)
        {
            return parameters.Length == args.Length && parameters.Select((p, i) => args[i] == null
                ? !p.ParameterType.IsValueType || Nullable.GetUnderlyingType(p.ParameterType) != null
                : p.ParameterType.IsInstanceOfType(args[i])).All(x => x);
        }

        internal static object Call(object instance, string method, params object[] args)
        {
            var type = instance as Type ?? instance.GetType();
            return Method(type, method, args).Invoke(instance is Type ? null : instance, args);
        }

        internal static object New(string type, params object[] args)
        {
            return Type(type).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(c => Fits(c.GetParameters(), args)).Invoke(args);
        }

        internal static object Current(string type) { return Call(Type(type), Compatibility.Current); }
        internal static IEnumerable<object> Items(object collection) { return ((IEnumerable)collection).Cast<object>(); }
        internal static object EnumValue(string type, string value) { return Enum.Parse(Type(type), value); }

        internal static object FieldOfType(object instance, string type)
        {
            return instance.GetType().GetFields(Flags).Single(f => f.FieldType.FullName == type).GetValue(instance);
        }

        internal static object Field(object instance, string name)
        {
            name = Compatibility.RuntimeSymbol(name);
            return instance.GetType().GetField(name, Flags).GetValue(instance);
        }

        internal static void Event(object instance, string name, Action<object> callback)
        {
            var evt = instance.GetType().GetEvent(name, Flags) ?? throw new MissingMemberException(name);
            var invoke = evt.EventHandlerType.GetMethod("Invoke");
            var parameters = invoke.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
            var body = Expression.Invoke(Expression.Constant(callback), parameters.Length == 0
                ? (Expression)Expression.Constant(null, typeof(object)) : Expression.Convert(parameters[0], typeof(object)));
            evt.AddEventHandler(instance, Expression.Lambda(evt.EventHandlerType, body, parameters).Compile());
        }
    }
}
