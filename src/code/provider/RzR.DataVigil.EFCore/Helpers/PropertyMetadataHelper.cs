// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-04-15 12:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 12:51
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

#endregion

namespace RzR.DataVigil.EFCore.Helpers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Accesses EF Core property metadata via reflection so that the same compiled assembly
    ///     works at runtime against EF Core 5.x through 8.x+. In EF Core 6 the <c>Name</c> and <c>
    ///     ClrType</c> members moved from
    ///     <c>IPropertyBase</c> to the new <c>IReadOnlyPropertyBase</c> interface, which causes
    ///     <see cref="MissingMethodException" /> when code compiled against EF 5 runs on EF 8.
    ///     Resolving through the concrete runtime type avoids that binary break.
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
        ///     Returns a human-readable full name for the given CLR type.
        ///     For <see cref="Nullable{T}" /> types the underlying type name is returned
        ///     with a trailing <c>?</c> (e.g. <c>System.DateTime?</c>) instead of the
        ///     verbose assembly-qualified generic representation.
        /// </summary>
        /// <param name="type">The CLR type (may be <c>null</c>).</param>
        /// <returns>
        ///     The clean type name, or <c>null</c> when <paramref name="type" /> is <c>null</c>.
        /// </returns>
        /// =================================================================================================
        internal static string GetCleanTypeName(Type type)
        {
            if (type == null)
                return null;

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                return underlying.FullName + "?";

            return type.FullName;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Resolves a property by name on a runtime type. First checks the concrete type hierarchy
        ///     (works for EF Core 5 <c>EntityType</c>/<c>Property</c> classes). If not found, falls
        ///     back to scanning implemented interfaces — required for EF Core 6+ where
        ///     <c>RuntimeEntityType</c>/<c>RuntimeProperty</c> expose metadata through default
        ///     interface methods on <c>IReadOnlyTypeBase</c>/<c>IReadOnlyPropertyBase</c> only.
        /// </summary>
        /// =================================================================================================
        private static PropertyInfo ResolveProperty(Type runtimeType, string propertyName)
        {
            // Direct lookup (EF Core 5 concrete types)
            var pi = runtimeType.GetProperty(propertyName);
            if (pi != null)
                return pi;

            // Interface fallback (EF Core 6+ RuntimeEntityType/RuntimeProperty)
            foreach (var iface in runtimeType.GetInterfaces())
            {
                pi = iface.GetProperty(propertyName);
                if (pi != null)
                    return pi;
            }

            return null;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Invokes <c>GetTableName()</c> on an <c>IEntityType</c> via reflection.
        ///     Works across EF Core 5 (<c>IEntityType</c> overload) and 6+
        ///     (<c>IReadOnlyEntityType</c> overload).
        /// </summary>
        /// <param name="entityType">An EF Core entity type metadata object.</param>
        /// <returns>The table name, or <c>null</c>.</returns>
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
        /// <returns>The schema name, or <c>null</c>.</returns>
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
        /// <returns>The column name, or <c>null</c>.</returns>
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
        private static MethodInfo ResolveRelationalExtensionMethod(
            ref MethodInfo cached,
            string extensionClassName,
            string methodName,
            object instance)
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