// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-04-15 12:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-09-11 21:30
// ***********************************************************************
//  <copyright file="PropertyMetadataHelper.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Concurrent;
using System.Reflection;
using RzR.Extensions.Domain.Primitives;

#endregion

namespace RzR.DataVigil.EFCore.Helpers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Accesses EF Core property metadata via reflection
    /// </summary>
    /// =================================================================================================
    internal static class PropertyMetadataHelper
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the name cache.
        /// </summary>
        /// =================================================================================================
        private static readonly ConcurrentDictionary<Type, PropertyInfo> NameCache =
            new ConcurrentDictionary<Type, PropertyInfo>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the colour type cache.
        /// </summary>
        /// =================================================================================================
        private static readonly ConcurrentDictionary<Type, PropertyInfo> ClrTypeCache =
            new ConcurrentDictionary<Type, PropertyInfo>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the value converter accessor cache, keyed by the runtime property metadata type.
        /// </summary>
        /// =================================================================================================
        private static readonly ConcurrentDictionary<Type, Func<object, object>> ValueConverterCache =
            new ConcurrentDictionary<Type, Func<object, object>>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the value comparer accessor cache, keyed by the runtime property metadata type.
        /// </summary>
        /// =================================================================================================
        private static readonly ConcurrentDictionary<Type, Func<object, object>> ValueComparerCache =
            new ConcurrentDictionary<Type, Func<object, object>>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the <c>ConvertToProvider</c> delegate cache, keyed by the converter type.
        /// </summary>
        /// =================================================================================================
        private static readonly ConcurrentDictionary<Type, PropertyInfo> ConvertToProviderCache =
            new ConcurrentDictionary<Type, PropertyInfo>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the <c>ValueComparer.Equals(object, object)</c> delegate cache, keyed by the
        ///     runtime type of the comparer instance.
        /// </summary>
        /// =================================================================================================
        private static readonly ConcurrentDictionary<Type, Func<object, object, object, bool>> ComparerEqualsCache =
            new ConcurrentDictionary<Type, Func<object, object, object, bool>>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the EF Core 5 extension class holding the property metadata accessors.
        /// </summary>
        /// =================================================================================================
        private const string PropertyExtensionsTypeName = "Microsoft.EntityFrameworkCore.PropertyExtensions";

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the name of the <c>GetValueConverter</c> metadata accessor.
        /// </summary>
        /// =================================================================================================
        private const string MethodNameGetValueConverter = "GetValueConverter";

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the name of the <c>GetValueComparer</c> metadata accessor.
        /// </summary>
        /// =================================================================================================
        private const string MethodNameGetValueComparer = "GetValueComparer";

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the name of the <c>ValueConverter.ConvertToProvider</c> delegate property.
        /// </summary>
        /// =================================================================================================
        private const string MethodNameConvertToProvider = "ConvertToProvider";

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the name of the <c>ValueComparer.Equals(object, object)</c> method.
        /// </summary>
        /// =================================================================================================
        private const string MethodNameEquals = "Equals";

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Cache for the <c>GetTableName</c> extension method resolved via the Relational assembly.
        /// </summary>
        /// =================================================================================================
        private static MethodInfo _getTableNameMethod;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Cache for the <c>GetSchema</c> extension method resolved via the Relational assembly.
        /// </summary>
        /// =================================================================================================
        private static MethodInfo _getSchemaMethod;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Cache for the <c>GetColumnName</c> extension method resolved via the Relational assembly.
        /// </summary>
        /// =================================================================================================
        private static MethodInfo _getColumnNameMethod;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets the <c>Name</c> of an EF Core property metadata object.
        /// </summary>
        /// <param name="property">An <c>IProperty</c> / <c>IPropertyBase</c> instance.</param>
        /// <returns>
        ///     The property name.
        /// </returns>
        /// =================================================================================================
        internal static string GetName(object property)
        {
            var type = property.GetType();
            var pi = NameCache.GetOrAdd(type, t => ResolveProperty(t, "Name"));

            return (string)pi?.GetValue(property);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets the <c>ClrType</c> of an EF Core property metadata object.
        /// </summary>
        /// <param name="property">An <c>IProperty</c> / <c>IPropertyBase</c> instance.</param>
        /// <returns>
        ///     The CLR type of the property.
        /// </returns>
        /// =================================================================================================
        internal static Type GetClrType(object property)
        {
            var type = property.GetType();
            var pi = ClrTypeCache.GetOrAdd(type, t => ResolveProperty(t, "ClrType"));

            return (Type)pi?.GetValue(property);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Returns a human-readable full name for the given CLR type. For <see cref="Nullable{T}" />
        ///     types the underlying type name is returned with a trailing <c>?</c> (e.g. <c>
        ///     System.DateTime?</c>) instead of the verbose assembly-qualified generic representation.
        /// </summary>
        /// <param name="type">The CLR type (maybe <c>null</c>).</param>
        /// <returns>
        ///     The clean type name, or <c>null</c> when <paramref name="type" /> is <c>null</c>.
        /// </returns>
        /// =================================================================================================
        internal static string GetCleanTypeName(Type type)
        {
            if (type.IsNull())
                return null;

            var underlying = Nullable.GetUnderlyingType(type);

            return underlying.IsNotNull() ? underlying!.FullName + "?" : type.FullName;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets the value converter explicitly configured for an EF Core property, or <c>null</c>
        ///     when the property is not mapped through one.
        /// </summary>
        /// <param name="property">An <c>IProperty</c> / <c>IPropertyBase</c> instance.</param>
        /// <returns>
        ///     The <c>ValueConverter</c> instance, or <c>null</c>.
        /// </returns>
        /// =================================================================================================
        internal static object GetValueConverter(object property)
        {
            var accessor = ValueConverterCache.GetOrAdd(property.GetType(),
                t => ResolveMetadataAccessor(t, MethodNameGetValueConverter));

            return accessor?.Invoke(property);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets the value comparer EF Core resolved for a property - explicitly configured, or the
        ///     default comparer EF Core's type mapping assigns for the property's CLR type - or <c>null</c>
        ///     when none could be resolved.
        /// </summary>
        /// <param name="property">An <c>IProperty</c> / <c>IPropertyBase</c> instance.</param>
        /// <returns>
        ///     The <c>ValueComparer</c> instance, or <c>null</c>.
        /// </returns>
        /// =================================================================================================
        internal static object GetValueComparer(object property)
        {
            var accessor = ValueComparerCache.GetOrAdd(property.GetType(),
                t => ResolveMetadataAccessor(t, MethodNameGetValueComparer));

            return accessor?.Invoke(property);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Runs the <c>ConvertToProvider</c> delegate of a value converter over a value.
        /// </summary>
        /// <param name="converter">The <c>ValueConverter</c> instance.</param>
        /// <param name="value">The model value to convert.</param>
        /// <returns>
        ///     The provider representation, or the given value when the delegate cannot be resolved.
        /// </returns>
        /// =================================================================================================
        internal static object ConvertToProvider(object converter, object value)
        {
            var pi = ConvertToProviderCache.GetOrAdd(converter.GetType(),
                t => ResolveProperty(t, MethodNameConvertToProvider));
            var convert = pi?.GetValue(converter) as Func<object, object>;

            return convert.IsNull() ? value : convert!(value);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Runs the <c>Equals(object, object)</c> method of a value comparer over a pair of model
        ///     values.
        /// </summary>
        /// <param name="comparer">The <c>ValueComparer</c> instance.</param>
        /// <param name="a">The first model value.</param>
        /// <param name="b">The second model value.</param>
        /// <returns>
        ///     True when the comparer reports the values as equal.
        /// </returns>
        /// <exception cref="MissingMethodException">
        ///     The <c>Equals(object, object)</c> method could not be resolved on the comparer's runtime
        ///     type.
        /// </exception>
        /// =================================================================================================
        internal static bool ComparerEquals(object comparer, object a, object b)
        {
            var equals = ComparerEqualsCache.GetOrAdd(comparer.GetType(), ResolveComparerEqualsAccessor);
            if (equals.IsNull())
                throw new MissingMethodException(comparer.GetType().FullName, MethodNameEquals);

            return equals(comparer, a, b);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Resolves the accessor for a parameterless metadata method (<c>GetValueConverter</c>,
        ///     <c>GetValueComparer</c>, ...): first as an instance method on the property metadata's
        ///     runtime type (EF Core 6+), then as a matching static method on <c>PropertyExtensions</c>
        ///     (EF Core 5). See the class <see cref="PropertyMetadataHelper" /> remarks for why both
        ///     paths are needed.
        /// </summary>
        /// <param name="runtimeType">The runtime type of the property metadata object to resolve the
        ///     accessor for.</param>
        /// <param name="methodName">The name of the parameterless accessor method to resolve.</param>
        /// <returns>
        ///     A delegate that invokes the resolved accessor, or <c>null</c> when neither path resolves.
        /// </returns>
        /// =================================================================================================
        private static Func<object, object> ResolveMetadataAccessor(Type runtimeType, string methodName)
        {
            var instanceMethod = ResolveParameterlessMethod(runtimeType, methodName);
            if (instanceMethod.IsNotNull())
                return target => instanceMethod.Invoke(target, null);

            var extensionType = runtimeType.Assembly.GetType(PropertyExtensionsTypeName);
            if (extensionType.IsNull())
                return null;

            foreach (var mi in extensionType.GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                if (mi.Name != methodName)
                    continue;

                var parameters = mi.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(runtimeType))
                    return target => mi.Invoke(null, new[] { target });
            }

            return null;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Resolves the <c>ValueComparer.Equals(object, object)</c> delegate for a comparer's runtime
        ///     type. The method is declared once, publicly, on the abstract <c>ValueComparer</c> base
        ///     class, so a single reflection lookup on the concrete runtime type is enough - no
        ///     interface-fallback or <c>PropertyExtensions</c> path is needed here.
        /// </summary>
        /// <param name="runtimeType">The runtime type of the comparer instance.</param>
        /// <returns>
        ///     A delegate that invokes <c>Equals(object, object)</c> on a given comparer instance, or
        ///     <c>null</c> when the method could not be resolved.
        /// </returns>
        /// =================================================================================================
        private static Func<object, object, object, bool> ResolveComparerEqualsAccessor(Type runtimeType)
        {
            var mi = runtimeType.GetMethod(MethodNameEquals, new[] { typeof(object), typeof(object) });
            if (mi.IsNull())
                return null;

            return (target, a, b) => (bool)mi!.Invoke(target, new[] { a, b });
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Resolves a parameterless instance method on a runtime type, scanning the implemented
        ///     interfaces when the concrete type does not declare it.
        /// </summary>
        /// <param name="runtimeType">The runtime type to resolve the method on.</param>
        /// <param name="methodName">The name of the method to resolve.</param>
        /// <returns>
        ///     The resolved <see cref="MethodInfo" />, or <c>null</c> when not found.
        /// </returns>
        /// =================================================================================================
        private static MethodInfo ResolveParameterlessMethod(Type runtimeType, string methodName)
        {
            var mi = runtimeType.GetMethod(methodName, Type.EmptyTypes);
            if (mi.IsNotNull())
                return mi;

            foreach (var iface in runtimeType.GetInterfaces())
            {
                mi = iface.GetMethod(methodName, Type.EmptyTypes);
                if (mi.IsNotNull())
                    return mi;
            }

            return null;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Resolves a property by name on a runtime type, scanning the implemented interfaces when the
        ///     concrete type does not declare it.
        /// </summary>
        /// <param name="runtimeType">The runtime type to resolve the property on.</param>
        /// <param name="propertyName">The name of the property to resolve.</param>
        /// <returns>
        ///     The resolved <see cref="PropertyInfo" />, or <c>null</c> when not found.
        /// </returns>
        /// =================================================================================================
        private static PropertyInfo ResolveProperty(Type runtimeType, string propertyName)
        {
            var pi = runtimeType.GetProperty(propertyName);
            if (pi.IsNotNull())
                return pi;

            foreach (var iface in runtimeType.GetInterfaces())
            {
                pi = iface.GetProperty(propertyName);
                if (pi.IsNotNull())
                    return pi;
            }

            return null;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Invokes <c>GetTableName()</c> on an <c>IEntityType</c> via reflection. Works across EF
        ///     Core 5 (<c>IEntityType</c> overload) and 6+ (<c>IReadOnlyEntityType</c> overload).
        /// </summary>
        /// <param name="entityType">An EF Core entity type metadata object.</param>
        /// <returns>
        ///     The table name, or <c>null</c>.
        /// </returns>
        /// =================================================================================================
        internal static string GetTableName(object entityType)
        {
            var method = ResolveRelationalExtensionMethod(
                ref _getTableNameMethod,
                "RelationalEntityTypeExtensions",
                "GetTableName",
                entityType);

            return (string)method?.Invoke(null, new[] { entityType });
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Invokes <c>GetSchema()</c> on an <c>IEntityType</c> via reflection.
        /// </summary>
        /// <param name="entityType">An EF Core entity type metadata object.</param>
        /// <returns>
        ///     The schema name, or <c>null</c>.
        /// </returns>
        /// =================================================================================================
        internal static string GetSchema(object entityType)
        {
            var method = ResolveRelationalExtensionMethod(
                ref _getSchemaMethod,
                "RelationalEntityTypeExtensions",
                "GetSchema",
                entityType);

            return (string)method?.Invoke(null, new[] { entityType });
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Invokes <c>GetColumnName()</c> on an <c>IProperty</c> via reflection.
        /// </summary>
        /// <param name="property">An EF Core property metadata object.</param>
        /// <returns>
        ///     The column name, or <c>null</c>.
        /// </returns>
        /// =================================================================================================
        internal static string GetColumnName(object property)
        {
            var method = ResolveRelationalExtensionMethod(
                ref _getColumnNameMethod,
                "RelationalPropertyExtensions",
                "GetColumnName",
                property);

            return (string)method?.Invoke(null, new[] { property });
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Finds a single-parameter static extension method in the EF Core Relational assembly
        ///     whose parameter type is assignable from the runtime type of <paramref name="instance"/>.
        /// </summary>
        /// =================================================================================================
        private static MethodInfo ResolveRelationalExtensionMethod(ref MethodInfo cached, string extensionClassName,
            string methodName, object instance)
        {
            if (cached != null)
                return cached;

            var instanceType = instance.GetType();

            // Search all loaded assemblies for the extension class
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.FullName.Contains("EntityFrameworkCore.Relational"))
                    continue;

                foreach (var type in asm.GetTypes())
                {
                    if (type.Name != extensionClassName)
                        continue;

                    foreach (var mi in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
                    {
                        if (mi.Name != methodName)
                            continue;

                        var parameters = mi.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(instanceType))
                        {
                            cached = mi;

                            return cached;
                        }
                    }
                }
            }

            return null;
        }
    }
}