// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 19:09
// ***********************************************************************
//  <copyright file="EntityGdprPolicyBuilder.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using RzR.DataVigil.Abstractions.Contracts;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Gdpr;

#endregion

namespace RzR.DataVigil.Core.Gdpr
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Fluent builder for configuring GDPR policies per entity type.
    /// </summary>
    /// <typeparam name="T">Generic type parameter.</typeparam>
    /// =================================================================================================
    public sealed class EntityGdprPolicyBuilder<T> where T : class, IAuditable
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the retrieval rules.
        /// </summary>
        /// =================================================================================================
        private readonly IList<FieldGdprRule> _retrievalRules = new List<FieldGdprRule>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the storage rules.
        /// </summary>
        /// =================================================================================================
        private readonly IList<FieldGdprRule> _storageRules = new List<FieldGdprRule>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Exclude on storage.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns>
        ///     An EntityGdprPolicyBuilder&lt;T&gt;
        /// </returns>
        /// =================================================================================================
        public EntityGdprPolicyBuilder<T> ExcludeOnStorage(Expression<Func<T, object>> field)
        {
            return AddStorageRule(field, GdprFieldAction.Exclude);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Mask on storage.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns>
        ///     An EntityGdprPolicyBuilder&lt;T&gt;
        /// </returns>
        /// =================================================================================================
        public EntityGdprPolicyBuilder<T> MaskOnStorage(Expression<Func<T, object>> field)
        {
            return AddStorageRule(field, GdprFieldAction.Mask);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Anonymize on storage.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns>
        ///     An EntityGdprPolicyBuilder&lt;T&gt;
        /// </returns>
        /// =================================================================================================
        public EntityGdprPolicyBuilder<T> AnonymizeOnStorage(Expression<Func<T, object>> field)
        {
            return AddStorageRule(field, GdprFieldAction.Anonymize);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Hash on storage.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns>
        ///     An EntityGdprPolicyBuilder&lt;T&gt;
        /// </returns>
        /// =================================================================================================
        public EntityGdprPolicyBuilder<T> HashOnStorage(Expression<Func<T, object>> field)
        {
            return AddStorageRule(field, GdprFieldAction.Hash);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Transform on storage.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="transformer">The transformer.</param>
        /// <returns>
        ///     An EntityGdprPolicyBuilder&lt;T&gt;
        /// </returns>
        /// =================================================================================================
        public EntityGdprPolicyBuilder<T> TransformOnStorage(
            Expression<Func<T, object>> field,
            Func<string, string> transformer)
        {
            return AddStorageRule(field, GdprFieldAction.Custom, transformer);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Mask on retrieval.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="access">(Optional) The access.</param>
        /// <returns>
        ///     An EntityGdprPolicyBuilder&lt;T&gt;
        /// </returns>
        /// =================================================================================================
        public EntityGdprPolicyBuilder<T> MaskOnRetrieval(
            Expression<Func<T, object>> field,
            Action<GdprAccessBuilder> access = null)
        {
            return AddRetrievalRule(field, GdprFieldAction.Mask, access);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Anonymize on retrieval.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="access">(Optional) The access.</param>
        /// <returns>
        ///     An EntityGdprPolicyBuilder&lt;T&gt;
        /// </returns>
        /// =================================================================================================
        public EntityGdprPolicyBuilder<T> AnonymizeOnRetrieval(
            Expression<Func<T, object>> field,
            Action<GdprAccessBuilder> access = null)
        {
            return AddRetrievalRule(field, GdprFieldAction.Anonymize, access);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets the build.
        /// </summary>
        /// <returns>
        ///     An EntityGdprPolicy.
        /// </returns>
        /// =================================================================================================
        internal EntityGdprPolicy Build()
        {
            return new EntityGdprPolicy
            {
                EntityType = typeof(T),
                StorageRules = _storageRules,
                RetrievalRules = _retrievalRules
            };
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Adds a storage rule.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="action">The action.</param>
        /// <param name="transformer">(Optional) The transformer.</param>
        /// <returns>
        ///     An EntityGdprPolicyBuilder&lt;T&gt;
        /// </returns>
        /// =================================================================================================
        private EntityGdprPolicyBuilder<T> AddStorageRule(
            Expression<Func<T, object>> field,
            GdprFieldAction action,
            Func<string, string> transformer = null)
        {
            _storageRules.Add(new FieldGdprRule
            {
                FieldName = GetPropertyName(field),
                Action = action,
                CustomTransformer = transformer
            });
            return this;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Adds a retrieval rule.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="action">The action.</param>
        /// <param name="accessConfig">The access configuration.</param>
        /// <returns>
        ///     An EntityGdprPolicyBuilder&lt;T&gt;
        /// </returns>
        /// =================================================================================================
        private EntityGdprPolicyBuilder<T> AddRetrievalRule(
            Expression<Func<T, object>> field,
            GdprFieldAction action,
            Action<GdprAccessBuilder> accessConfig)
        {
            var accessBuilder = new GdprAccessBuilder();
            accessConfig?.Invoke(accessBuilder);

            _retrievalRules.Add(new FieldGdprRule
            {
                FieldName = GetPropertyName(field),
                Action = action,
                AllowedRoles = new List<string>(accessBuilder.Roles),
                AllowedClaims = new Dictionary<string, string>(accessBuilder.Claims)
            });
            return this;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets property name.
        /// </summary>
        /// <exception cref="ArgumentException">
        ///     Thrown when one or more arguments have unsupported or illegal values.
        /// </exception>
        /// <param name="expression">The expression.</param>
        /// <returns>
        ///     The property name.
        /// </returns>
        /// =================================================================================================
        private static string GetPropertyName(Expression<Func<T, object>> expression)
        {
            MemberExpression memberExpression;

            if (expression.Body is UnaryExpression unaryExpression)
                memberExpression = unaryExpression.Operand as MemberExpression;
            else
                memberExpression = expression.Body as MemberExpression;

            if (memberExpression == null)
                throw new ArgumentException(
                    "Expression must be a member access expression.",
                    nameof(expression));

            return memberExpression.Member.Name;
        }
    }
}