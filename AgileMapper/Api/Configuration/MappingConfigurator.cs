﻿namespace AgileObjects.AgileMapper.Api.Configuration
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using Extensions;
    using Members;

    public class MappingConfigurator<TSource, TTarget>
    {
        private readonly MappingConfigInfo _configInfo;

        internal MappingConfigurator(MappingConfigInfo configInfo)
        {
            _configInfo = configInfo;
        }

        public CustomDataSourceTargetMemberSpecifier<TSource, TTarget> Map<TSourceValue>(
            Expression<Func<ITypedMemberMappingContext<TSource, TTarget>, TSourceValue>> valueFactoryExpression)
        {
            return new CustomDataSourceTargetMemberSpecifier<TSource, TTarget>(
                _configInfo.ForSourceValueType(typeof(TSourceValue)),
                context => valueFactoryExpression.ReplaceParameterWith(context.Parameter));
        }

        public CustomDataSourceTargetMemberSpecifier<TSource, TTarget> Map<TSourceValue>(
            Expression<Func<TSource, TTarget, TSourceValue>> valueFactoryExpression)
        {
            return new CustomDataSourceTargetMemberSpecifier<TSource, TTarget>(
                _configInfo.ForSourceValueType(typeof(TSourceValue)),
                context => valueFactoryExpression.ReplaceParametersWith(context.SourceObject, context.InstanceVariable));
        }

        public CustomDataSourceTargetMemberSpecifier<TSource, TTarget> MapFunc<TSourceValue>(Func<TSource, TSourceValue> valueFunc)
        {
            return GetConstantTargetMemberSpecifier(valueFunc);
        }

        public CustomDataSourceTargetMemberSpecifier<TSource, TTarget> Map<TSourceValue>(TSourceValue value)
        {
            Expression valueFactoryExpression;
            Type valueFactoryReturnType;

            return TryGetValueFactory(value, out valueFactoryExpression, out valueFactoryReturnType)
                ? new CustomDataSourceTargetMemberSpecifier<TSource, TTarget>(
                    _configInfo.ForSourceValueType(valueFactoryReturnType),
                    context => Expression.Invoke(valueFactoryExpression, context.Parameter))
                : GetConstantTargetMemberSpecifier(value);
        }

        #region Map Helpers

        private static bool TryGetValueFactory<TSourceValue>(
            TSourceValue value,
            out Expression valueFactoryExpression,
            out Type valueFactoryReturnType)
        {
            if (typeof(TSourceValue).IsGenericType &&
                (typeof(TSourceValue).GetGenericTypeDefinition() == typeof(Func<,>)))
            {
                var funcTypeArguments = typeof(TSourceValue).GetGenericArguments();
                var contextTypeArgument = funcTypeArguments.First();

                if (contextTypeArgument.IsGenericType &&
                    (contextTypeArgument.GetGenericTypeDefinition() == typeof(ITypedMemberMappingContext<,>)))
                {
                    var contextTypes = contextTypeArgument.GetGenericArguments();

                    if (typeof(TSource).IsAssignableFrom(contextTypes.First()))
                    {
                        valueFactoryExpression = Expression.Constant(value, typeof(TSourceValue));
                        valueFactoryReturnType = funcTypeArguments.Last();
                        return true;
                    }
                }
            }

            valueFactoryExpression = null;
            valueFactoryReturnType = null;
            return false;
        }

        private CustomDataSourceTargetMemberSpecifier<TSource, TTarget> GetConstantTargetMemberSpecifier<TSourceValue>(TSourceValue value)
        {
            var valueConstant = Expression.Constant(value, typeof(TSourceValue));

            return new CustomDataSourceTargetMemberSpecifier<TSource, TTarget>(
                _configInfo.ForSourceValueType(valueConstant.Type),
                instance => valueConstant);
        }

        #endregion

        public ConditionSpecifier<TSource, TTarget> Ignore<TTargetValue>(Expression<Func<TTarget, TTargetValue>> targetMember)
        {
            var configuredIgnoredMember = ConfiguredIgnoredMember.For(
                _configInfo,
                typeof(TTarget),
                targetMember.Body);

            _configInfo.MapperContext.UserConfigurations.Add(configuredIgnoredMember);

            return new ConditionSpecifier<TSource, TTarget>(configuredIgnoredMember, negateCondition: true);
        }

        public PreEventMappingConfigStartingPoint<TSource, TTarget> Before => new PreEventMappingConfigStartingPoint<TSource, TTarget>(_configInfo);

        public PostEventMappingConfigStartingPoint<TSource, TTarget> After => new PostEventMappingConfigStartingPoint<TSource, TTarget>(_configInfo);
    }
}