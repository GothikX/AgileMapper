﻿namespace AgileObjects.AgileMapper.ObjectPopulation
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using Extensions;
    using Members;

    internal static class MappingFactory
    {
        public static Expression GetDerivedTypeMapping(
            IObjectMappingData declaredTypeMappingData,
            Expression sourceValue,
            Type targetType)
        {
            var declaredTypeMapperData = declaredTypeMappingData.MapperData;

            var targetValue = declaredTypeMapperData.TargetMember.IsReadable
                ? declaredTypeMapperData.TargetObject.GetConversionTo(targetType)
                : Expression.Default(targetType);

            var derivedTypeMappingData = declaredTypeMappingData.WithTypes(sourceValue.Type, targetType);

            if (declaredTypeMappingData.IsRoot)
            {
                return GetDerivedTypeRootMapping(derivedTypeMappingData, sourceValue, targetValue);
            }

            if (declaredTypeMapperData.TargetMemberIsEnumerableElement())
            {
                return GetInlineElementMappingBlock(derivedTypeMappingData, sourceValue, targetValue);
            }

            return GetDerivedTypeChildMapping(derivedTypeMappingData, sourceValue, targetValue);
        }

        private static Expression GetDerivedTypeRootMapping(
            IObjectMappingData derivedTypeMappingData,
            Expression sourceValue,
            Expression targetValue)
        {
            var declaredTypeMapperData = derivedTypeMappingData.DeclaredTypeMappingData.MapperData;

            var mappingValues = new MappingValues(
                sourceValue,
                targetValue,
                Expression.Default(typeof(int?)));

            var inlineMappingBlock = GetInlineMappingBlock(
                derivedTypeMappingData,
                MappingDataFactory.ForRootMethod,
                mappingValues,
                new[]
                {
                    mappingValues.SourceValue,
                    mappingValues.TargetValue,
                    Expression.Property(declaredTypeMapperData.MappingDataObject, "MappingContext")
                });

            return inlineMappingBlock;
        }

        private static Expression GetDerivedTypeChildMapping(
            IObjectMappingData derivedTypeMappingData,
            Expression sourceValue,
            Expression targetValue)
        {
            var derivedTypeMapperData = derivedTypeMappingData.MapperData;
            var declaredTypeMapperData = derivedTypeMappingData.DeclaredTypeMappingData.MapperData;

            var mappingValues = new MappingValues(
                sourceValue,
                targetValue,
                derivedTypeMapperData.Parent.EnumerableIndex);

            return GetChildMapping(
                derivedTypeMapperData.SourceMember,
                mappingValues,
                declaredTypeMapperData.DataSourceIndex,
                derivedTypeMappingData,
                derivedTypeMapperData,
                declaredTypeMapperData);
        }

        public static Expression GetChildMapping(int dataSourceIndex, IMemberMappingData childMappingData)
        {
            var childMapperData = childMappingData.MapperData;
            var relativeMember = childMapperData.SourceMember.RelativeTo(childMapperData.SourceMember);
            var sourceMemberAccess = relativeMember.GetQualifiedAccess(childMapperData.SourceObject);

            return GetChildMapping(
                relativeMember,
                sourceMemberAccess,
                dataSourceIndex,
                childMappingData);
        }

        public static Expression GetChildMapping(
            IQualifiedMember sourceMember,
            Expression sourceMemberAccess,
            int dataSourceIndex,
            IMemberMappingData childMappingData)
        {
            var childMapperData = childMappingData.MapperData;
            var targetMemberAccess = childMapperData.GetTargetMemberAccess();

            var mappingValues = new MappingValues(
                sourceMemberAccess,
                targetMemberAccess,
                childMapperData.Parent.EnumerableIndex);

            return GetChildMapping(
                sourceMember,
                mappingValues,
                dataSourceIndex,
                childMappingData.Parent,
                childMapperData,
                childMapperData.Parent);
        }

        private static Expression GetChildMapping(
            IQualifiedMember sourceMember,
            MappingValues mappingValues,
            int dataSourceIndex,
            IObjectMappingData parentMappingData,
            IMemberMapperData childMapperData,
            ObjectMapperData declaredTypeMapperData)
        {
            var childMappingData = ObjectMappingDataFactory.ForChild(
                sourceMember,
                childMapperData.TargetMember,
                dataSourceIndex,
                parentMappingData);

            if (childMappingData.MapperKey.MappingTypes.RuntimeTypesNeeded)
            {
                return declaredTypeMapperData.GetMapCall(mappingValues.SourceValue, childMapperData.TargetMember, dataSourceIndex);
            }

            if (childMapperData.TargetMemberEverRecurses())
            {
                var mapRecursionCall = GetMapRecursionCallFor(
                    childMappingData,
                    mappingValues.SourceValue,
                    dataSourceIndex,
                    declaredTypeMapperData);

                return mapRecursionCall;
            }

            var inlineMappingBlock = GetInlineMappingBlock(
                childMappingData,
                MappingDataFactory.ForChildMethod,
                mappingValues,
                new[]
                {
                    mappingValues.SourceValue,
                    mappingValues.TargetValue,
                    mappingValues.EnumerableIndex,
                    Expression.Constant(childMapperData.TargetMember.RegistrationName),
                    Expression.Constant(dataSourceIndex),
                    childMapperData.Parent.MappingDataObject
                });

            return inlineMappingBlock;
        }

        private static Expression GetMapRecursionCallFor(
            IObjectMappingData childMappingData,
            Expression sourceValue,
            int dataSourceIndex,
            ObjectMapperData declaredTypeMapperData)
        {
            var childMapperData = childMappingData.MapperData;

            childMapperData.RegisterRequiredMapperFunc(childMappingData);

            var mapRecursionCall = declaredTypeMapperData.GetMapRecursionCall(
                sourceValue,
                childMapperData.TargetMember,
                dataSourceIndex);

            return mapRecursionCall;
        }

        public static Expression GetElementMapping(
            Expression sourceElementValue,
            Expression targetElementValue,
            IObjectMappingData enumerableMappingData)
        {
            var enumerableMapperData = enumerableMappingData.MapperData;

            var elementMappingData = ObjectMappingDataFactory.ForElement(enumerableMappingData);

            if (elementMappingData.MapperKey.MappingTypes.RuntimeTypesNeeded)
            {
                return enumerableMapperData.GetMapCall(sourceElementValue, targetElementValue);
            }

            return GetInlineElementMappingBlock(elementMappingData, sourceElementValue, targetElementValue);
        }

        private static Expression GetInlineElementMappingBlock(
            IObjectMappingData elementMappingData,
            Expression sourceElementValue,
            Expression targetElementValue)
        {
            var enumerableMapperData = elementMappingData.Parent.MapperData;
            var elementMapperData = elementMappingData.MapperData;

            Expression enumerableIndex, parentMappingDataObject;

            if (elementMapperData.Context.IsStandalone)
            {
                enumerableIndex = Expression.Property(elementMapperData.EnumerableIndex, "Value");
                parentMappingDataObject = Expression.Default(typeof(IObjectMappingData));
            }
            else
            {
                enumerableIndex = enumerableMapperData.EnumerablePopulationBuilder.Counter;
                parentMappingDataObject = enumerableMapperData.MappingDataObject;
            }

            var mappingValues = new MappingValues(
                sourceElementValue,
                targetElementValue,
                enumerableIndex);

            return GetInlineMappingBlock(
                elementMappingData,
                MappingDataFactory.ForElementMethod,
                mappingValues,
                new[]
                {
                    mappingValues.SourceValue,
                    mappingValues.TargetValue,
                    mappingValues.EnumerableIndex,
                    parentMappingDataObject
                });
        }

        private static Expression GetInlineMappingBlock(
            IObjectMappingData childMappingData,
            MethodInfo createMethod,
            MappingValues mappingValues,
            Expression[] createMethodCallArguments)
        {
            var childMapper = childMappingData.Mapper;

            if (childMapper.MappingExpression.NodeType != ExpressionType.Try)
            {
                return childMapper.MappingExpression;
            }

            if (!childMapper.MapperData.Context.UsesMappingDataObject)
            {
                return GetDirectAccessMapping(
                    childMappingData,
                    mappingValues,
                    createMethod,
                    createMethodCallArguments);
            }

            var createInlineMappingDataCall = GetCreateMappingDataCall(
                createMethod,
                childMapper.MapperData,
                createMethodCallArguments);

            var inlineMappingDataVariable = childMapper.MapperData.MappingDataObject;

            var inlineMappingDataAssignment = Expression
                .Assign(inlineMappingDataVariable, createInlineMappingDataCall);

            var mappingTryCatch = (TryExpression)childMapper.MappingExpression;

            var updatedTryCatch = mappingTryCatch.Update(
                Expression.Block(inlineMappingDataAssignment, mappingTryCatch.Body),
                mappingTryCatch.Handlers,
                mappingTryCatch.Finally,
                mappingTryCatch.Fault);

            var mappingBlock = Expression.Block(new[] { inlineMappingDataVariable }, updatedTryCatch);

            return mappingBlock;
        }

        private static Expression GetDirectAccessMapping(
            IObjectMappingData mappingData,
            MappingValues mappingValues,
            MethodInfo createMethod,
            Expression[] createMethodCallArguments)
        {
            var mapper = mappingData.Mapper;
            var mapperData = mappingData.MapperData;

            var replacementsByTarget = new ExpressionReplacementDictionary
            {
                [mapperData.SourceObject] = mappingValues.SourceValue,
                [mapperData.TargetObject] = mappingValues.TargetValue,
                [mapperData.EnumerableIndex] = mappingValues.EnumerableIndex.GetConversionTo(mapperData.EnumerableIndex.Type)
            };

            var directAccessMapping = mapper.MappingLambda.Body.Replace(replacementsByTarget);

            var createInlineMappingDataCall = GetCreateMappingDataCall(
                createMethod,
                mapperData,
                createMethodCallArguments);

            directAccessMapping = directAccessMapping.Replace(
                mapperData.MappingDataObject,
                createInlineMappingDataCall);

            return directAccessMapping;
        }

        private static Expression GetCreateMappingDataCall(
            MethodInfo createMethod,
            ObjectMapperData childMapperData,
            Expression[] createMethodCallArguments)
        {
            if (childMapperData.Context.IsStandalone)
            {
                return childMapperData.DeclaredTypeMapperData
                    .GetAsCall(childMapperData.SourceType, childMapperData.TargetType);
            }

            return Expression.Call(
                createMethod.MakeGenericMethod(childMapperData.SourceType, childMapperData.TargetType),
                createMethodCallArguments);
        }
    }

    internal class MappingValues
    {
        public MappingValues(Expression sourceValue, Expression targetValue, Expression enumerableIndex)
        {
            SourceValue = sourceValue;
            TargetValue = targetValue;
            EnumerableIndex = enumerableIndex;
        }

        public Expression SourceValue { get; }

        public Expression TargetValue { get; }

        public Expression EnumerableIndex { get; }
    }
}